using FactoryERP.Core.Constants;
using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ISystemSettingService _settings;
    private readonly IManufacturingAccountingService _accounting;
    private readonly ICurrentUserService? _currentUser;

    public PurchaseOrderService(
        IDbContextFactory<AppDbContext> contextFactory,
        ISystemSettingService settings,
        IManufacturingAccountingService accounting,
        ICurrentUserService? currentUser = null)
    {
        _contextFactory = contextFactory;
        _settings = settings;
        _accounting = accounting;
        _currentUser = currentUser;
    }

    public async Task<List<PurchaseOrder>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .ThenInclude(i => i.RawMaterial)
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PurchaseOrder> ReceivePurchaseAsync(ReceivePurchaseRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new InvalidOperationException("La compra debe incluir al menos un material.");

        decimal taxRate = await _settings.GetDecimalAsync(SettingKeys.FiscalDefaultTaxRate, 0.18m);
        PurchaseOrder order;

        await InventoryConcurrencyGate.RawMaterialGate.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            using var tx = await context.Database.BeginTransactionAsync();
            var supplier = await context.Suppliers.FindAsync(request.SupplierId)
                ?? throw new InvalidOperationException("Suplidor no encontrado.");
            if (!supplier.IsActive)
                throw new InvalidOperationException($"El suplidor '{supplier.Name}' está inactivo y no puede registrar compras.");

            var materialIds = request.Items.Select(i => i.RawMaterialId).Distinct().OrderBy(id => id).ToList();
            var materials = await context.RawMaterials
                .Where(m => materialIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            decimal subTotal = 0m;
            decimal totalTax = 0m;
            var items = new List<PurchaseOrderItem>();
            var batches = new List<RawMaterialBatch>();
            string orderNumber = $"OC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";
            DateTime purchaseDate = request.PurchaseDate ?? DateTime.UtcNow;

            foreach (var reqItem in request.Items.OrderBy(i => i.RawMaterialId))
            {
                if (reqItem.Quantity <= 0m || reqItem.UnitCost < 0m)
                    throw new ArgumentException("La cantidad debe ser mayor a cero y el costo no puede ser negativo.");

                if (!materials.TryGetValue(reqItem.RawMaterialId, out var material))
                    throw new InvalidOperationException($"Materia prima ID {reqItem.RawMaterialId} no encontrada.");
                if (!material.IsActive)
                    throw new InvalidOperationException($"La materia prima '{material.Name}' ({material.Sku}) está inactiva y no puede recibirse en compras.");

                decimal lineSub = Math.Round(reqItem.Quantity * reqItem.UnitCost, 2, MidpointRounding.AwayFromZero);
                decimal lineTax = reqItem.IsTaxable ? Math.Round(lineSub * taxRate, 2, MidpointRounding.AwayFromZero) : 0m;
                decimal lineTotal = lineSub + lineTax;

                string batchCode = string.IsNullOrWhiteSpace(reqItem.BatchCode)
                    ? $"LMP-{purchaseDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}"
                    : ProductionPlannerHelper.TruncateNotes(reqItem.BatchCode.Trim().ToUpperInvariant(), 40);
                DateTime? expDate = reqItem.ExpirationDate.HasValue
                    ? DateTime.SpecifyKind(reqItem.ExpirationDate.Value.Date, DateTimeKind.Utc)
                    : null;

                material.AverageCost = ProductionCostCalculator.CalculateWeightedAverageCost(
                    material.CurrentStock, material.AverageCost, reqItem.Quantity, reqItem.UnitCost);
                material.LastPurchaseCost = reqItem.UnitCost;
                material.CurrentStock = Math.Round(material.CurrentStock + reqItem.Quantity, 4, MidpointRounding.AwayFromZero);
                InventoryKardexRecorder.RecordRawMaterial(
                    context, material, InventoryMovementType.PurchaseReceipt, reqItem.Quantity, reqItem.UnitCost,
                    orderNumber, $"Compra a {supplier.Name}", _currentUser?.UserName, purchaseDate, batchCode);

                subTotal += lineSub;
                totalTax += lineTax;

                items.Add(new PurchaseOrderItem
                {
                    RawMaterialId = material.Id,
                    Quantity = reqItem.Quantity,
                    UnitCost = reqItem.UnitCost,
                    IsTaxable = reqItem.IsTaxable,
                    TaxAmount = lineTax,
                    LineTotal = lineTotal
                });
                batches.Add(new RawMaterialBatch
                {
                    RawMaterialId = material.Id,
                    BatchCode = batchCode,
                    ReceivedDate = purchaseDate,
                    ExpirationDate = expDate,
                    InitialQuantity = reqItem.Quantity,
                    QuantityRemaining = reqItem.Quantity,
                    UnitCost = reqItem.UnitCost,
                    Notes = ProductionPlannerHelper.TruncateNotes(request.Notes, 300)
                });
            }

            decimal total = subTotal + totalTax;
            if (request.PaymentMethod == PaymentMethod.Credit)
            {
                supplier.CurrentBalance += total;
            }

            order = new PurchaseOrder
            {
                OrderNumber = orderNumber,
                SupplierId = supplier.Id,
                PurchaseDate = purchaseDate,
                Status = PurchaseOrderStatus.Received,
                PaymentMethod = request.PaymentMethod,
                SubTotal = subTotal,
                TaxAmount = totalTax,
                Total = total,
                AmountPaid = request.PaymentMethod == PaymentMethod.Credit ? 0m : total,
                Notes = ProductionPlannerHelper.TruncateNotes(request.Notes, 300),
                Items = items
            };

            context.PurchaseOrders.Add(order);
            foreach (var b in batches)
            {
                b.PurchaseOrder = order;
                context.RawMaterialBatches.Add(b);
            }
            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        finally
        {
            InventoryConcurrencyGate.RawMaterialGate.Release();
        }

        if (order.Total > 0m)
        {
            await _accounting.PostPurchaseReceiptAsync(order, _currentUser?.UserId);
        }
        return (await GetByIdAsync(order.Id))!;
    }
}

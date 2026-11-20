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

    public PurchaseOrderService(
        IDbContextFactory<AppDbContext> contextFactory,
        ISystemSettingService settings,
        IManufacturingAccountingService accounting)
    {
        _contextFactory = contextFactory;
        _settings = settings;
        _accounting = accounting;
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

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            using var tx = await context.Database.BeginTransactionAsync();
            var supplier = await context.Suppliers.FindAsync(request.SupplierId)
                ?? throw new InvalidOperationException("Suplidor no encontrado.");

            var materialIds = request.Items.Select(i => i.RawMaterialId).Distinct().OrderBy(id => id).ToList();
            var materials = await context.RawMaterials
                .Where(m => materialIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            decimal subTotal = 0m;
            decimal totalTax = 0m;
            var items = new List<PurchaseOrderItem>();

            foreach (var reqItem in request.Items.OrderBy(i => i.RawMaterialId))
            {
                if (reqItem.Quantity <= 0m || reqItem.UnitCost < 0m)
                    throw new ArgumentException("La cantidad debe ser mayor a cero y el costo no puede ser negativo.");

                if (!materials.TryGetValue(reqItem.RawMaterialId, out var material))
                    throw new InvalidOperationException($"Materia prima ID {reqItem.RawMaterialId} no encontrada.");

                decimal lineSub = Math.Round(reqItem.Quantity * reqItem.UnitCost, 2, MidpointRounding.AwayFromZero);
                decimal lineTax = reqItem.IsTaxable ? Math.Round(lineSub * taxRate, 2, MidpointRounding.AwayFromZero) : 0m;
                decimal lineTotal = lineSub + lineTax;

                material.AverageCost = ProductionCostCalculator.CalculateWeightedAverageCost(
                    material.CurrentStock, material.AverageCost, reqItem.Quantity, reqItem.UnitCost);
                material.LastPurchaseCost = reqItem.UnitCost;
                material.CurrentStock = Math.Round(material.CurrentStock + reqItem.Quantity, 4, MidpointRounding.AwayFromZero);

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
            }

            decimal total = subTotal + totalTax;
            if (request.PaymentMethod == PaymentMethod.Credit)
            {
                supplier.CurrentBalance += total;
            }

            order = new PurchaseOrder
            {
                OrderNumber = $"OC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
                SupplierId = supplier.Id,
                PurchaseDate = request.PurchaseDate ?? DateTime.UtcNow,
                Status = PurchaseOrderStatus.Received,
                PaymentMethod = request.PaymentMethod,
                SubTotal = subTotal,
                TaxAmount = totalTax,
                Total = total,
                AmountPaid = request.PaymentMethod == PaymentMethod.Credit ? 0m : total,
                Notes = request.Notes.Trim(),
                Items = items
            };

            context.PurchaseOrders.Add(order);
            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        if (order.Total > 0m)
        {
            await _accounting.PostPurchaseReceiptAsync(order);
        }
        return (await GetByIdAsync(order.Id))!;
    }
}

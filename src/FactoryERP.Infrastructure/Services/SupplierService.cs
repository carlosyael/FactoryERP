using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class SupplierService : ISupplierService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IManufacturingAccountingService _accounting;

    public SupplierService(IDbContextFactory<AppDbContext> contextFactory, IManufacturingAccountingService accounting)
    {
        _contextFactory = contextFactory;
        _accounting = accounting;
    }

    public async Task<List<Supplier>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Suppliers.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Supplier> CreateAsync(Supplier supplier)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        supplier.Name = supplier.Name.Trim();
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier;
    }

    public async Task UpdateAsync(Supplier supplier)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Suppliers.FindAsync(supplier.Id)
            ?? throw new InvalidOperationException("Suplidor no encontrado.");

        existing.Name = supplier.Name.Trim();
        existing.Rnc = supplier.Rnc.Trim();
        existing.Phone = supplier.Phone.Trim();
        existing.Email = supplier.Email.Trim();
        existing.Address = supplier.Address.Trim();
        existing.ContactPerson = supplier.ContactPerson.Trim();
        existing.IsActive = supplier.IsActive;

        await context.SaveChangesAsync();
    }

    public async Task<SupplierPayment> RegisterPaymentAsync(
        int supplierId, decimal amount, PaymentMethod method, string reference, string notes = "")
    {
        if (amount <= 0m) throw new ArgumentException("El monto del pago debe ser mayor a cero.", nameof(amount));
        if (method == PaymentMethod.Credit) throw new InvalidOperationException("Un abono no puede registrarse a crédito.");

        SupplierPayment payment;
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            using var tx = await context.Database.BeginTransactionAsync();
            var supplier = await context.Suppliers.FindAsync(supplierId)
                ?? throw new InvalidOperationException("Suplidor no encontrado.");

            decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (rounded > supplier.CurrentBalance)
                throw new InvalidOperationException($"El monto ({rounded:N2}) supera la deuda pendiente ({supplier.CurrentBalance:N2}).");

            supplier.CurrentBalance -= rounded;
            payment = new SupplierPayment
            {
                SupplierId = supplierId,
                PaymentDate = DateTime.UtcNow,
                Amount = rounded,
                PaymentMethod = method,
                Reference = string.IsNullOrWhiteSpace(reference) ? $"PAG-{DateTime.UtcNow:yyyyMMddHHmmss}" : reference.Trim(),
                Notes = notes.Trim()
            };

            context.SupplierPayments.Add(payment);
            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await _accounting.PostSupplierPaymentAsync(payment);
        return payment;
    }

    public async Task<List<SupplierPayment>> GetPaymentsAsync(int supplierId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SupplierPayments
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }
}

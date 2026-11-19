using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IManufacturingAccountingService _accounting;

    public CustomerService(IDbContextFactory<AppDbContext> contextFactory, IManufacturingAccountingService accounting)
    {
        _contextFactory = contextFactory;
        _accounting = accounting;
    }

    public async Task<List<Customer>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Customers.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        customer.Name = customer.Name.Trim();
        customer.RncCedula = customer.RncCedula.Trim();
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    public async Task UpdateAsync(Customer customer)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Customers.FindAsync(customer.Id)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        existing.Name = customer.Name.Trim();
        existing.RncCedula = customer.RncCedula.Trim();
        existing.CustomerType = customer.CustomerType;
        existing.Phone = customer.Phone.Trim();
        existing.WhatsApp = customer.WhatsApp.Trim();
        existing.Email = customer.Email.Trim();
        existing.Address = customer.Address.Trim();
        existing.CreditLimit = Math.Max(0m, customer.CreditLimit);
        existing.CreditDays = Math.Clamp(customer.CreditDays, 0, 365);
        existing.DefaultDiscountPercent = Math.Clamp(customer.DefaultDiscountPercent, 0m, 100m);
        existing.IsActive = customer.IsActive;

        await context.SaveChangesAsync();
    }

    public async Task<CustomerPayment> RegisterPaymentAsync(
        int customerId, decimal amount, PaymentMethod method, string reference, string notes = "")
    {
        if (amount <= 0m) throw new ArgumentException("El monto del abono debe ser mayor a cero.", nameof(amount));
        if (method == PaymentMethod.Credit) throw new InvalidOperationException("Un cobro no puede registrarse a crédito.");

        CustomerPayment payment;
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            using var tx = await context.Database.BeginTransactionAsync();
            var customer = await context.Customers.FindAsync(customerId)
                ?? throw new InvalidOperationException("Cliente no encontrado.");

            decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (rounded > customer.CurrentBalance)
                throw new InvalidOperationException($"El abono ({rounded:N2}) supera el balance pendiente ({customer.CurrentBalance:N2}).");

            customer.CurrentBalance -= rounded;
            payment = new CustomerPayment
            {
                CustomerId = customerId,
                PaymentDate = DateTime.UtcNow,
                Amount = rounded,
                PaymentMethod = method,
                Reference = string.IsNullOrWhiteSpace(reference) ? $"COB-{DateTime.UtcNow:yyyyMMddHHmmss}" : reference.Trim(),
                Notes = notes.Trim()
            };

            context.CustomerPayments.Add(payment);
            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        await _accounting.PostCustomerPaymentAsync(payment);
        return payment;
    }

    public async Task<List<CustomerPayment>> GetPaymentsAsync(int customerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.CustomerPayments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }
}

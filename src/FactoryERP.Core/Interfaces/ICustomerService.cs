using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Interfaces;

public interface ICustomerService
{
    Task<List<Customer>> GetAllAsync(bool includeInactive = false);
    Task<Customer?> GetByIdAsync(int id);
    Task<Customer> CreateAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task<CustomerPayment> RegisterPaymentAsync(int customerId, decimal amount, PaymentMethod method, string reference, string notes = "");
    Task<List<CustomerPayment>> GetPaymentsAsync(int customerId);
}

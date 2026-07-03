using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface ICustomerService
{
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    bool IsSystemCustomer(Customer customer);

    /// <summary>Returns false when the customer is a protected system account (e.g. walk-in guest).</summary>
    Task<bool> CanDeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Returns false when the customer is a protected system account (e.g. walk-in guest).</summary>
    Task<bool> CanModifyCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}

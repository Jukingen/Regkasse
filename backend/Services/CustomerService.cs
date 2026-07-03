using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;

    public CustomerService(AppDbContext context)
    {
        _context = context;
    }

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public bool IsSystemCustomer(Customer customer) =>
        customer.IsSystem || customer.Id == WalkInCustomerConstants.GuestCustomerId;

    public async Task<bool> CanDeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        if (customer == null)
            return false;
        if (IsSystemCustomer(customer))
            return false;
        return true;
    }

    public async Task<bool> CanModifyCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        if (customer == null)
            return false;
        if (IsSystemCustomer(customer))
            return false;
        return true;
    }
}

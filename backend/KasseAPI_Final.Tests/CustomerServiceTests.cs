using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CustomerServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CustomerService_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task CanDeleteCustomerAsync_SystemCustomer_ReturnsFalse()
    {
        await using var db = CreateDb();
        db.Customers.Add(new Customer
        {
            Id = WalkInCustomerConstants.GuestCustomerId,
            Name = "Walk-in Customer",
            CustomerNumber = "GUEST-000",
            Email = "walkin@system.local",
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new CustomerService(db);
        var canDelete = await service.CanDeleteCustomerAsync(WalkInCustomerConstants.GuestCustomerId);

        Assert.False(canDelete);
    }

    [Fact]
    public async Task CanDeleteCustomerAsync_RegularCustomer_ReturnsTrue()
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = id,
            Name = "Regular",
            CustomerNumber = "C-1",
            Email = "a@example.com",
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new CustomerService(db);
        Assert.True(await service.CanDeleteCustomerAsync(id));
    }
}

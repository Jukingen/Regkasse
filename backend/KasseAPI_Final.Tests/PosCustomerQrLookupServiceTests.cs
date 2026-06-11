using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosCustomerQrLookupServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosCustomerQr_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task ResolveByQrData_CustomerPrefixGuid_FindsCustomer()
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        ctx.Customers.Add(new Customer
        {
            Id = id,
            Name = "Max Mustermann",
            CustomerNumber = "C-100",
            Email = "max@example.com",
            Phone = "+431234",
            LoyaltyPoints = 42,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = new PosCustomerQrLookupService(ctx);
        var dto = await svc.ResolveByQrDataAsync($"customer:{id}");

        Assert.NotNull(dto);
        Assert.Equal("Max Mustermann", dto!.Name);
        Assert.Equal(42, dto.LoyaltyPoints);
    }

    [Fact]
    public async Task ResolveByQrData_RkCanonicalPayload_FindsCustomer()
    {
        await using var ctx = CreateContext();
        ctx.Customers.Add(new Customer
        {
            Name = "Anna",
            CustomerNumber = "VIP-9",
            Email = "anna@example.com",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = new PosCustomerQrLookupService(ctx);
        var dto = await svc.ResolveByQrDataAsync("RK:C:VIP-9");

        Assert.NotNull(dto);
        Assert.Equal("Anna", dto!.Name);
    }
}

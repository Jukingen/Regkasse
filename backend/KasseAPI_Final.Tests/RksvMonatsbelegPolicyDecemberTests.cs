using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvMonatsbelegPolicyDecemberTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MonPolDec_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedRegisterAsync(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.Customers.Add(new Customer
        {
            Id = WalkInCustomerConstants.GuestCustomerId,
            Name = "Gast",
            Email = "gast@test",
            Phone = "0",
            IsActive = true
        });
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return regId;
    }

    [Fact]
    public async Task HasMonatsbeleg_December_TrueWhenJahresbelegExistsForYear()
    {
        await using var ctx = CreateContext();
        var regId = await SeedRegisterAsync(ctx);
        const int year = 2033;
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "u",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "s",
            TseTimestamp = DateTime.UtcNow,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "J-1",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Jahresbeleg,
            RksvSpecialReceiptYear = year,
            RksvSpecialReceiptMonth = null,
        });
        await ctx.SaveChangesAsync();

        var pol = new RksvMonatsbelegPolicy(ctx, Options.Create(new TseOptions { TseMode = "Device" }));
        var has = await pol.HasMonatsbelegForRegisterMonthAsync(regId, year, 12, CancellationToken.None);
        Assert.True(has);
    }

    [Fact]
    public async Task HasMonatsbeleg_December_TrueWhenLegacyDecemberMonatsbelegExists()
    {
        await using var ctx = CreateContext();
        var regId = await SeedRegisterAsync(ctx);
        const int year = 2034;
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "u",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "s",
            TseTimestamp = DateTime.UtcNow,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "M12-1",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
            RksvSpecialReceiptYear = year,
            RksvSpecialReceiptMonth = 12,
        });
        await ctx.SaveChangesAsync();

        var pol = new RksvMonatsbelegPolicy(ctx, Options.Create(new TseOptions { TseMode = "Device" }));
        Assert.True(await pol.HasMonatsbelegForRegisterMonthAsync(regId, year, 12, CancellationToken.None));
    }

    [Fact]
    public async Task HasMonatsbeleg_December_FalseWhenJahresbelegExistsForDifferentYear()
    {
        await using var ctx = CreateContext();
        var regId = await SeedRegisterAsync(ctx);
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "u",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "s",
            TseTimestamp = DateTime.UtcNow,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "J-oth",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Jahresbeleg,
            RksvSpecialReceiptYear = 2020,
            RksvSpecialReceiptMonth = null,
        });
        await ctx.SaveChangesAsync();

        var pol = new RksvMonatsbelegPolicy(ctx, Options.Create(new TseOptions { TseMode = "Device" }));
        Assert.False(await pol.HasMonatsbelegForRegisterMonthAsync(regId, 2035, 12, CancellationToken.None));
    }
}

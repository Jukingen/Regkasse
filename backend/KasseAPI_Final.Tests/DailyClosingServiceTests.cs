using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DailyClosingServiceTests
{
    [Fact]
    public async Task GenerateClosingSummaryAsync_ExcludesStornoFromSalesTotals_AndListsStornos()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 10);
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosing_{Guid.NewGuid():N}")
                .Options,
            // Customer is tenant-scoped; run under the seeded tenant so the customer is visible.
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-test", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "REG1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = noonUtc,
            Status = RegisterStatus.Open,
            CreatedAt = noonUtc,
        });
        ctx.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "C",
            CustomerNumber = "00000001",
            TaxNumber = "ATU12345678",
            CreatedAt = noonUtc,
        });
        await ctx.SaveChangesAsync();

        var cust = await ctx.Customers.AsNoTracking().FirstAsync();
        var sale = new PaymentDetails
        {
            CustomerId = cust.Id,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "x",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-SALE",
            CreatedAt = noonUtc,
        };
        var storno = new PaymentDetails
        {
            CustomerId = cust.Id,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = -10m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "y",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-STO",
            IsStorno = true,
            StornoReason = StornoReason.KundeStorniert,
            OriginalReceiptId = Guid.NewGuid(),
            CreatedAt = noonUtc.AddMinutes(1),
        };
        ctx.PaymentDetails.AddRange(sale, storno);
        await ctx.SaveChangesAsync();

        var sut = new DailyClosingService(ctx);
        var dto = await sut.GenerateClosingSummaryAsync(tenantId, regId, day);

        Assert.Equal(10m, dto.TotalSales);
        Assert.Equal(1, dto.ReceiptCount);
        Assert.Equal(10m, dto.TotalCash);
        Assert.Single(dto.Stornos);
        Assert.Equal(-10m, dto.Stornos[0].TotalAmount);
        Assert.Equal(1, dto.StornoRowCount);
        Assert.Equal(-10m, dto.StornoTotalAmount);
    }
}

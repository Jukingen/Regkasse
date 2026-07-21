using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Integrity sequence checks: duplicate receipt numbers across PaymentDetails + Receipts; monotonic RKSV-style sequence per register/day.
/// </summary>
public class IntegrityCheckServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"Integrity_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static IntegrityCheckService CreateService(AppDbContext ctx) =>
        new(ctx, NullLogger<IntegrityCheckService>.Instance);

    private static async Task<(Guid regId, Guid customerId)> SeedCashRegisterAndCustomer(AppDbContext ctx)
    {
        var regId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "R1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "C",
            Email = "c@test.com",
            Phone = "1",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return (regId, customerId);
    }

    private static PaymentDetails CreatePayment(Guid customerId, Guid regId, string receiptNumber, DateTime createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "cash1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "sig",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = receiptNumber,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
            CreatedAt = createdAtUtc,
            IsActive = true
        };

    [Fact]
    public async Task DuplicateReceiptNumbers_TwoPaymentsSameNumber_AreDetected()
    {
        using var ctx = CreateContext();
        var (regId, customerId) = await SeedCashRegisterAndCustomer(ctx);
        var day = new DateTime(2024, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        const string sharedNr = "AT-R1-20240310-1";
        ctx.PaymentDetails.Add(CreatePayment(customerId, regId, sharedNr, day));
        ctx.PaymentDetails.Add(CreatePayment(customerId, regId, sharedNr, day.AddMinutes(1)));
        await ctx.SaveChangesAsync();

        var report = await CreateService(ctx).GetReportAsync(new DateTime(2024, 3, 10), new DateTime(2024, 3, 11), includeDetails: true);

        Assert.Equal(1, report.SequenceIssues.DuplicateReceiptNumberCount);
        Assert.NotNull(report.SequenceIssues.DuplicateReceiptNumbers);
        Assert.Contains(sharedNr, report.SequenceIssues.DuplicateReceiptNumbers);
        Assert.Equal(0, report.SequenceIssues.NonMonotonicSequenceCount);
    }

    [Fact]
    public async Task MonotonicSequence_StrictlyIncreasing_NoDuplicates_Passes()
    {
        using var ctx = CreateContext();
        var (regId, customerId) = await SeedCashRegisterAndCustomer(ctx);
        var day = new DateTime(2024, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        ctx.PaymentDetails.Add(CreatePayment(customerId, regId, "AT-R1-20240310-1", day));
        ctx.PaymentDetails.Add(CreatePayment(customerId, regId, "AT-R1-20240310-2", day.AddMinutes(1)));
        ctx.PaymentDetails.Add(CreatePayment(customerId, regId, "AT-R1-20240310-3", day.AddMinutes(2)));
        await ctx.SaveChangesAsync();

        var report = await CreateService(ctx).GetReportAsync(new DateTime(2024, 3, 10), new DateTime(2024, 3, 11));

        Assert.Equal(0, report.SequenceIssues.DuplicateReceiptNumberCount);
        Assert.Equal(0, report.SequenceIssues.NonMonotonicSequenceCount);
    }

    [Fact]
    public async Task MalformedReceiptNumbers_NoFalseDuplicateOrMonotonicFlags()
    {
        using var ctx = CreateContext();
        var (regId, customerId) = await SeedCashRegisterAndCustomer(ctx);
        var day = new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        ctx.PaymentDetails.Add(CreatePayment(customerId, regId, "not-a-valid-beleg-nr-format", day));
        await ctx.SaveChangesAsync();

        var report = await CreateService(ctx).GetReportAsync(new DateTime(2024, 5, 1), new DateTime(2024, 5, 2));

        Assert.Equal(0, report.SequenceIssues.DuplicateReceiptNumberCount);
        Assert.Equal(0, report.SequenceIssues.NonMonotonicSequenceCount);
    }

    /// <summary>
    /// Multiset must count every occurrence from PaymentDetails and Receipts before duplicate grouping —
    /// three copies of the same string must still yield one duplicate key (regression guard).
    /// </summary>
    [Fact]
    public async Task DuplicateReceiptNumbers_ThreeOccurrencesSameString_OneDuplicateKey()
    {
        using var ctx = CreateContext();
        var (regId, customerId) = await SeedCashRegisterAndCustomer(ctx);
        var day = new DateTime(2024, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        const string sharedNr = "AT-R1-20240701-9";
        var p1 = CreatePayment(customerId, regId, sharedNr, day);
        var p2 = CreatePayment(customerId, regId, sharedNr, day.AddMinutes(1));
        ctx.PaymentDetails.AddRange(p1, p2);
        ctx.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = p1.Id,
            ReceiptNumber = sharedNr,
            IssuedAt = day,
            CashRegisterId = regId,
            SubTotal = 8m,
            TaxTotal = 1m,
            GrandTotal = 9m,
        });
        await ctx.SaveChangesAsync();

        var report = await CreateService(ctx).GetReportAsync(new DateTime(2024, 7, 1), new DateTime(2024, 7, 2), includeDetails: true);

        Assert.Equal(1, report.SequenceIssues.DuplicateReceiptNumberCount);
        Assert.NotNull(report.SequenceIssues.DuplicateReceiptNumbers);
        Assert.Contains(sharedNr, report.SequenceIssues.DuplicateReceiptNumbers);
    }
}

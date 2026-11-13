using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Preorder;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PreorderServiceTests
{
    [Fact]
    public async Task Create_ThenCollect_DoesNotRequireNewPayment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var sut = CreateSut(db, tenantId);
        var payment = SeedPayment(db);

        await sut.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true, Items = [] },
            "cashier1");

        var created = await sut.GetByReceiptNumberAsync(payment.ReceiptNumber);
        Assert.NotNull(created);
        Assert.Equal(PreorderStatuses.Pending, created!.Status);
        Assert.Equal(payment.Id, created.SourcePaymentId);

        var collected = await sut.SetOperationalStatusAsync(created.Id, PreorderStatuses.Collected);
        Assert.Equal(PreorderStatuses.Collected, collected!.Status);
        Assert.NotNull(collected.CollectedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(created.PreorderNumber));
        Assert.StartsWith("BS", created.PreorderNumber);
        Assert.Equal(15m, created.PaidAmount);
        Assert.Equal(0m, created.RemainingAmount);
    }

    [Fact]
    public async Task Collect_WhenRemainingOpen_Throws()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var sut = CreateSut(db, tenantId);
        var payment = SeedPayment(db);

        await sut.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true, PreorderRemainingAmount = 4.20m },
            "cashier1");

        var created = await sut.GetByReceiptNumberAsync(payment.ReceiptNumber);
        Assert.Equal(4.20m, created!.RemainingAmount);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetOperationalStatusAsync(created.Id, PreorderStatuses.Collected));
        Assert.Equal("PREORDER_BALANCE_OPEN", ex.Message);
    }

    [Fact]
    public async Task GetByReceiptNumber_FindsBesorgerzettelNumber()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var sut = CreateSut(db, tenantId);
        var payment = SeedPayment(db);
        await sut.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true },
            "cashier1");

        var created = await sut.GetByReceiptNumberAsync(payment.ReceiptNumber);
        var byNumber = await sut.GetByReceiptNumberAsync(created!.PreorderNumber!);
        Assert.Equal(created.Id, byNumber!.Id);
    }

    [Fact]
    public async Task ApplyBalance_ThenCollect()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var sut = CreateSut(db, tenantId);
        var payment = SeedPayment(db);
        await sut.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true, PreorderRemainingAmount = 5m },
            "cashier1");
        var created = await sut.GetByReceiptNumberAsync(payment.ReceiptNumber);

        var balance = SeedPayment(db, "AT-kasse-03-dev-20260908-2", 5m);
        var guard = await sut.ValidateBalancePaymentAsync(created!.Id, 5m);
        Assert.True(guard.Ok);
        await sut.ApplyBalancePaymentAsync(created.Id, balance);

        var after = await sut.GetByIdAsync(created.Id);
        Assert.Equal(0m, after!.RemainingAmount);
        var collected = await sut.SetOperationalStatusAsync(created.Id, PreorderStatuses.Collected);
        Assert.Equal(PreorderStatuses.Collected, collected!.Status);
    }

    [Fact]
    public async Task SetCancelled_WithoutFiscalFlag_Throws()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var sut = CreateSut(db, tenantId);
        var payment = SeedPayment(db);
        await sut.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true },
            "cashier1");
        var created = await sut.GetByReceiptNumberAsync(payment.ReceiptNumber);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetOperationalStatusAsync(created!.Id, PreorderStatuses.Cancelled));
        Assert.Equal("PREORDER_CANCEL_REQUIRES_STORNO", ex.Message);
    }

    [Fact]
    public async Task CrossTenant_Lookup_ReturnsNull()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDb(tenantA);
        var writer = CreateSut(db, tenantA);
        var payment = SeedPayment(db);
        await writer.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true },
            "cashier1");

        var reader = CreateSut(db, tenantB);
        Assert.Null(await reader.GetByReceiptNumberAsync(payment.ReceiptNumber));
    }

    [Fact]
    public async Task StornoHook_MarksCancelled()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        var sut = CreateSut(db, tenantId);
        var payment = SeedPayment(db);
        await sut.TryCreateFromSuccessfulPaymentAsync(
            payment,
            new CreatePaymentRequest { IsPreorder = true },
            "cashier1");

        await sut.MarkCancelledForPaymentAsync(payment.Id);
        var row = await sut.GetByReceiptNumberAsync(payment.ReceiptNumber);
        Assert.Equal(PreorderStatuses.Cancelled, row!.Status);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("preorder-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static PreorderService CreateSut(AppDbContext db, Guid tenantId) =>
        new(db, TenantTestDoubles.SettingsResolverReturning(tenantId), NullLogger<PreorderService>.Instance);

    private static PaymentDetails SeedPayment(
        AppDbContext db,
        string receiptNumber = "AT-kasse-03-dev-20260908-1",
        decimal totalAmount = 15m)
    {
        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Max",
            TableNumber = 1,
            CashierId = "cashier1",
            TotalAmount = totalAmount,
            TaxAmount = 1.36m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU00000000",
            CashRegisterId = Guid.NewGuid(),
            TseSignature = "header.payload.sig",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = receiptNumber
        };
        db.PaymentDetails.Add(payment);
        db.SaveChanges();
        return payment;
    }
}

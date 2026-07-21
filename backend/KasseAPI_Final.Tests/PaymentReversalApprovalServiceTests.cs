using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentReversalApprovalServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rev_approval_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static IOptionsMonitor<T> MonitorOf<T>(T value) where T : class
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(x => x.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static PaymentReversalApprovalService CreateSut(
        AppDbContext db,
        PaymentReversalApprovalOptions? options = null,
        Mock<IPaymentReversalApprovalEmailService>? emailMock = null)
    {
        var opts = options ?? new PaymentReversalApprovalOptions();
        var tenantResolver = new Mock<ISettingsTenantResolver>();
        tenantResolver.Setup(x => x.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantId);

        emailMock ??= new Mock<IPaymentReversalApprovalEmailService>();
        emailMock.Setup(x => x.TrySendApprovalTokenAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<PaymentDetails>(),
                It.IsAny<PaymentReversalOperation>(),
                It.IsAny<decimal?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var workflow = new ApprovalWorkflowService(
            db,
            tenantResolver.Object,
            MonitorOf(opts));

        return new PaymentReversalApprovalService(
            db,
            tenantResolver.Object,
            emailMock.Object,
            workflow,
            MonitorOf(opts),
            NullLogger<PaymentReversalApprovalService>.Instance);
    }

    private static PaymentDetails HighValuePayment(decimal total = 150m) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test",
            CashRegisterId = Guid.NewGuid(),
            TotalAmount = total,
            TaxAmount = 0m,
            PaymentMethod = PaymentMethod.Cash,
            CashierId = "cashier",
            TableNumber = 1,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

    [Fact]
    public async Task AssessPolicyAsync_high_amount_cancel_requires_approval()
    {
        var svc = CreateSut(CreateContext());
        var policy = await svc.AssessPolicyAsync(HighValuePayment(), PaymentReversalOperation.Cancel, null);

        Assert.True(policy.RequiresApproval);
        Assert.Contains("HIGH_AMOUNT", policy.RiskFactors);
    }

    [Fact]
    public async Task AssessPolicyAsync_small_cancel_does_not_require_approval()
    {
        var svc = CreateSut(CreateContext());
        var policy = await svc.AssessPolicyAsync(HighValuePayment(50m), PaymentReversalOperation.Cancel, null);

        Assert.False(policy.RequiresApproval);
        Assert.Empty(policy.RiskFactors);
    }

    [Fact]
    public async Task AssessPolicyAsync_high_refund_share_requires_approval()
    {
        var svc = CreateSut(CreateContext());
        var payment = HighValuePayment(100m);
        var policy = await svc.AssessPolicyAsync(payment, PaymentReversalOperation.Refund, 60m);

        Assert.True(policy.RequiresApproval);
        Assert.Contains("HIGH_REFUND_SHARE", policy.RiskFactors);
    }

    [Fact]
    public async Task EnforceApprovalAsync_without_token_creates_pending_request()
    {
        await using var db = CreateContext();
        var svc = CreateSut(db);
        var payment = HighValuePayment();

        var outcome = await svc.EnforceApprovalAsync(
            payment,
            PaymentReversalOperation.Cancel,
            null,
            "[CustomerRequest] customer changed mind",
            (int)CancellationReasonCode.CustomerRequest,
            "requester",
            null,
            "idem-1");

        Assert.Equal(PaymentReversalApprovalGateResult.ApprovalRequired, outcome.Result);
        Assert.NotNull(outcome.ApprovalRequestId);
        Assert.NotNull(outcome.ExpiresAtUtc);
        Assert.True(outcome.NotificationSent);

        var row = await db.PaymentReversalApprovals.SingleAsync();
        Assert.Equal(PaymentReversalApprovalStatus.Pending, row.Status);
        Assert.Equal(payment.Id, row.PaymentId);
    }

    [Fact]
    public async Task EnforceApprovalAsync_valid_token_consumes_pending_request()
    {
        await using var db = CreateContext();
        var emailMock = new Mock<IPaymentReversalApprovalEmailService>();
        string? capturedToken = null;
        emailMock.Setup(x => x.TrySendApprovalTokenAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<PaymentDetails>(),
                It.IsAny<PaymentReversalOperation>(),
                It.IsAny<decimal?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, PaymentDetails, PaymentReversalOperation, decimal?, DateTime, CancellationToken>(
                (_, token, _, _, _, _, _) => capturedToken = token)
            .ReturnsAsync(1);

        var svc = CreateSut(db, emailMock: emailMock);
        var payment = HighValuePayment();

        await svc.EnforceApprovalAsync(
            payment,
            PaymentReversalOperation.Cancel,
            null,
            "[Other] test",
            (int)CancellationReasonCode.Other,
            "requester",
            null,
            null);

        Assert.False(string.IsNullOrWhiteSpace(capturedToken));

        var approved = await svc.EnforceApprovalAsync(
            payment,
            PaymentReversalOperation.Cancel,
            null,
            "[Other] test",
            (int)CancellationReasonCode.Other,
            "requester",
            capturedToken,
            null);

        Assert.Equal(PaymentReversalApprovalGateResult.Approved, approved.Result);

        var row = await db.PaymentReversalApprovals.SingleAsync();
        Assert.Equal(PaymentReversalApprovalStatus.Consumed, row.Status);
        Assert.NotNull(row.ConsumedAtUtc);
    }

    [Fact]
    public async Task EnforceApprovalAsync_invalid_token_returns_invalid()
    {
        await using var db = CreateContext();
        var svc = CreateSut(db);
        var payment = HighValuePayment();

        await svc.EnforceApprovalAsync(
            payment,
            PaymentReversalOperation.Cancel,
            null,
            "[Other] test",
            (int)CancellationReasonCode.Other,
            "requester",
            null,
            null);

        var outcome = await svc.EnforceApprovalAsync(
            payment,
            PaymentReversalOperation.Cancel,
            null,
            "[Other] test",
            (int)CancellationReasonCode.Other,
            "requester",
            "000000",
            null);

        Assert.Equal(PaymentReversalApprovalGateResult.InvalidToken, outcome.Result);
    }

    [Fact]
    public void PaymentReversalReasonMapper_maps_cancellation_codes()
    {
        Assert.Equal(StornoReason.KundeStorniert,
            PaymentReversalReasonMapper.ToStornoReason(CancellationReasonCode.CustomerRequest));
        Assert.StartsWith("[CustomerRequest]", PaymentReversalReasonMapper.FormatCancellationReason(
            CancellationReasonCode.CustomerRequest,
            "changed mind"));
    }
}

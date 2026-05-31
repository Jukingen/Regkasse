using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ApprovalWorkflowServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"approval_wf_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IOptionsMonitor<T> MonitorOf<T>(T value) where T : class
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(x => x.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static ApprovalWorkflowService CreateSut(
        AppDbContext db,
        PaymentReversalApprovalOptions? options = null)
    {
        var tenantResolver = new Mock<ISettingsTenantResolver>();
        tenantResolver.Setup(x => x.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TenantId);
        return new ApprovalWorkflowService(
            db,
            tenantResolver.Object,
            MonitorOf(options ?? new PaymentReversalApprovalOptions()));
    }

    private static PaymentDetails SamplePayment(
        decimal total = 50m,
        DateTime? createdAt = null,
        Guid? cashRegisterId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test",
            CashRegisterId = cashRegisterId ?? Guid.NewGuid(),
            TotalAmount = total,
            TaxAmount = 0m,
            PaymentMethod = PaymentMethod.Cash,
            CashierId = "cashier-1",
            TableNumber = 1,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            IsActive = true,
        };

    [Fact]
    public async Task CheckApprovalRequirementAsync_amount_over_threshold_requires_approval()
    {
        await using var db = CreateContext();
        var svc = CreateSut(db);
        var payment = SamplePayment(total: 150m);

        var result = await svc.CheckApprovalRequirementAsync(
            payment,
            PaymentReversalOperation.Cancel,
            150m,
            "user-1");

        Assert.True(result.RequiresApproval);
        Assert.Contains("HIGH_AMOUNT", result.RiskFactors);
        Assert.Contains("Betrag über 100€", result.Reasons);
    }

    [Fact]
    public async Task CheckApprovalRequirementAsync_payment_older_than_24h_requires_approval()
    {
        await using var db = CreateContext();
        var svc = CreateSut(db);
        var payment = SamplePayment(total: 10m, createdAt: DateTime.UtcNow.AddHours(-30));

        var result = await svc.CheckApprovalRequirementAsync(
            payment,
            PaymentReversalOperation.Cancel,
            10m,
            "user-1");

        Assert.True(result.RequiresApproval);
        Assert.Contains("PAYMENT_AGE", result.RiskFactors);
        Assert.Equal("Zahlung ist älter als 24 Stunden", result.Reasons[0]);
    }

    [Fact]
    public async Task CheckApprovalRequirementAsync_frequent_stornos_require_approval()
    {
        await using var db = CreateContext();
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = TenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        for (var i = 0; i < 5; i++)
        {
            db.PaymentDetails.Add(new PaymentDetails
            {
                Id = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                CustomerName = "C",
                CashRegisterId = regId,
                TotalAmount = -5m,
                TaxAmount = 0m,
                PaymentMethod = PaymentMethod.Cash,
                CashierId = "cashier-1",
                CreatedBy = "cashier-1",
                TableNumber = 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                IsStorno = true,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();

        var svc = CreateSut(db);
        var payment = SamplePayment(total: 10m, cashRegisterId: regId);

        var result = await svc.CheckApprovalRequirementAsync(
            payment,
            PaymentReversalOperation.Cancel,
            10m,
            "cashier-1");

        Assert.True(result.RequiresApproval);
        Assert.Contains("HIGH_STORNO_FREQUENCY", result.RiskFactors);
        Assert.Contains(result.Reasons[0], "Zu viele Stornos in kurzer Zeit");
    }

    [Fact]
    public async Task CheckApprovalRequirementAsync_low_risk_returns_false()
    {
        await using var db = CreateContext();
        var svc = CreateSut(db);
        var payment = SamplePayment(total: 20m);

        var result = await svc.CheckApprovalRequirementAsync(
            payment,
            PaymentReversalOperation.Cancel,
            20m,
            "user-1");

        Assert.False(result.RequiresApproval);
        Assert.Empty(result.RiskFactors);
    }
}

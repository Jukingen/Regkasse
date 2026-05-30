using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SuspiciousTransactionDetectorTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"suspicious_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task DetectForTenantAsync_high_value_payment_creates_alert()
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
        var paymentId = Guid.NewGuid();
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            CashRegisterId = regId,
            TotalAmount = 600m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            CashierId = "cashier",
            TableNumber = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var alertMock = new Mock<ISuspiciousTransactionAlertService>();
        var detector = new SuspiciousTransactionDetector(
            db,
            alertMock.Object,
            Options.Create(new SuspiciousTransactionDetectionOptions
            {
                HighValueThresholdEur = 500m,
            }));

        await detector.DetectForTenantAsync(TenantId, CancellationToken.None);

        alertMock.Verify(
            x => x.TryPublishAlertAsync(
                It.Is<SuspiciousAlertDraft>(d =>
                    d.Type == SuspiciousAlertType.HighValue
                    && d.PaymentId == paymentId
                    && d.TenantId == TenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AlertService_skips_duplicate_dedup_key()
    {
        await using var db = CreateContext();
        db.SuspiciousTransactionAlerts.Add(new SuspiciousTransactionAlert
        {
            TenantId = TenantId,
            AlertType = SuspiciousAlertType.HighValue,
            Severity = SuspiciousAlertSeverity.High,
            Status = SuspiciousAlertStatus.Open,
            Message = "existing",
            DedupKey = "high_value_test",
            DetectedAtUtc = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var activityMock = new Mock<IActivityEventService>();
        var svc = new SuspiciousTransactionAlertService(
            db,
            activityMock.Object,
            Options.Create(new SuspiciousTransactionDetectionOptions()),
            NullLogger<SuspiciousTransactionAlertService>.Instance);

        await svc.TryPublishAlertAsync(
            new SuspiciousAlertDraft(
                TenantId,
                SuspiciousAlertType.HighValue,
                SuspiciousAlertSeverity.High,
                "duplicate",
                "action",
                "high_value_test"));

        activityMock.Verify(
            x => x.PublishAsync(It.IsAny<ActivityEventPublishRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Single(db.SuspiciousTransactionAlerts);
    }
}

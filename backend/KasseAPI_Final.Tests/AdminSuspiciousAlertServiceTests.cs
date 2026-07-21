using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminSuspiciousAlertServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin_suspicious_alerts_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task ListAsync_unreadOnly_returns_open_alerts_for_tenant()
    {
        await using var db = CreateContext();
        var openId = Guid.NewGuid();
        var ackId = Guid.NewGuid();
        db.SuspiciousTransactionAlerts.AddRange(
            new SuspiciousTransactionAlert
            {
                Id = openId,
                TenantId = TenantA,
                AlertType = SuspiciousAlertType.HighValue,
                Severity = SuspiciousAlertSeverity.High,
                Status = SuspiciousAlertStatus.Open,
                Message = "open",
                DedupKey = "k1",
                DetectedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            },
            new SuspiciousTransactionAlert
            {
                Id = ackId,
                TenantId = TenantA,
                AlertType = SuspiciousAlertType.MultipleStornos,
                Severity = SuspiciousAlertSeverity.Medium,
                Status = SuspiciousAlertStatus.Acknowledged,
                Message = "read",
                DedupKey = "k2",
                DetectedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
            },
            new SuspiciousTransactionAlert
            {
                Id = Guid.NewGuid(),
                TenantId = TenantB,
                AlertType = SuspiciousAlertType.HighValue,
                Severity = SuspiciousAlertSeverity.High,
                Status = SuspiciousAlertStatus.Open,
                Message = "other tenant",
                DedupKey = "k3",
                DetectedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var svc = new AdminSuspiciousAlertService(db);
        var unread = await svc.ListAsync(TenantA, unreadOnly: true);
        var all = await svc.ListAsync(TenantA, unreadOnly: false);

        Assert.Single(unread.Items);
        Assert.Equal(openId, unread.Items[0].Id);
        Assert.False(unread.Items[0].IsRead);

        Assert.Equal(2, all.Total);
    }

    [Fact]
    public async Task MarkAsReadAsync_sets_acknowledged_and_returns_false_for_other_tenant()
    {
        await using var db = CreateContext();
        var alertId = Guid.NewGuid();
        db.SuspiciousTransactionAlerts.Add(new SuspiciousTransactionAlert
        {
            Id = alertId,
            TenantId = TenantA,
            AlertType = SuspiciousAlertType.HighValue,
            Severity = SuspiciousAlertSeverity.High,
            Status = SuspiciousAlertStatus.Open,
            Message = "open",
            DedupKey = "k1",
            DetectedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = new AdminSuspiciousAlertService(db);

        var notFound = await svc.MarkAsReadAsync(TenantB, alertId, "manager1");
        Assert.False(notFound);

        var ok = await svc.MarkAsReadAsync(TenantA, alertId, "manager1");
        Assert.True(ok);

        var entity = await db.SuspiciousTransactionAlerts.FindAsync(alertId);
        Assert.Equal(SuspiciousAlertStatus.Acknowledged, entity!.Status);
        Assert.Equal("manager1", entity.UpdatedBy);
        Assert.NotNull(entity.UpdatedAt);

        var dto = AdminSuspiciousAlertService.Map(entity);
        Assert.True(dto.IsRead);
        Assert.NotNull(dto.ReadAtUtc);
    }

    [Fact]
    public async Task ListAsync_unreadOnly_collapses_duplicate_dedup_keys()
    {
        await using var db = CreateContext();
        var paymentId = Guid.NewGuid();
        var dedupKey = $"unusual_time_{paymentId:N}";
        db.SuspiciousTransactionAlerts.AddRange(
            new SuspiciousTransactionAlert
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                PaymentId = paymentId,
                AlertType = SuspiciousAlertType.UnusualTime,
                Severity = SuspiciousAlertSeverity.Medium,
                Status = SuspiciousAlertStatus.Open,
                Message = "older",
                DedupKey = dedupKey,
                DetectedAtUtc = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                IsActive = true,
            },
            new SuspiciousTransactionAlert
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                PaymentId = paymentId,
                AlertType = SuspiciousAlertType.UnusualTime,
                Severity = SuspiciousAlertSeverity.Medium,
                Status = SuspiciousAlertStatus.Open,
                Message = "newest",
                DedupKey = dedupKey,
                DetectedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var svc = new AdminSuspiciousAlertService(db);
        var unread = await svc.ListAsync(TenantA, unreadOnly: true);

        Assert.Single(unread.Items);
        Assert.Equal("newest", unread.Items[0].Message);
    }

    [Fact]
    public async Task MarkAsReadAsync_acknowledges_sibling_open_alerts_with_same_dedup_key()
    {
        await using var db = CreateContext();
        var dedupKey = "unusual_time_shared";
        var primaryId = Guid.NewGuid();
        var siblingId = Guid.NewGuid();
        db.SuspiciousTransactionAlerts.AddRange(
            new SuspiciousTransactionAlert
            {
                Id = primaryId,
                TenantId = TenantA,
                AlertType = SuspiciousAlertType.UnusualTime,
                Severity = SuspiciousAlertSeverity.Medium,
                Status = SuspiciousAlertStatus.Open,
                Message = "primary",
                DedupKey = dedupKey,
                DetectedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            },
            new SuspiciousTransactionAlert
            {
                Id = siblingId,
                TenantId = TenantA,
                AlertType = SuspiciousAlertType.UnusualTime,
                Severity = SuspiciousAlertSeverity.Medium,
                Status = SuspiciousAlertStatus.Open,
                Message = "sibling",
                DedupKey = dedupKey,
                DetectedAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var svc = new AdminSuspiciousAlertService(db);
        var ok = await svc.MarkAsReadAsync(TenantA, primaryId, "manager1");
        Assert.True(ok);

        var primary = await db.SuspiciousTransactionAlerts.FindAsync(primaryId);
        var sibling = await db.SuspiciousTransactionAlerts.FindAsync(siblingId);
        Assert.Equal(SuspiciousAlertStatus.Acknowledged, primary!.Status);
        Assert.Equal(SuspiciousAlertStatus.Acknowledged, sibling!.Status);
    }
}

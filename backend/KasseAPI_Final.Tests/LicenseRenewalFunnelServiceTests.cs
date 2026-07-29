using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseRenewalFunnelServiceTests
{
    [Fact]
    public async Task GetFunnelAsync_CountsDistinctTenantsPerStep()
    {
        await using var db = CreateDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantC = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow.AddDays(1);

        db.Tenants.AddRange(
            MakeTenant(tenantA, "A", "a"),
            MakeTenant(tenantB, "B", "b"),
            MakeTenant(tenantC, "C", "c"));

        db.BillingAuditLogs.AddRange(
            MakeBilling(tenantA, BillingAuditEventTypes.LicenseReminderSent, DateTime.UtcNow.AddDays(-10)),
            MakeBilling(tenantB, BillingAuditEventTypes.LicenseReminderSent, DateTime.UtcNow.AddDays(-9)),
            MakeBilling(tenantA, BillingAuditEventTypes.LicenseReminderSent, DateTime.UtcNow.AddDays(-8)),
            MakeBilling(tenantA, BillingAuditEventTypes.LicenseActivated, DateTime.UtcNow.AddDays(-2)),
            MakeBilling(tenantC, BillingAuditEventTypes.LicenseExtended, DateTime.UtcNow.AddDays(-3)));

        db.AuditLogs.AddRange(
            MakeAudit(tenantA, AuditEventType.LicenseRenewalPageViewed, AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED, DateTime.UtcNow.AddDays(-7)),
            MakeAudit(tenantB, AuditEventType.LicenseRenewalPageViewed, AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED, DateTime.UtcNow.AddDays(-6)),
            MakeAudit(tenantA, AuditEventType.LicenseRenewed, AuditLogActions.LICENSE_RENEWED, DateTime.UtcNow.AddDays(-4)));

        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>(MockBehavior.Strict);
        var sut = new LicenseRenewalFunnelService(db, audit.Object, NullLogger<LicenseRenewalFunnelService>.Instance);

        var funnel = await sut.GetFunnelAsync(new LicenseRenewalFunnelQuery(from, to));

        Assert.Equal(2, funnel.Total);
        Assert.Equal(2, funnel.ReminderSent);
        Assert.Equal(2, funnel.PageViewed);
        Assert.Equal(2, funnel.Renewed); // A renewed + C extended
        Assert.Equal(1, funnel.Activated);
        Assert.Equal(50.0, funnel.ConversionRate);
    }

    [Fact]
    public async Task RecordPageViewAsync_DedupesSameUtcDay()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(MakeTenant(tenantId, "A", "a"));
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.LicenseRenewalPageViewed,
                It.IsAny<Guid?>(),
                tenantId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = "s",
                UserId = "u1",
                UserRole = "Manager",
                Action = AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED,
                ActionType = AuditEventType.LicenseRenewalPageViewed,
                Timestamp = DateTime.UtcNow,
                Status = AuditLogStatus.Success,
            })
            .Callback(() =>
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SessionId = "s",
                    UserId = "u1",
                    UserRole = "Manager",
                    Action = AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED,
                    ActionType = AuditEventType.LicenseRenewalPageViewed,
                    Timestamp = DateTime.UtcNow,
                    Status = AuditLogStatus.Success,
                });
                db.SaveChanges();
            });

        var sut = new LicenseRenewalFunnelService(db, audit.Object, NullLogger<LicenseRenewalFunnelService>.Instance);

        Assert.True(await sut.RecordPageViewAsync(tenantId, "u1", "Manager"));
        Assert.False(await sut.RecordPageViewAsync(tenantId, "u1", "Manager"));
        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.LicenseRenewalPageViewed,
                It.IsAny<Guid?>(),
                tenantId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    private static Tenant MakeTenant(Guid id, string name, string slug) => new()
    {
        Id = id,
        Name = name,
        Slug = slug,
        Status = TenantStatuses.Active,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static BillingAuditLog MakeBilling(Guid tenantId, string action, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserId = Guid.Empty,
        Action = action,
        TimestampUtc = at,
    };

    private static AuditLog MakeAudit(Guid tenantId, AuditEventType type, string action, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SessionId = "s",
        UserId = "u1",
        UserRole = "Manager",
        Action = action,
        ActionType = type,
        Timestamp = at,
        Status = AuditLogStatus.Success,
    };

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicFunnel_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }
}

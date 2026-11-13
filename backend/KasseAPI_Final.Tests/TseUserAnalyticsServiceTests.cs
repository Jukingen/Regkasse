using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseUserAnalyticsServiceTests
{
    [Fact]
    public async Task GenerateUserReportAsync_AggregatesSessionsFeaturesAndFunnel()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var now = DateTime.UtcNow;
        var from = now.AddDays(-14);

        db.AuthSessions.AddRange(
            Session(tenantId, "user-a", now.AddDays(-3), durationMinutes: 30),
            Session(tenantId, "user-b", now.AddDays(-2), durationMinutes: 45),
            Session(tenantId, "user-a", now.AddDays(-1), durationMinutes: 20));

        db.AuditLogs.AddRange(
            Audit(tenantId, "user-a", AuditLogActions.USER_LOGIN, AuditLogEntityTypes.USER, now.AddDays(-3)),
            Audit(tenantId, "user-a", AuditLogActions.POS_REG_READY, AuditLogEntityTypes.CASH_REGISTER, now.AddDays(-3).AddMinutes(1)),
            Audit(tenantId, "user-a", AuditLogActions.CART_CREATE, AuditLogEntityTypes.CART, now.AddDays(-3).AddMinutes(2)),
            Audit(tenantId, "user-a", AuditLogActions.PAYMENT_CONFIRM, AuditLogEntityTypes.PAYMENT, now.AddDays(-3).AddMinutes(3)),
            Audit(tenantId, "user-a", AuditLogActions.RECEIPT_SAVED, AuditLogEntityTypes.RECEIPT, now.AddDays(-3).AddMinutes(4)),
            Audit(tenantId, "user-b", AuditLogActions.USER_LOGIN, AuditLogEntityTypes.USER, now.AddDays(-2)),
            Audit(tenantId, "user-b", AuditLogActions.POS_REG_READY, AuditLogEntityTypes.CASH_REGISTER, now.AddDays(-2).AddMinutes(1)),
            Audit(tenantId, "user-b", AuditLogActions.TSE_STATUS_CHECK, AuditLogEntityTypes.TSE_DEVICE, now.AddDays(-2).AddMinutes(2)));
        await db.SaveChangesAsync();

        var svc = new TseUserAnalyticsService(db, NullLogger<TseUserAnalyticsService>.Instance);
        var report = await svc.GenerateUserReportAsync(tenantId, from, now);

        Assert.Equal(tenantId, report.TenantId);
        Assert.True(report.DiagnosticOnly);
        Assert.Equal(3, report.TotalSessions);
        Assert.Equal(2, report.UniqueUsers);
        Assert.True(report.AverageSessionDuration > 0);
        Assert.True(report.FeatureUsage.ContainsKey("login"));
        Assert.True(report.FeatureUsage.ContainsKey("payment"));
        Assert.NotEmpty(report.FunnelSteps);
        Assert.NotEmpty(report.DropoffPoints);
        Assert.True(report.UserSatisfactionScores.ContainsKey("overall"));
        Assert.NotEmpty(report.Recommendations);
    }

    [Fact]
    public async Task GetFeatureUsageReportAsync_BuildsHeatmap()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var now = DateTime.UtcNow;

        db.AuditLogs.Add(
            Audit(tenantId, "user-a", AuditLogActions.PAYMENT_CONFIRM, AuditLogEntityTypes.PAYMENT, now.AddHours(-2)));
        await db.SaveChangesAsync();

        var svc = new TseUserAnalyticsService(db, NullLogger<TseUserAnalyticsService>.Instance);
        var features = await svc.GetFeatureUsageReportAsync(tenantId, now.AddDays(-7), now);

        Assert.Equal(tenantId, features.TenantId);
        Assert.True(features.FeatureUsage.ContainsKey("payment"));
        Assert.NotEmpty(features.Heatmap);
        Assert.Contains(features.Heatmap, c => c.Feature == "payment" && c.Count >= 1);
    }

    [Fact]
    public async Task PerformCohortAnalysisAsync_GroupsUsersByWeek()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var now = DateTime.UtcNow;
        var from = now.AddDays(-60);

        db.AuthSessions.AddRange(
            Session(tenantId, "cohort-a", now.AddDays(-28), durationMinutes: 10),
            Session(tenantId, "cohort-b", now.AddDays(-28), durationMinutes: 10),
            Session(tenantId, "cohort-a", now.AddDays(-7), durationMinutes: 15));
        db.AuditLogs.Add(
            Audit(tenantId, "cohort-a", AuditLogActions.USER_LOGIN, AuditLogEntityTypes.USER, now.AddDays(-7)));
        await db.SaveChangesAsync();

        var svc = new TseUserAnalyticsService(db, NullLogger<TseUserAnalyticsService>.Instance);
        var cohorts = await svc.PerformCohortAnalysisAsync(tenantId, from, now);

        Assert.True(cohorts.CohortWeeks >= 1);
        Assert.Contains(cohorts.Cohorts, c => c.CohortSize >= 1);
        Assert.All(cohorts.Cohorts, c => Assert.NotEmpty(c.RetentionByWeek));
    }

    [Fact]
    public async Task GenerateUserReportAsync_UnknownTenant_ThrowsKeyNotFound()
    {
        await using var db = CreateDb();
        var svc = new TseUserAnalyticsService(db, NullLogger<TseUserAnalyticsService>.Instance);
        var now = DateTime.UtcNow;

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GenerateUserReportAsync(Guid.NewGuid(), now.AddDays(-7), now));
    }

    [Theory]
    [InlineData(AuditLogActions.PAYMENT_CONFIRM, AuditLogEntityTypes.PAYMENT, "payment")]
    [InlineData(AuditLogActions.TSE_STATUS_CHECK, AuditLogEntityTypes.TSE_DEVICE, "tse_status")]
    [InlineData(AuditLogActions.POS_SPL_RCPT, AuditLogEntityTypes.RECEIPT, "special_receipt")]
    [InlineData(AuditLogActions.USER_LOGIN, AuditLogEntityTypes.USER, "login")]
    public void MapFeature_MapsKnownActions(string action, string entity, string expected)
    {
        Assert.Equal(expected, TseUserAnalyticsService.MapFeature(action, entity));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_user_analytics_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "UX Cafe",
            Slug = "ux-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static AuthSession Session(Guid tenantId, string userId, DateTime created, int durationMinutes) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientApp = "pos",
            TenantId = tenantId,
            CreatedAtUtc = created,
            LastActivityAtUtc = created.AddMinutes(durationMinutes),
        };

    private static AuditLog Audit(
        Guid tenantId,
        string userId,
        string action,
        string entityType,
        DateTime timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            UserRole = "Cashier",
            Action = action,
            EntityType = entityType,
            Status = AuditLogStatus.Success,
            Timestamp = timestamp,
            CreatedAt = timestamp,
            IsActive = true,
        };
}

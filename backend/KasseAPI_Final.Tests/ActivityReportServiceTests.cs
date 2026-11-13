using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.ActivityReports;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ActivityReportServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ActivityReport_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static ActivityReportService CreateService(AppDbContext db) =>
        new(db, new ActivityAnomalyService(db), NullLogger<ActivityReportService>.Instance);

    private static async Task SeedTenantAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Activity Tenant",
            Slug = "activity-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static OperationLog Log(
        string type,
        string userId,
        DateTime createdAt,
        bool undone = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = userId,
            OperationType = type,
            EntityType = OperationEntityTypes.Product,
            EntityId = Guid.NewGuid().ToString("N"),
            CreatedAt = createdAt,
            IsUndone = undone,
        };

    [Fact]
    public async Task GenerateWeeklyReportAsync_ReturnsNull_WhenTenantMissing()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var report = await service.GenerateWeeklyReportAsync(Guid.NewGuid());

        Assert.Null(report);
    }

    [Fact]
    public async Task GenerateWeeklyReportAsync_AggregatesByOperationType()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var now = DateTime.UtcNow;
        db.OperationLogs.AddRange(
            Log(OperationTypes.UpdateProduct, "u1", now.AddDays(-1)),
            Log(OperationTypes.UpdateProduct, "u2", now.AddDays(-2)),
            Log(OperationTypes.UpdateCustomer, "u1", now.AddHours(-3)),
            Log(OperationTypes.UpdateProduct, "u1", now.AddDays(-10))); // outside window
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.GenerateWeeklyReportAsync(TenantId);

        Assert.NotNull(report);
        Assert.Equal(3, report!.TotalActivities);
        Assert.Equal(2, report.UniqueUsers);
        Assert.Equal(2, report.ActivitySummary.Count);
        var product = Assert.Single(report.ActivitySummary, a => a.OperationType == OperationTypes.UpdateProduct);
        Assert.Equal(2, product.Count);
        Assert.Equal(2, product.Users);
    }

    [Fact]
    public async Task GenerateWeeklyReportAsync_DetectsHighVolumeAnomaly()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);
        var now = DateTime.UtcNow;
        for (var i = 0; i < ActivityAnomalyService.HighVolumeThreshold; i++)
        {
            db.OperationLogs.Add(Log(OperationTypes.UpdateProduct, "u1", now.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.GenerateWeeklyReportAsync(TenantId);

        Assert.NotNull(report);
        Assert.Contains(report!.Anomalies, a => a.Code == "HIGH_VOLUME");
        Assert.NotEmpty(report.Recommendations);
    }

    [Fact]
    public async Task GenerateWeeklyReportAsync_EmptyWeek_HasGuidanceRecommendation()
    {
        await using var db = CreateDb();
        await SeedTenantAsync(db);

        var service = CreateService(db);
        var report = await service.GenerateWeeklyReportAsync(TenantId);

        Assert.NotNull(report);
        Assert.Equal(0, report!.TotalActivities);
        Assert.Contains(report.Recommendations, r => r.Contains("No operation-log activity", StringComparison.Ordinal));
    }
}

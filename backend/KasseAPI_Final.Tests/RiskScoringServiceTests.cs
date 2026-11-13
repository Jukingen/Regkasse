using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.RiskScoring;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RiskScoringServiceTests
{
    private static RiskScoringService CreateService() => new(CreateDb());

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RiskScoring_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(null));
    }

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static UserAction BaseAction(Action<UserAction>? configure = null)
    {
        var action = new UserAction
        {
            TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            UserId = "user-1",
            ActionType = "settings.update",
            Timestamp = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc),
            BulkCount = 1,
            AverageBulkCount = 1,
            IsKnownIp = true,
            IsRapidSuccession = false,
            IsFirstTime = false,
        };
        configure?.Invoke(action);
        return action;
    }

    [Fact]
    public void CalculateRisk_NoSignals_IsLow()
    {
        var service = CreateService();
        var result = service.CalculateRisk(BaseAction());

        Assert.Equal(0, result.Score);
        Assert.Equal(RiskLevels.Low, result.RiskLevel);
        Assert.Contains("No elevated risk", result.Reason);
    }

    [Fact]
    public void CalculateRisk_UnusualTime_Adds20()
    {
        var service = CreateService();
        var result = service.CalculateRisk(BaseAction(a =>
            a.Timestamp = new DateTime(2026, 7, 23, 4, 15, 0, DateTimeKind.Utc)));

        Assert.Equal(20, result.Score);
        Assert.Equal(RiskLevels.Low, result.RiskLevel);
        Assert.Contains("Unusual time", result.Reason);
    }

    [Fact]
    public void CalculateRisk_BulkOverAverage_Adds30()
    {
        var service = CreateService();
        var result = service.CalculateRisk(BaseAction(a =>
        {
            a.BulkCount = 40;
            a.AverageBulkCount = 10;
        }));

        Assert.Equal(30, result.Score);
        Assert.Equal(RiskLevels.Medium, result.RiskLevel);
        Assert.Contains("Bulk operation", result.Reason);
    }

    [Fact]
    public void CalculateRisk_AllRules_CapsAt100_AndCritical()
    {
        var service = CreateService();
        var result = service.CalculateRisk(BaseAction(a =>
        {
            a.Timestamp = new DateTime(2026, 7, 23, 3, 0, 0, DateTimeKind.Utc);
            a.BulkCount = 100;
            a.AverageBulkCount = 10;
            a.IsKnownIp = false;
            a.IsRapidSuccession = true;
            a.IsFirstTime = true;
        }));

        // 20+30+15+25+10 = 100
        Assert.Equal(100, result.Score);
        Assert.Equal(RiskLevels.Critical, result.RiskLevel);
        Assert.Contains("New IP address", result.Reason);
        Assert.Contains("Rapid succession", result.Reason);
        Assert.Contains("First time", result.Reason);
    }

    [Fact]
    public void CalculateRisk_HighThreshold_At50()
    {
        var service = CreateService();
        var result = service.CalculateRisk(BaseAction(a =>
        {
            a.IsRapidSuccession = true; // 25
            a.IsKnownIp = false; // 15
            a.IsFirstTime = true; // 10 => 50
        }));

        Assert.Equal(50, result.Score);
        Assert.Equal(RiskLevels.High, result.RiskLevel);
    }

    [Fact]
    public async Task EvaluateAsync_PersistsWhenMediumOrAbove()
    {
        var db = CreateDb();
        var service = new RiskScoringService(db);
        var action = BaseAction(a =>
        {
            a.BulkCount = 40;
            a.AverageBulkCount = 10;
        });

        var response = await service.EvaluateAsync(action, persistIfElevated: true);

        Assert.Equal(30, response.Score);
        Assert.NotNull(response.PersistedId);
        // ITenantEntity global filter: accessor TenantId is null in unit tests.
        Assert.Equal(1, await db.RiskScores.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotPersistLowScores()
    {
        var db = CreateDb();
        var service = new RiskScoringService(db);

        var response = await service.EvaluateAsync(BaseAction(), persistIfElevated: true);

        Assert.Equal(0, response.Score);
        Assert.Null(response.PersistedId);
        Assert.Equal(0, await db.RiskScores.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task ResolveAsync_MarksResolved()
    {
        var db = CreateDb();
        var service = new RiskScoringService(db);
        var action = BaseAction(a =>
        {
            a.BulkCount = 40;
            a.AverageBulkCount = 10;
        });
        var evaluated = await service.EvaluateAsync(action);
        Assert.NotNull(evaluated.PersistedId);

        var resolved = await service.ResolveAsync(evaluated.PersistedId!.Value, "admin-1", "False positive");

        Assert.NotNull(resolved);
        Assert.True(resolved!.IsResolved);
        Assert.Equal("False positive", resolved.Resolution);
        Assert.Equal("admin-1", resolved.ResolvedBy);
    }
}

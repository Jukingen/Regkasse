using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AuditLogStatisticsAggregationTests
{
    [Fact]
    public void FromCombinedGroups_MatchesSeparateGroupByCounts()
    {
        var rows = new (string Action, AuditLogStatus Status, string EntityType)[]
        {
            ("CREATE", AuditLogStatus.Success, "Payment"),
            ("CREATE", AuditLogStatus.Success, "Payment"),
            ("CREATE", AuditLogStatus.Failed, "Payment"),
            ("READ", AuditLogStatus.Success, "Cart"),
        };

        var legacy = LegacyAggregate(rows);
        var combined = AuditLogStatisticsAggregation.FromCombinedGroups(rows
            .GroupBy(row => new { row.Action, row.Status, row.EntityType })
            .Select(group => new AuditLogStatisticsAggregation.Bucket(
                group.Key.Action,
                group.Key.Status,
                group.Key.EntityType,
                group.Count())));

        Assert.Equal(legacy, combined);
        Assert.Equal(3, combined["Action_CREATE"]);
        Assert.Equal(3, combined["Status_Success"]);
        Assert.Equal(3, combined["EntityType_Payment"]);
        Assert.Equal(4, combined["Total"]);
    }

    [Fact]
    public async Task GetAuditLogStatisticsAsync_DefaultWindow_ExcludesOlderRows_AndMatchesLegacyCounts()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuditStats_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));

        var now = DateTime.UtcNow;
        db.AuditLogs.AddRange(
            NewLog(tenantId, "CREATE", AuditLogStatus.Success, "Payment", now.AddHours(-2)),
            NewLog(tenantId, "CREATE", AuditLogStatus.Failed, "Cart", now.AddDays(-1)),
            NewLog(tenantId, "READ", AuditLogStatus.Success, "Payment", now.AddDays(-60)));
        await db.SaveChangesAsync();

        var service = new AuditLogService(
            db,
            new Mock<ILogger<AuditLogService>>().Object,
            Mock.Of<IHttpContextAccessor>(),
            new NullCurrentTenantAccessor(),
            Mock.Of<IActorDisplayNameResolver>(),
            Options.Create(new AuditRetentionOptions()));

        var statistics = await service.GetAuditLogStatisticsAsync();
        var inWindow = new (string Action, AuditLogStatus Status, string EntityType)[]
        {
            ("CREATE", AuditLogStatus.Success, "Payment"),
            ("CREATE", AuditLogStatus.Failed, "Cart"),
        };

        Assert.Equal(LegacyAggregate(inWindow), statistics);
        Assert.False(statistics.ContainsKey("Action_READ"));
        Assert.Equal(2, statistics["Total"]);
    }

    private static Dictionary<string, int> LegacyAggregate(
        IEnumerable<(string Action, AuditLogStatus Status, string EntityType)> rows)
    {
        var list = rows.ToList();
        var statistics = new Dictionary<string, int>();
        foreach (var stat in list.GroupBy(row => row.Action))
            statistics[$"Action_{stat.Key}"] = stat.Count();
        foreach (var stat in list.GroupBy(row => row.Status))
            statistics[$"Status_{stat.Key}"] = stat.Count();
        foreach (var stat in list.GroupBy(row => row.EntityType))
            statistics[$"EntityType_{stat.Key}"] = stat.Count();
        statistics["Total"] = list.GroupBy(row => row.Action).Sum(stat => stat.Count());
        return statistics;
    }

    private static AuditLog NewLog(
        Guid tenantId,
        string action,
        AuditLogStatus status,
        string entityType,
        DateTime timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = "auditor",
            UserRole = "Manager",
            Action = action,
            EntityType = entityType,
            Status = status,
            Timestamp = timestamp,
            SessionId = Guid.NewGuid().ToString("N"),
        };
}

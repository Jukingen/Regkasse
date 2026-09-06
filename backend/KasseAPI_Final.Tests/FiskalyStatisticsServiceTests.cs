using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Reflection;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyStatisticsServiceTests
{
    [Fact]
    public void ResolveRange_DefaultsToLast30Days()
    {
        var (from, to) = FiskalyStatisticsService.ResolveRange(null, null);
        Assert.True(to > from);
        Assert.InRange((to - from).TotalDays, 29.9, 30.1);
    }

    [Fact]
    public void ResolveRange_RejectsInvertedAndTooLong()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            FiskalyStatisticsService.ResolveRange(now, now.AddMinutes(-1)));
        Assert.Throws<ArgumentException>(() =>
            FiskalyStatisticsService.ResolveRange(now.AddDays(-400), now));
    }

    [Fact]
    public void Build_ComputesKpisAndFillsGaps()
    {
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 3, 23, 59, 59, DateTimeKind.Utc);
        var rows = new[]
        {
            new FiskalyStatisticsService.StatRow(from.AddHours(2), from.AddHours(2).AddMilliseconds(100), "Success", "normal"),
            new FiskalyStatisticsService.StatRow(from.AddHours(3), from.AddHours(3).AddMilliseconds(300), "Failed", "normal"),
            new FiskalyStatisticsService.StatRow(from.AddDays(2), from.AddDays(2).AddMilliseconds(200), "Success", "cancel"),
            new FiskalyStatisticsService.StatRow(from.AddDays(2).AddHours(1), null, "Pending", "cancel"),
        };

        var dto = FiskalyStatisticsService.Build(from, to, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), rows);

        Assert.Equal(4, dto.Kpis.TotalOperations);
        Assert.Equal(50, dto.Kpis.SuccessRatePercent);
        Assert.Equal("cancel", dto.Kpis.MostUsedOperationType);
        Assert.Equal(1, dto.Kpis.TotalErrors);
        Assert.Equal(2, dto.Kpis.SuccessCount);
        Assert.Equal(1, dto.Kpis.FailedCount);
        Assert.Equal(1, dto.Kpis.InFlightCount);
        Assert.Equal(200, dto.Kpis.AverageProcessingTimeMs);
        Assert.Equal(3, dto.Daily.Count);
        Assert.Equal("2026-08-01", dto.Daily[0].Date);
        Assert.Equal(2, dto.Daily[0].Total);
        Assert.Equal(0, dto.Daily[1].Total);
        Assert.Equal(2, dto.Daily[2].Total);
        Assert.Single(dto.Monthly);
        Assert.Equal("2026-08", dto.Monthly[0].YearMonth);
        Assert.Equal(4, dto.Monthly[0].Total);
    }

    [Fact]
    public void Build_Empty_ZeroSuccessRate()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var dto = FiskalyStatisticsService.Build(from, to, null, []);
        Assert.Equal(0, dto.Kpis.TotalOperations);
        Assert.Equal(0, dto.Kpis.SuccessRatePercent);
        Assert.Null(dto.Kpis.MostUsedOperationType);
        Assert.Null(dto.Kpis.AverageProcessingTimeMs);
        Assert.Equal(2, dto.Daily.Count);
    }

    [Fact]
    public async Task Get_Manager_SeesOnlyAmbientTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenantA, FiskalyOperationTypes.Normal, "Success"));
        db.FiskalyOperationHistories.Add(Row(tenantB, FiskalyOperationTypes.Cancel, "Failed"));
        await db.SaveChangesAsync();

        var sut = new FiskalyStatisticsService(db, accessor);
        var dto = await sut.GetAsync(
            new FiskalyStatisticsQuery { TenantId = tenantB },
            actorIsSuperAdmin: false);

        Assert.Equal(1, dto.Kpis.TotalOperations);
        Assert.Equal(100, dto.Kpis.SuccessRatePercent);
        Assert.Equal("normal", dto.Kpis.MostUsedOperationType);
        Assert.Equal(tenantA, dto.TenantId);
    }

    [Fact]
    public async Task Get_SuperAdmin_SeesAllThenFiltersTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenantA);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenantA, FiskalyOperationTypes.Normal, "Success"));
        db.FiskalyOperationHistories.Add(Row(tenantB, FiskalyOperationTypes.Nullbeleg, "Failed"));
        await db.SaveChangesAsync();

        var sut = new FiskalyStatisticsService(db, accessor);
        var all = await sut.GetAsync(new FiskalyStatisticsQuery(), actorIsSuperAdmin: true);
        Assert.Equal(2, all.Kpis.TotalOperations);
        Assert.Equal(50, all.Kpis.SuccessRatePercent);
        Assert.Null(all.TenantId);

        var filtered = await sut.GetAsync(
            new FiskalyStatisticsQuery { TenantId = tenantB },
            actorIsSuperAdmin: true);
        Assert.Equal(1, filtered.Kpis.TotalOperations);
        Assert.Equal(0, filtered.Kpis.SuccessRatePercent);
        Assert.Equal("nullbeleg", filtered.Kpis.MostUsedOperationType);
        Assert.Equal(tenantB, filtered.TenantId);
    }

    [Fact]
    public async Task Get_FiltersOperationTypeAndIgnoresRowsOutsideRange()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        var now = DateTime.UtcNow;
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Cancel, "Failed", now.AddDays(-2)));
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Normal, "Success", now.AddDays(-2)));
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Cancel, "Success", now.AddDays(-40)));
        await db.SaveChangesAsync();

        var sut = new FiskalyStatisticsService(db, accessor);
        var dto = await sut.GetAsync(
            new FiskalyStatisticsQuery { OperationType = FiskalyOperationTypes.Cancel },
            actorIsSuperAdmin: false);

        Assert.Equal(1, dto.Kpis.TotalOperations);
        Assert.Equal("cancel", dto.Kpis.MostUsedOperationType);
        Assert.Equal(1, dto.Kpis.TotalErrors);
    }

    [Fact]
    public async Task Export_CsvAndPdf_ReturnFiles()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        db.FiskalyOperationHistories.Add(Row(tenant, FiskalyOperationTypes.Startbeleg, "Success"));
        await db.SaveChangesAsync();

        var sut = new FiskalyStatisticsService(db, accessor);
        var csv = await sut.ExportAsync(new FiskalyStatisticsQuery(), "csv", actorIsSuperAdmin: false);
        Assert.Contains("text/csv", csv.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".csv", csv.FileName);
        var csvText = System.Text.Encoding.UTF8.GetString(csv.Bytes);
        Assert.Contains("totalOperations", csvText);
        Assert.Contains("startbeleg", csvText);

        var pdf = await sut.ExportAsync(new FiskalyStatisticsQuery(), "pdf", actorIsSuperAdmin: false);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.EndsWith(".pdf", pdf.FileName);
        Assert.True(pdf.Bytes.Length > 100);
        Assert.Equal('%', (char)pdf.Bytes[0]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ExportAsync(new FiskalyStatisticsQuery(), "xlsx", actorIsSuperAdmin: false));
    }

    [Fact]
    public void CsvBuilder_IncludesKpiAndSeriesRows()
    {
        var stats = FiskalyStatisticsService.Build(
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            null,
            [new FiskalyStatisticsService.StatRow(
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc).AddMilliseconds(50),
                "Success",
                "normal")]);
        var csv = FiskalyStatisticsCsvBuilder.Build(stats);
        Assert.Contains("kpi,totalOperations,1", csv);
        Assert.Contains("operationType,normal,1", csv);
        Assert.Contains("daily,2026-08-01,1|1|0", csv);
    }

    [Fact]
    public void StatisticsController_RequiresHistoryView()
    {
        var get = typeof(Controllers.AdminFiskalyStatisticsController)
            .GetMethod(nameof(Controllers.AdminFiskalyStatisticsController.Get));
        var export = typeof(Controllers.AdminFiskalyStatisticsController)
            .GetMethod(nameof(Controllers.AdminFiskalyStatisticsController.Export));
        Assert.NotNull(get);
        Assert.NotNull(export);
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            get!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
        Assert.Contains(
            AppPermissions.FiskalyHistoryView,
            export!.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
    }

    private static (AppDbContext Db, ICurrentTenantAccessor Accessor) CreateDb(Guid ambientTenant)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(ambientTenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_stats_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return (new AppDbContext(options, accessor), accessor);
    }

    private static FiskalyOperationHistory Row(
        Guid tenantId,
        string operation,
        string status,
        DateTime? createdAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperationType = operation,
            Status = status,
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = operation,
            UserId = "user",
            UserDisplayName = "user",
            RequestPayloadJson = """{"cashRegisterId":"00000000-0000-0000-0000-000000000001"}""",
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            CompletedAtUtc = status is "Success" or "Failed" ? (createdAtUtc ?? DateTime.UtcNow).AddMilliseconds(80) : null
        };
}

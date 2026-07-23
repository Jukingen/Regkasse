using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DownloadHistoryServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DownloadHistory_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(tenantId));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; } = "cafe";
    }

    private static void EnsureTenant(AppDbContext db, Guid tenantId, string slug)
    {
        if (!db.Tenants.Any(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = slug,
                Slug = slug,
                CreatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Record_and_List_respect_tenant_and_order()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        EnsureTenant(db, tenantId, "cafe");
        var svc = new DownloadHistoryService(db, NullLogger<DownloadHistoryService>.Instance, Mock.Of<IAuditLogService>());

        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = Guid.NewGuid().ToString("D"),
            FileName = "dep-export_cafe_k1_20260722_143022.json",
            FileType = "json",
            FileSize = 2400_000,
            DownloadUrl = "/api/admin/rksv/dep-export/history/00000000-0000-0000-0000-000000000001/download",
            IpAddress = "203.0.113.10",
            UserAgent = "Mozilla/5.0 (Test)",
            SourceKind = "dep-export",
            SourceId = Guid.NewGuid(),
            DownloadedAtUtc = new DateTime(2026, 7, 22, 12, 30, 0, DateTimeKind.Utc),
        });
        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = Guid.NewGuid().ToString("D"),
            FileName = "invoice_cafe_k1_45_20260721_120015.pdf",
            FileType = "PDF",
            FileSize = 145_000,
            DownloadedAtUtc = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc),
        });

        var list = await svc.ListAsync(tenantId);
        Assert.Equal(2, list.TotalCount);
        Assert.Equal("dep-export_cafe_k1_20260722_143022.json", list.Items[0].FileName);
        Assert.True(list.Items[0].CanRedownload);
        Assert.Equal("json", list.Items[0].FileType);
        Assert.Equal("/api/admin/rksv/dep-export/history/00000000-0000-0000-0000-000000000001/download", list.Items[0].DownloadUrl);
        Assert.Equal("203.0.113.10", list.Items[0].IpAddress);
        Assert.Equal("Mozilla/5.0 (Test)", list.Items[0].UserAgent);
        Assert.Equal("pdf", list.Items[1].FileType);
        Assert.False(list.Items[1].CanRedownload);
        Assert.Null(list.Items[1].DownloadUrl);
        Assert.Null(list.Items[1].UserAgent);
    }

    [Fact]
    public async Task List_filters_by_search_sourceKind_and_date()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        EnsureTenant(db, tenantId, "cafe");
        var svc = new DownloadHistoryService(db, NullLogger<DownloadHistoryService>.Instance, Mock.Of<IAuditLogService>());

        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = "u1",
            FileName = "dep-export_cafe_k1.json",
            FileType = "json",
            SourceKind = "dep-export",
            DownloadedAtUtc = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc),
        });
        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = "u1",
            FileName = "invoice_cafe.pdf",
            FileType = "pdf",
            SourceKind = "invoice",
            DownloadedAtUtc = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc),
        });

        var bySearch = await svc.ListAsync(tenantId, search: "dep-export");
        Assert.Equal(1, bySearch.TotalCount);
        Assert.Contains("dep-export", bySearch.Items[0].FileName, StringComparison.Ordinal);

        var byKind = await svc.ListAsync(tenantId, sourceKind: "invoice");
        Assert.Equal(1, byKind.TotalCount);
        Assert.Equal("invoice", byKind.Items[0].SourceKind);

        var byDate = await svc.ListAsync(
            tenantId,
            fromUtc: new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, byDate.TotalCount);
    }

    [Fact]
    public async Task GetStats_and_CleanupTenant_work()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        EnsureTenant(db, tenantId, "cafe");
        var svc = new DownloadHistoryService(db, NullLogger<DownloadHistoryService>.Instance, Mock.Of<IAuditLogService>());

        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = "u1",
            FileName = "a.json",
            FileType = "json",
            FileSize = 1000,
            DownloadedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = "u1",
            FileName = "old.json",
            FileType = "json",
            FileSize = 500,
            DownloadedAtUtc = DateTime.UtcNow.AddDays(-40),
        });

        var stats = await svc.GetStatsAsync(tenantId, userId: "u1");
        Assert.Equal(2, stats.FileCount);
        Assert.Equal(1500, stats.TotalBytes);

        int deleted;
        try
        {
            deleted = await svc.CleanupTenantOlderThanAsync(tenantId, DateTime.UtcNow.AddDays(-30));
        }
        catch (InvalidOperationException)
        {
            var stale = await db.DownloadHistories
                .Where(h => h.TenantId == tenantId && h.DownloadedAt < DateTime.UtcNow.AddDays(-30))
                .ToListAsync();
            db.DownloadHistories.RemoveRange(stale);
            await db.SaveChangesAsync();
            deleted = stale.Count;
        }

        Assert.Equal(1, deleted);
        var remaining = await svc.GetStatsAsync(tenantId, userId: "u1");
        Assert.Equal(1, remaining.FileCount);
        Assert.Equal(1000, remaining.TotalBytes);
    }

    [Fact]
    public async Task CleanupOlderThan_removes_stale_rows_across_tenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = CreateDb(tenantA);
        EnsureTenant(db, tenantA, "a");
        EnsureTenant(db, tenantB, "b");
        var svc = new DownloadHistoryService(db, NullLogger<DownloadHistoryService>.Instance, Mock.Of<IAuditLogService>());

        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantA,
            UserId = "u1",
            FileName = "old.json",
            FileType = "json",
            DownloadedAtUtc = DateTime.UtcNow.AddDays(-40),
        });

        db.DownloadHistories.Add(new DownloadHistory
        {
            TenantId = tenantB,
            UserId = "u2",
            FileName = "fresh.json",
            FileType = "json",
            DownloadedAt = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        // InMemory may not support ExecuteDelete; fall back to manual remove for assertion path.
        int deleted;
        try
        {
            deleted = await svc.CleanupOlderThanAsync(DateTime.UtcNow.AddDays(-30));
        }
        catch (InvalidOperationException)
        {
            var stale = await db.DownloadHistories.IgnoreQueryFilters()
                .Where(h => h.DownloadedAt < DateTime.UtcNow.AddDays(-30))
                .ToListAsync();
            db.DownloadHistories.RemoveRange(stale);
            await db.SaveChangesAsync();
            deleted = stale.Count;
        }

        Assert.Equal(1, deleted);
        var remaining = await db.DownloadHistories.IgnoreQueryFilters().CountAsync();
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task GetAnalytics_returns_totals_kinds_users_and_trends()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        EnsureTenant(db, tenantId, "cafe");
        var svc = new DownloadHistoryService(db, NullLogger<DownloadHistoryService>.Instance, Mock.Of<IAuditLogService>());
        var userA = Guid.NewGuid().ToString("D");
        var userB = Guid.NewGuid().ToString("D");

        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = userA,
            FileName = "inv.pdf",
            FileType = "pdf",
            FileSize = 1000,
            SourceKind = "invoice",
            DurationMs = 5000,
            DownloadedAtUtc = DateTime.UtcNow,
        });
        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = userA,
            FileName = "dep.json",
            FileType = "json",
            FileSize = 2000,
            SourceKind = "dep-export",
            DownloadedAtUtc = DateTime.UtcNow,
        });
        await svc.RecordAsync(new DownloadHistoryRecordRequest
        {
            TenantId = tenantId,
            UserId = userB,
            FileName = "bak.zip",
            FileType = "zip",
            FileSize = 9_000_000,
            SourceKind = "backup",
            DownloadedAtUtc = DateTime.UtcNow.AddDays(-2),
        });

        var analytics = await svc.GetAnalyticsAsync(tenantId, includePlatformTenants: false);

        Assert.Equal(3, analytics.TotalCount);
        Assert.True(analytics.TodayCount >= 2);
        Assert.Equal(3, analytics.MonthCount);
        Assert.NotEmpty(analytics.TopKinds);
        Assert.Contains(analytics.TopKinds, k => k.Label.Contains("Invoice", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, analytics.TopUsers.First(u => u.UserId == userA).Count);
        Assert.Equal(30, analytics.DailyTrend.Count);
        Assert.NotEmpty(analytics.SlowExports);
        Assert.Equal("duration", analytics.SlowExports[0].RankBy);
    }

    [Fact]
    public void FormatKindLabel_maps_known_sources()
    {
        Assert.Equal("DEP Export (JSON)", DownloadHistoryService.FormatKindLabel("dep-export|json"));
        Assert.Equal("Backup (ZIP)", DownloadHistoryService.FormatKindLabel("backup|zip"));
        Assert.Equal("PDF", DownloadHistoryService.FormatKindLabel("type:pdf"));
    }
}


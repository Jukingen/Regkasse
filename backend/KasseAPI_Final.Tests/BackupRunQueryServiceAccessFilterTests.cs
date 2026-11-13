using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunQueryServiceAccessFilterTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_query_access_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task GetHistoryAsync_ManagerScope_FiltersByTenantHint()
    {
        await using var db = CreateDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                IdempotencyKey = $"manual-tenant-{TenantA:D}-1",
                RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                IdempotencyKey = $"manual-tenant-{TenantB:D}-2",
                RequestedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var svc = new BackupRunQueryService(db);
        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "manager-1");
        var (items, total) = await svc.GetHistoryAsync(1, 20, scope);

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Contains(TenantA.ToString("D"), items[0].IdempotencyKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetLatestRunAsync_ManagerScope_ReturnsNewestAccessibleRun()
    {
        await using var db = CreateDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                IdempotencyKey = $"manual-tenant-{TenantB:D}-newer",
                RequestedAt = DateTime.UtcNow,
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                IdempotencyKey = $"manual-tenant-{TenantA:D}-older",
                RequestedAt = DateTime.UtcNow.AddHours(-2),
            });
        await db.SaveChangesAsync();

        var svc = new BackupRunQueryService(db);
        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "manager-1");
        var latest = await svc.GetLatestRunAsync(scope);

        Assert.NotNull(latest);
        Assert.Contains(TenantA.ToString("D"), latest!.IdempotencyKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAverageSucceededDurationAsync_ManagerScope_UsesAccessibleRunsOnly()
    {
        var started = DateTime.UtcNow.AddHours(-3);
        var completedA = started.AddMinutes(10);
        var completedB = started.AddMinutes(99);
        await using var db = CreateDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = TenantA,
                RequestedAt = started,
                StartedAt = started,
                CompletedAt = completedA,
            },
            new BackupRun
            {
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = TenantB,
                RequestedAt = started.AddHours(1),
                StartedAt = started.AddHours(1),
                CompletedAt = completedB,
            });
        await db.SaveChangesAsync();

        var svc = new BackupRunQueryService(db);
        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "manager-1");
        var stats = await svc.GetAverageSucceededDurationAsync(15, scope);

        Assert.Equal(1, stats.SampleCount);
        Assert.Equal(600, stats.AverageDurationSeconds);
    }
}

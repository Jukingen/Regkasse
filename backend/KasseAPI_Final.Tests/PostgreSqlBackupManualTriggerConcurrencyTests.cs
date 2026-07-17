using System.Collections.Concurrent;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Manuel yedek tetikleme: PostgreSQL <c>pg_advisory_xact_lock</c> ile eşzamanlılık (InMemory’de kilit yok).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlBackupManualTriggerConcurrencyTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public PostgreSqlBackupManualTriggerConcurrencyTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    private static IOptionsMonitor<BackupOptions> OptionsMonitorOf(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static BackupManualTriggerService CreateService(AppDbContext db)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new BackupManualTriggerService(
            db,
            audit.Object,
            Mock.Of<ICurrentTenantAccessor>(),
            OptionsMonitorOf(new BackupOptions()),
            Mock.Of<IBackupAlertPublisher>(),
            NullLogger<BackupManualTriggerService>.Instance);
    }

    private static async Task WipeBackupRunsAsync(AppDbContext db)
    {
        var rows = await db.BackupRuns.ToListAsync();
        db.BackupRuns.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task Concurrent_manual_requests_without_idempotency_yield_single_new_queued_run()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using (var wipe = CreateContext())
            await WipeBackupRunsAsync(wipe);

        const int n = 28;
        var bag = new ConcurrentBag<BackupManualTriggerOutcome>();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, n),
            new ParallelOptions { MaxDegreeOfParallelism = n },
            async (i, ct) =>
            {
                await using var db = CreateContext();
                var svc = CreateService(db);
                var o = await svc.RequestManualBackupAsync("u1", "Admin", null, $"corr-{i}", cancellationToken: ct);
                bag.Add(o);
            });

        Assert.Equal(1, bag.Count(o => o.Kind == BackupManualTriggerResultKind.NewRunQueued));
        Assert.Equal(n - 1, bag.Count(o => o.Kind == BackupManualTriggerResultKind.DuplicateActiveManualPrevented));

        await using var verify = CreateContext();
        var activeManual = await verify.BackupRuns.AsNoTracking()
            .CountAsync(r => r.TriggerSource == BackupTriggerSource.Manual
                              && (r.Status == BackupRunStatus.Queued
                                  || r.Status == BackupRunStatus.Running
                                  || r.Status == BackupRunStatus.AwaitingVerification));
        Assert.Equal(1, activeManual);
    }

    [SkippableFact]
    public async Task Concurrent_same_idempotency_key_requests_resolve_to_one_run()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using (var wipe = CreateContext())
            await WipeBackupRunsAsync(wipe);

        const string key = "idem-concurrent-shared-key";
        const int n = 22;
        var bag = new ConcurrentBag<BackupManualTriggerOutcome>();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, n),
            new ParallelOptions { MaxDegreeOfParallelism = n },
            async (i, ct) =>
            {
                await using var db = CreateContext();
                var svc = CreateService(db);
                var o = await svc.RequestManualBackupAsync("u1", "Admin", key, $"corr-{i}", cancellationToken: ct);
                bag.Add(o);
            });

        var distinctIds = bag.Select(o => o.Run.Id).Distinct().ToHashSet();
        Assert.Single(distinctIds);

        var newQueued = bag.Count(o => o.Kind == BackupManualTriggerResultKind.NewRunQueued);
        var replay = bag.Count(o => o.Kind == BackupManualTriggerResultKind.IdempotentReplay);
        Assert.Equal(1, newQueued);
        Assert.Equal(n - 1, replay);
    }

    [SkippableFact]
    public async Task Sequential_semantics_match_prior_api_contract()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using (var wipe = CreateContext())
            await WipeBackupRunsAsync(wipe);

        await using (var db = CreateContext())
        {
            var svc = CreateService(db);

            var first = await svc.RequestManualBackupAsync("u1", "Admin", null, "c1", cancellationToken: default);
            var second = await svc.RequestManualBackupAsync("u1", "Admin", null, "c2", cancellationToken: default);

            Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, first.Kind);
            Assert.Equal(BackupManualTriggerResultKind.DuplicateActiveManualPrevented, second.Kind);
            Assert.Equal(first.Run.Id, second.Run.Id);

            await WipeBackupRunsAsync(db);

            const string key = "idem-seq";
            var a = await svc.RequestManualBackupAsync("u1", "Admin", key, "c3", cancellationToken: default);
            var b = await svc.RequestManualBackupAsync("u1", "Admin", key, "c4", cancellationToken: default);
            Assert.Equal(BackupManualTriggerResultKind.NewRunQueued, a.Kind);
            Assert.Equal(BackupManualTriggerResultKind.IdempotentReplay, b.Kind);
            Assert.Equal(a.Run.Id, b.Run.Id);
        }
    }
}

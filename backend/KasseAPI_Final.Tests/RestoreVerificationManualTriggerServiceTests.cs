using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationManualTriggerServiceTests
{
    private static (RestoreVerificationManualTriggerService Svc, AppDbContext Db) CreateSut(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        var roMock = new Mock<IOptionsMonitor<RestoreVerificationOptions>>();
        roMock.Setup(m => m.CurrentValue).Returns(new RestoreVerificationOptions { DumpFallbackDepth = 7 });
        var svc = new RestoreVerificationManualTriggerService(
            db,
            roMock.Object,
            NullLogger<RestoreVerificationManualTriggerService>.Instance);
        return (svc, db);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_existing_run()
    {
        var dbName = $"rv_idem_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var existingId = Guid.NewGuid();
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = existingId,
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-30),
            IdempotencyKey = "idem-1"
        });
        await db.SaveChangesAsync();

        var result = await svc.EnqueueManualAsync("u1", "c1", "idem-1");

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.ExistingByIdempotencyKey, result.OrchestrationState);
        Assert.True(result.ExistingRunReturned);
        Assert.False(result.NewQueuedRunCreated);
        Assert.Equal(existingId, result.Run.Id);
        Assert.Equal(1, await db.RestoreVerificationRuns.CountAsync());
    }

    [Fact]
    public async Task Active_queued_run_prevents_new_creation_without_idempotency_key()
    {
        var dbName = $"rv_q_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var activeId = Guid.NewGuid();
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = activeId,
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await svc.EnqueueManualAsync("u1", null, null);

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.ExistingActiveRunReturned, result.OrchestrationState);
        Assert.Equal(activeId, result.Run.Id);
        Assert.Equal(1, await db.RestoreVerificationRuns.CountAsync());
    }

    [Fact]
    public async Task Active_running_run_prevents_new_creation()
    {
        var dbName = $"rv_r_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var activeId = Guid.NewGuid();
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = activeId,
            Status = RestoreVerificationStatus.Running,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var result = await svc.EnqueueManualAsync(null, null, "new-key");

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.ExistingActiveRunReturned, result.OrchestrationState);
        Assert.Equal(activeId, result.Run.Id);
        Assert.Equal(1, await db.RestoreVerificationRuns.CountAsync());
    }

    [Fact]
    public async Task Active_scheduled_queued_deduplicates_manual_trigger_even_with_new_idempotency_key()
    {
        var dbName = $"rv_sched_block_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var scheduledId = Guid.NewGuid();
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = scheduledId,
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await svc.EnqueueManualAsync("u1", "c1", "client-unique-key");

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.ExistingActiveRunReturned, result.OrchestrationState);
        Assert.True(result.ExistingRunReturned);
        Assert.False(result.NewQueuedRunCreated);
        Assert.Equal(scheduledId, result.Run.Id);
        Assert.Equal(1, await db.RestoreVerificationRuns.CountAsync());
    }

    [Fact]
    public async Task Completed_run_with_key_A_does_not_block_new_request_with_key_B()
    {
        var dbName = $"rv_ab_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            IdempotencyKey = "key-a"
        });
        await db.SaveChangesAsync();

        var result = await svc.EnqueueManualAsync("u", null, "key-b");

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.NewlyQueued, result.OrchestrationState);
        Assert.True(result.NewQueuedRunCreated);
        Assert.False(result.ExistingRunReturned);
        Assert.Equal("key-b", result.Run.IdempotencyKey);
        Assert.Equal(2, await db.RestoreVerificationRuns.CountAsync());
    }

    [Fact]
    public async Task New_manual_enqueue_persists_config_snapshot_json()
    {
        var dbName = $"rv_cfg_{Guid.NewGuid():N}";
        var (svc, db) = CreateSut(dbName);
        var result = await svc.EnqueueManualAsync("u1", "c1", null);

        Assert.Equal(RestoreVerificationTriggerOrchestrationState.NewlyQueued, result.OrchestrationState);
        Assert.NotNull(result.Run.ConfigSnapshotJson);
        Assert.Contains("restore_manual_enqueue", result.Run.ConfigSnapshotJson, StringComparison.Ordinal);
        Assert.Contains("\"dumpFallbackDepth\":7", result.Run.ConfigSnapshotJson, StringComparison.Ordinal);

        var fromDb = await db.RestoreVerificationRuns.AsNoTracking().SingleAsync(r => r.Id == result.Run.Id);
        Assert.Equal(result.Run.ConfigSnapshotJson, fromDb.ConfigSnapshotJson);
    }

    [Fact]
    public async Task Idempotency_key_longer_than_200_throws()
    {
        var dbName = $"rv_len_{Guid.NewGuid():N}";
        var (svc, _) = CreateSut(dbName);
        var longKey = new string('x', 201);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.EnqueueManualAsync(null, null, longKey));
    }
}

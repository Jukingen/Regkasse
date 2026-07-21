using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunQueryServicePgDumpTests
{
    [Fact]
    public async Task GetRecentSucceededPgDumpRunIdsAsync_returns_only_pg_dump_adapter()
    {
        var dbName = $"bk_pgdump_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var pg = Guid.NewGuid();
        var fake = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = pg,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = nameof(BackupExecutionAdapterKind.PgDump),
            RequestedAt = new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc)
        });
        db.BackupRuns.Add(new BackupRun
        {
            Id = fake,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = nameof(BackupExecutionAdapterKind.Fake),
            RequestedAt = new DateTime(2026, 4, 2, 13, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var sut = new BackupRunQueryService(db);
        var ids = await sut.GetRecentSucceededPgDumpRunIdsAsync(10, default);

        Assert.Single(ids);
        Assert.Equal(pg, ids[0]);
    }
}

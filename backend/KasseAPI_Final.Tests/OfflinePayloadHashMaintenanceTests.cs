using System;
using System.Linq;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class OfflinePayloadHashMaintenanceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PayloadHashMaint_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Analyze_CountsRuntimeMismatch_AndRepair_UpdatesIdempotently()
    {
        await using var context = CreateContext();
        var reg = Guid.NewGuid();
        var payload = """{"z":1,"a":2}""";
        var canonical = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(payload);

        context.OfflineTransactions.Add(new OfflineTransaction
        {
            Id = Guid.NewGuid(),
            CashRegisterId = reg,
            PayloadJson = payload,
            PayloadHash = new string('f', 64),
            ServerReceivedAtUtc = DateTime.UtcNow,
            OfflineCreatedAtUtc = DateTime.UtcNow,
            Status = OfflineTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var svc = new OfflinePayloadHashMaintenanceService(
            context,
            new Mock<ILogger<OfflinePayloadHashMaintenanceService>>().Object);

        var analyze = await svc.AnalyzeAsync(100, null);
        Assert.Equal(1, analyze.RuntimeMismatchCount);
        Assert.Equal(1, analyze.RepairableNoConflictCount);

        var dry = await svc.RepairAsync(100, dryRun: true, null);
        Assert.Equal(1, dry.Updated);
        Assert.True(dry.DryRun);

        var row = await context.OfflineTransactions.FirstAsync();
        Assert.NotEqual(canonical, row.PayloadHash);

        var applied = await svc.RepairAsync(100, dryRun: false, null);
        Assert.Equal(1, applied.Updated);
        Assert.False(applied.DryRun);

        await context.Entry(row).ReloadAsync();
        Assert.Equal(canonical, row.PayloadHash, StringComparer.OrdinalIgnoreCase);

        var second = await svc.RepairAsync(100, dryRun: false, null);
        Assert.Equal(0, second.Updated);
        Assert.Equal(1, second.SkippedAlreadyAligned);
    }
}

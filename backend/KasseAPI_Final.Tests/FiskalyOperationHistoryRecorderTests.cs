using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyOperationHistoryRecorderTests
{
    [Fact]
    public void Statuses_ProgressAndRetryRules()
    {
        Assert.Equal(5, FiskalyOperationHistoryStatuses.ProgressPercent("Pending"));
        Assert.Equal(55, FiskalyOperationHistoryStatuses.ProgressPercent("Processing"));
        Assert.Equal(100, FiskalyOperationHistoryStatuses.ProgressPercent("Success"));
        Assert.Equal(100, FiskalyOperationHistoryStatuses.ProgressPercent("Failed"));
        Assert.True(FiskalyOperationHistoryStatuses.IsInFlight("Pending"));
        Assert.True(FiskalyOperationHistoryStatuses.IsInFlight("Processing"));
        Assert.True(FiskalyOperationHistoryStatuses.IsTerminal("Success"));
        Assert.True(FiskalyOperationHistoryStatuses.CanRetry("Failed"));
        Assert.False(FiskalyOperationHistoryStatuses.CanRetry("Processing"));
        Assert.Equal("Success", FiskalyOperationHistoryStatuses.NormalizeFilter("Completed"));
        Assert.Equal("Processing", FiskalyOperationHistoryStatuses.NormalizeFilter("Processing"));
    }

    [Fact]
    public async Task Start_WritesPending_ThenMarkProcessing_ThenComplete_BroadcastsEach()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        var scope = new FiskalyOperationHistoryWriteScope();
        var broadcasts = new List<FiskalyOperationStatusEventDto>();
        var broadcaster = new Mock<IFiskalyOperationStatusBroadcaster>();
        broadcaster
            .Setup(b => b.PublishAsync(It.IsAny<FiskalyOperationStatusEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<FiskalyOperationStatusEventDto, CancellationToken>((evt, _) => broadcasts.Add(evt))
            .Returns(Task.CompletedTask);

        var sut = new FiskalyOperationHistoryRecorder(
            db,
            accessor,
            scope,
            NullLogger<FiskalyOperationHistoryRecorder>.Instance,
            broadcaster.Object);

        var id = await sut.StartAsync(new FiskalyOperationHistoryStartRequest
        {
            OperationType = FiskalyOperationTypes.Nullbeleg,
            CashRegisterId = Guid.NewGuid(),
            ActorUserId = "user-1",
            RequestPayload = new { reason = "test" }
        });

        Assert.NotNull(id);
        Assert.Equal(id, scope.CurrentHistoryId);
        Assert.Equal(FiskalyOperationHistoryStatuses.Pending, broadcasts[0].Status);
        Assert.Equal(5, broadcasts[0].ProgressPercent);

        await sut.MarkProcessingAsync(id.Value);
        Assert.Equal(FiskalyOperationHistoryStatuses.Processing, broadcasts[1].Status);
        Assert.Equal(55, broadcasts[1].ProgressPercent);

        await sut.CompleteAsync(id.Value, new FiskalyOperationHistoryCompleteRequest
        {
            Success = true,
            ReceiptNumber = "N-1",
            ReceiptId = "rid"
        });
        Assert.Equal(FiskalyOperationHistoryStatuses.Success, broadcasts[2].Status);
        Assert.Equal(100, broadcasts[2].ProgressPercent);

        var row = await db.FiskalyOperationHistories.AsNoTracking().SingleAsync();
        Assert.Equal(FiskalyOperationHistoryStatuses.Success, row.Status);
        Assert.Equal("N-1", row.ReceiptNumber);
        Assert.NotNull(row.CompletedAtUtc);
        Assert.Equal(3, broadcasts.Count);
    }

    [Fact]
    public async Task MarkProcessing_DoesNotOverwriteTerminalStatus()
    {
        var tenant = Guid.NewGuid();
        var (db, accessor) = CreateDb(tenant);
        await using var _ = db;
        var id = Guid.NewGuid();
        db.FiskalyOperationHistories.Add(new FiskalyOperationHistory
        {
            Id = id,
            TenantId = tenant,
            OperationType = FiskalyOperationTypes.Normal,
            Status = FiskalyOperationHistoryStatuses.Success,
            CashRegisterId = Guid.NewGuid(),
            UserId = "u1",
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var broadcaster = new Mock<IFiskalyOperationStatusBroadcaster>(MockBehavior.Strict);
        var sut = new FiskalyOperationHistoryRecorder(
            db,
            accessor,
            new FiskalyOperationHistoryWriteScope(),
            NullLogger<FiskalyOperationHistoryRecorder>.Instance,
            broadcaster.Object);

        await sut.MarkProcessingAsync(id);

        var row = await db.FiskalyOperationHistories.AsNoTracking().SingleAsync();
        Assert.Equal(FiskalyOperationHistoryStatuses.Success, row.Status);
        broadcaster.Verify(
            b => b.PublishAsync(It.IsAny<FiskalyOperationStatusEventDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static (AppDbContext Db, ICurrentTenantAccessor Accessor) CreateDb(Guid ambientTenant)
    {
        var accessor = TenantTestDoubles.TenantAccessorReturning(ambientTenant);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_hist_rec_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return (new AppDbContext(options, accessor), accessor);
    }
}

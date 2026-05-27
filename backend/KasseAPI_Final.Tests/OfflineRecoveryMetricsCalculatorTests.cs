using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OfflineRecoveryMetricsCalculatorTests
{
    [Fact]
    public void Build_ComputesPendingSnapshotsAndRecoveryStats()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var regId = Guid.NewGuid();

        var cohort = new List<OfflineTransaction>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CashRegisterId = regId,
                ServerReceivedAtUtc = from.AddHours(-1),
                Status = OfflineTransactionStatus.Synced,
                FiscalizedAtUtc = from.AddHours(2),
                RetryCount = 0,
            },
            new()
            {
                Id = Guid.NewGuid(),
                CashRegisterId = regId,
                ServerReceivedAtUtc = from.AddHours(1),
                Status = OfflineTransactionStatus.Synced,
                FiscalizedAtUtc = from.AddHours(3),
                RetryCount = 2,
            },
            new()
            {
                Id = Guid.NewGuid(),
                CashRegisterId = regId,
                ServerReceivedAtUtc = from.AddHours(2),
                Status = OfflineTransactionStatus.Failed,
                RetryCount = 1,
                LastReplayAttemptAt = from.AddHours(4),
            },
            new()
            {
                Id = Guid.NewGuid(),
                CashRegisterId = regId,
                ServerReceivedAtUtc = from.AddHours(5),
                Status = OfflineTransactionStatus.Pending,
            },
        };

        var report = OfflineRecoveryMetricsCalculator.Build(
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 1),
            from,
            to,
            endExclusive: true,
            cohort,
            new Dictionary<Guid, string> { [regId] = "K1" });

        Assert.Equal(1, report.RecoveredSuccessfully);
        Assert.Equal(1, report.RecoveredWithRetry);
        Assert.Equal(1, report.PermanentlyFailed);
        Assert.True(report.PendingAtEnd >= 1);
        Assert.Single(report.ByRegister);
        Assert.Equal("K1", report.ByRegister[0].RegisterNumber);
    }
}

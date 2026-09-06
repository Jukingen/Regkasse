using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class WalArchiveServiceTests
{
    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedUtcTimeProvider(DateTime utc) =>
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public void GetStatus_reads_files_and_covers_window()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"wal_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var old = Path.Combine(dir, "000000010000000000000001");
            var recent = Path.Combine(dir, "000000010000000000000002");
            File.WriteAllBytes(old, new byte[] { 1 });
            File.WriteAllBytes(recent, new byte[] { 2 });
            File.SetLastWriteTimeUtc(old, new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(recent, new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc));

            var sut = CreateSut(dir, new DateTime(2026, 9, 6, 12, 30, 0, DateTimeKind.Utc));
            var status = sut.GetStatus();
            Assert.True(status.Enabled);
            Assert.Equal(2, status.FileCount);
            Assert.True(sut.CoversWindow(
                new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PurgeExpired_deletes_files_older_than_retention()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"wal_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var expired = Path.Combine(dir, "old.wal");
            var keep = Path.Combine(dir, "new.wal");
            File.WriteAllBytes(expired, new byte[] { 1 });
            File.WriteAllBytes(keep, new byte[] { 2 });
            File.SetLastWriteTimeUtc(expired, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(keep, new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

            var sut = CreateSut(dir, new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc), retentionDays: 7);
            Assert.Equal(1, sut.PurgeExpired());
            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(keep));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static WalArchiveService CreateSut(string directory, DateTime utcNow, int retentionDays = 7)
    {
        var opts = new BackupOptions
        {
            WalArchiveDirectory = directory,
            WalArchiveRetentionDays = retentionDays,
            WalArchiveSwitchIntervalMinutes = 5
        };
        var monitor = new Mock<IOptionsMonitor<BackupOptions>>();
        monitor.Setup(o => o.CurrentValue).Returns(opts);
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        return new WalArchiveService(
            monitor.Object,
            env.Object,
            new FixedUtcTimeProvider(utcNow),
            NullLogger<WalArchiveService>.Instance);
    }
}

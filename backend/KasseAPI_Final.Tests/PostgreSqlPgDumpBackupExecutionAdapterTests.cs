using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.Backup.PgDump;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PostgreSqlPgDumpBackupExecutionAdapterTests
{
    private static IOptionsMonitor<BackupOptions> Monitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    [Fact]
    public async Task ExecuteAsync_when_pg_dump_succeeds_produces_logical_and_manifest_with_disk_flags()
    {
        var temp = Path.Combine(Path.GetTempPath(), "regkasse_pgdump_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var runner = new Mock<IPgDumpProcessRunner>();
            runner
                .Setup(r => r.RunAsync(It.IsAny<PgDumpProcessSpec>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PgDumpProcessSpec spec, CancellationToken _) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(spec.OutputFilePath)!);
                    // Gerçek pg_dump -Fc çıktısı PGDMP ile başlar; adapter bu imzayı doğrular.
                    File.WriteAllBytes(
                        spec.OutputFilePath,
                        [(byte)'P', (byte)'G', (byte)'D', (byte)'M', (byte)'P', 1, 2, 3, 4, 5]);
                    return new PgDumpRunResult { Success = true, ExitCode = 0 };
                });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=127.0.0.1;Port=5432;Username=u;Password=p;Database=regkasse"
                })
                .Build();

            var checksum = new BackupChecksumService();
            var adapter = new PostgreSqlPgDumpBackupExecutionAdapter(
                config,
                Monitor(new BackupOptions { ArtifactStagingRoot = temp }),
                runner.Object,
                new BackupManifestService(checksum),
                checksum,
                NullLogger<PostgreSqlPgDumpBackupExecutionAdapter>.Instance);

            var ctx = new BackupExecutionContext(
                Guid.NewGuid(),
                null,
                adapter.AdapterKind,
                default,
                "cafe",
                new DateTime(2026, 7, 3, 15, 1, 0, DateTimeKind.Utc));
            var result = await adapter.ExecuteAsync(ctx);

            Assert.True(result.Success);
            Assert.Equal(2, result.Artifacts.Count);
            var logical = result.Artifacts.Single(a => a.ArtifactType == BackupArtifactType.LogicalDump);
            Assert.Equal("backup_cafe_20260703_150100.dump", logical.StorageDescriptor);
            Assert.True(logical.RequireOnDiskHashVerification);
            Assert.True(File.Exists(Path.Combine(temp, logical.StorageDescriptor)));

            var manifest = result.Artifacts.Single(a => a.ArtifactType == BackupArtifactType.VerificationManifest);
            Assert.Equal("backup_cafe_20260703_150100_manifest.json", manifest.StorageDescriptor);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                // ignore test cleanup
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_when_pg_dump_fails_returns_no_artifacts()
    {
        var temp = Path.Combine(Path.GetTempPath(), "regkasse_pgdump_fail_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var runner = new Mock<IPgDumpProcessRunner>();
            runner
                .Setup(r => r.RunAsync(It.IsAny<PgDumpProcessSpec>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PgDumpRunResult
                {
                    Success = false,
                    ExitCode = 1,
                    StdErr = "pg_dump: error: connection refused"
                });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=127.0.0.1;Port=5432;Username=u;Password=p;Database=regkasse"
                })
                .Build();

            var checksum = new BackupChecksumService();
            var adapter = new PostgreSqlPgDumpBackupExecutionAdapter(
                config,
                Monitor(new BackupOptions { ArtifactStagingRoot = temp }),
                runner.Object,
                new BackupManifestService(checksum),
                checksum,
                NullLogger<PostgreSqlPgDumpBackupExecutionAdapter>.Instance);

            var result = await adapter.ExecuteAsync(
                new BackupExecutionContext(Guid.NewGuid(), null, "PgDump", default));

            Assert.False(result.Success);
            Assert.Empty(result.Artifacts);
            Assert.Equal("PG_DUMP_FAILED", result.ErrorCode);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_when_pg_dump_cancelled_maps_error_code()
    {
        var temp = Path.Combine(Path.GetTempPath(), "regkasse_pgdump_cancel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var runner = new Mock<IPgDumpProcessRunner>();
            runner
                .Setup(r => r.RunAsync(It.IsAny<PgDumpProcessSpec>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PgDumpRunResult
                {
                    Success = false,
                    ExitCode = -3,
                    StdErr = "cancelled"
                });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=127.0.0.1;Port=5432;Username=u;Password=p;Database=regkasse"
                })
                .Build();

            var checksum = new BackupChecksumService();
            var adapter = new PostgreSqlPgDumpBackupExecutionAdapter(
                config,
                Monitor(new BackupOptions { ArtifactStagingRoot = temp }),
                runner.Object,
                new BackupManifestService(checksum),
                checksum,
                NullLogger<PostgreSqlPgDumpBackupExecutionAdapter>.Instance);

            var result = await adapter.ExecuteAsync(
                new BackupExecutionContext(Guid.NewGuid(), null, "PgDump", default));

            Assert.False(result.Success);
            Assert.Equal("PG_DUMP_CANCELLED", result.ErrorCode);
            Assert.Empty(result.Artifacts);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_when_dump_missing_pg_custom_magic_fails_closed()
    {
        var temp = Path.Combine(Path.GetTempPath(), "regkasse_pgdump_magic_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var runner = new Mock<IPgDumpProcessRunner>();
            runner
                .Setup(r => r.RunAsync(It.IsAny<PgDumpProcessSpec>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PgDumpProcessSpec spec, CancellationToken _) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(spec.OutputFilePath)!);
                    File.WriteAllText(spec.OutputFilePath, "plain-text-not-a-dump");
                    return new PgDumpRunResult { Success = true, ExitCode = 0 };
                });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=127.0.0.1;Port=5432;Username=u;Password=p;Database=regkasse"
                })
                .Build();

            var checksum = new BackupChecksumService();
            var adapter = new PostgreSqlPgDumpBackupExecutionAdapter(
                config,
                Monitor(new BackupOptions { ArtifactStagingRoot = temp }),
                runner.Object,
                new BackupManifestService(checksum),
                checksum,
                NullLogger<PostgreSqlPgDumpBackupExecutionAdapter>.Instance);

            var result = await adapter.ExecuteAsync(
                new BackupExecutionContext(Guid.NewGuid(), null, "PgDump", default));

            Assert.False(result.Success);
            Assert.Equal("DUMP_FORMAT_INVALID", result.ErrorCode);
            Assert.Empty(result.Artifacts);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}

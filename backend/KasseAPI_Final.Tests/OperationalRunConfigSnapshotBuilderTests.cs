using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.OperationalRuns;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OperationalRunConfigSnapshotBuilderTests
{
    [Fact]
    public void Backup_snapshot_omits_filesystem_paths_and_executable_locations()
    {
        var opts = new BackupOptions
        {
            ArtifactStagingRoot = @"D:\SecretFolder\SuperSecretToken_XYZ789\staging",
            ExternalArchiveRoot = @"\\archive\share\token=abc123",
            LogicalDumpConnectionStringName = "LogicalDumpReadonly",
            PgDumpExecutablePath = @"C:\Program Files\PostgreSQL\16\bin\pg_dump.EXE",
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump
        };

        var json = OperationalRunConfigSnapshotBuilder.SerializeBackup(opts, "test_phase", DateTime.UtcNow);

        Assert.DoesNotContain("SuperSecretToken_XYZ789", json, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("pg_dump.EXE", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretFolder", json, StringComparison.Ordinal);
        Assert.Contains("LogicalDumpReadonly", json, StringComparison.Ordinal);
        Assert.Contains("\"pgDumpExecutablePathConfigured\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"artifactStagingRootConfigured\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"externalArchiveRootConfigured\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_snapshot_omits_pg_restore_executable_path_value()
    {
        var opts = new RestoreVerificationOptions
        {
            PgRestoreExecutablePath = @"C:\tools\pg_restore_secret.exe",
            DumpFallbackDepth = 12,
            FiscalValidationConnectionStringName = "FiscalCheck",
            AllowNonPgDumpBackupSource = false,
            PostRestoreSqlChecksEnabled = true
        };

        var json = OperationalRunConfigSnapshotBuilder.SerializeRestore(opts, "test_rv", DateTime.UtcNow);

        Assert.DoesNotContain("pg_restore_secret", json, StringComparison.Ordinal);
        Assert.Contains("\"pgRestoreExecutablePathConfigured\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"dumpFallbackDepth\":12", json, StringComparison.Ordinal);
        Assert.Contains("FiscalCheck", json, StringComparison.Ordinal);
        Assert.Contains("\"allowNonPgDumpBackupSource\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"postRestoreSqlChecksEnabled\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"restoredDatabaseApplicationSmokeEnabled\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"applicationSmokeProbeEnabled\":false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupRunMapper_ToDto_backward_compatible_when_config_snapshot_null()
    {
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            ConfigSnapshotJson = null
        };

        var dto = BackupRunMapper.ToDto(run, includeChildren: false, materializedChildren: false);
        Assert.Null(dto.ConfigSnapshotJson);
    }

    [Fact]
    public void RestoreVerificationRunMapper_ToDto_backward_compatible_when_config_snapshot_null()
    {
        var run = new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            RequestedAt = DateTime.UtcNow,
            ConfigSnapshotJson = null
        };

        var dto = RestoreVerificationRunMapper.ToDto(run);
        Assert.Null(dto.ConfigSnapshotJson);
    }

    [Fact]
    public void Backup_snapshot_json_is_valid_document_with_schema_version()
    {
        var json = OperationalRunConfigSnapshotBuilder.SerializeBackup(
            new BackupOptions(),
            "backup_manual_enqueue",
            new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("schemaVersion").ValueKind);
        Assert.Equal(3, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("backup_run", doc.RootElement.GetProperty("scope").GetString());
        Assert.Equal("Fake", doc.RootElement.GetProperty("configurationExecutionAdapterKind").GetString());
        Assert.Equal("InheritFromConfiguration", doc.RootElement.GetProperty("adminRuntimeExecutionMode").GetString());
        Assert.Equal("Disabled", doc.RootElement.GetProperty("retentionPolicyMode").GetString());
        Assert.False(doc.RootElement.GetProperty("retentionArtifactDeletionEnabled").GetBoolean());
    }
}

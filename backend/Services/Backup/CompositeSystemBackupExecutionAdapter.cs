using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Super Admin System strategy: <c>pg_dump</c> (restore-compatible LogicalDump) + structured
/// <c>*.system.zip</c> (GlobalsDump) with Identity, licenses, and nested tenant packages.
/// </summary>
public sealed class CompositeSystemBackupExecutionAdapter : IBackupExecutionAdapter
{
    private readonly PostgreSqlPgDumpBackupExecutionAdapter _pgDump;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ISystemScopedBackupExporter _systemExporter;
    private readonly IBackupChecksumService _checksum;
    private readonly ILogger<CompositeSystemBackupExecutionAdapter> _logger;

    public CompositeSystemBackupExecutionAdapter(
        PostgreSqlPgDumpBackupExecutionAdapter pgDump,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> options,
        ISystemScopedBackupExporter systemExporter,
        IBackupChecksumService checksum,
        ILogger<CompositeSystemBackupExecutionAdapter> logger)
    {
        _pgDump = pgDump;
        _scopeFactory = scopeFactory;
        _options = options;
        _systemExporter = systemExporter;
        _checksum = checksum;
        _logger = logger;
    }

    public string AdapterKind => "SystemComposite";

    public async Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context)
    {
        // 1) Instance-wide pg_dump — validation restore / DR primary artifact
        var dumpResult = await _pgDump.ExecuteAsync(context);
        if (!dumpResult.Success)
            return dumpResult;

        // 2) Structured Super Admin package (sketch SystemBackupData)
        var packageArtifacts = await TryBuildSystemPackageAsync(context);
        if (packageArtifacts == null)
        {
            // pg_dump succeeded; package failure is non-fatal for restore path but reported
            _logger.LogWarning(
                "System structured package failed after successful pg_dump for run {RunId}; continuing with dump only.",
                context.BackupRunId);
            return dumpResult;
        }

        var merged = dumpResult.Artifacts.Concat(packageArtifacts).ToList();
        return new BackupExecutionResult
        {
            Success = true,
            Artifacts = merged
        };
    }

    private async Task<IReadOnlyList<BackupArtifactDescriptor>?> TryBuildSystemPackageAsync(
        BackupExecutionContext context)
    {
        var opts = _options.CurrentValue;
        var root = opts.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var rootFull = Path.GetFullPath(root.Trim());
        Directory.CreateDirectory(rootFull);

        var fileNameTimestamp = context.ArtifactFileNameTimestampUtc ?? DateTime.UtcNow;
        var zipName = BackupArtifactFileNameBuilder.BuildSystemPackageFileName(fileNameTimestamp);
        var zipPath = Path.GetFullPath(Path.Combine(rootFull, zipName));
        if (!BackupPathGuard.IsPathUnderStagingRoot(zipPath, rootFull))
            return null;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var export = await _systemExporter.ExportAsync(db, zipPath, context.CancellationToken);
            var hash = await _checksum.ComputeFileSha256HexAsync(zipPath, context.CancellationToken);

            var meta = JsonSerializer.Serialize(new
            {
                kind = "system_logical_package",
                format = export.Manifest.Format,
                activeTenantCount = export.Manifest.ActiveTenantCount,
                activeTenantIds = export.Manifest.ActiveTenantIds,
                exportedAtUtc = export.Manifest.ExportedAtUtc,
                sectionRowCounts = export.Manifest.SectionRowCounts,
                includedCategories = export.Manifest.IncludedCategories,
                contentHashSha256 = hash,
                backupRunId = context.BackupRunId,
                note = "Super Admin system ZIP (Identity + all tenants). Primary restore artifact remains pg_dump LogicalDump."
            });

            _logger.LogInformation(
                "System structured backup written: runId={RunId}, bytes={Bytes}, tenants={TenantCount}",
                context.BackupRunId,
                export.ByteSize,
                export.Manifest.ActiveTenantCount);

            return new[]
            {
                new BackupArtifactDescriptor
                {
                    ArtifactType = BackupArtifactType.GlobalsDump,
                    StorageDescriptor = zipName,
                    ByteSize = export.ByteSize,
                    ContentHashSha256 = hash,
                    MetadataJson = meta,
                    RequireOnDiskHashVerification = true
                }
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            TryDelete(zipPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System structured package export failed for run {RunId}", context.BackupRunId);
            TryDelete(zipPath);
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort
        }
    }
}

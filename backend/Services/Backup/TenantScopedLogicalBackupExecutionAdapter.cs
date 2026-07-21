using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Worker adapter for <see cref="BackupStrategyKind.Tenant"/>: ZIP of tenant-filtered JSON tables.
/// Never includes AspNet Identity or platform-wide settings. Not a <c>pg_dump</c> / <c>pg_restore</c> artifact.
/// </summary>
public sealed class TenantScopedLogicalBackupExecutionAdapter : IBackupExecutionAdapter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ITenantScopedBackupExporter _exporter;
    private readonly IBackupChecksumService _checksum;
    private readonly IBackupEncryptionService _encryption;
    private readonly ILogger<TenantScopedLogicalBackupExecutionAdapter> _logger;

    public TenantScopedLogicalBackupExecutionAdapter(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> options,
        ITenantScopedBackupExporter exporter,
        IBackupChecksumService checksum,
        IBackupEncryptionService encryption,
        ILogger<TenantScopedLogicalBackupExecutionAdapter> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _exporter = exporter;
        _checksum = checksum;
        _encryption = encryption;
        _logger = logger;
    }

    public string AdapterKind => "TenantLogical";

    public async Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context)
    {
        if (context.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return Fail("TENANT_ID_REQUIRED", "Tenant strategy backup requires BackupRun.TenantId.");
        }

        var opts = _options.CurrentValue;
        var root = opts.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(root))
            return Fail("MISSING_STAGING_ROOT", "Backup:ArtifactStagingRoot is not set.");

        var rootFull = Path.GetFullPath(root.Trim());
        Directory.CreateDirectory(rootFull);

        var fileNameTimestamp = context.ArtifactFileNameTimestampUtc ?? DateTime.UtcNow;
        var isIncremental = context.IncrementalSinceUtc.HasValue;
        var zipName = isIncremental
            ? BackupArtifactFileNameBuilder.BuildTenantIncrementalPackageFileName(
                context.TenantSlugForFileName,
                fileNameTimestamp)
            : BackupArtifactFileNameBuilder.BuildTenantLogicalPackageFileName(
                context.TenantSlugForFileName,
                fileNameTimestamp);
        var zipPath = Path.GetFullPath(Path.Combine(rootFull, zipName));
        if (!BackupPathGuard.IsPathUnderStagingRoot(zipPath, rootFull))
            return Fail("PATH_ESCAPE", "Resolved tenant backup path left staging root.");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var export = await _exporter.ExportAsync(
                db,
                tenantId,
                context.TenantSlugForFileName,
                zipPath,
                context.CancellationToken,
                context.IncrementalSinceUtc);

            await _encryption.EncryptFileInPlaceAsync(zipPath, context.CancellationToken);
            var zipFi = new FileInfo(zipPath);
            var hash = await _checksum.ComputeFileSha256HexAsync(zipPath, context.CancellationToken);
            var manifestName = BackupArtifactFileNameBuilder.BuildManifestFileName(
                context.TenantSlugForFileName,
                fileNameTimestamp);
            var manifestPath = Path.Combine(rootFull, manifestName);

            var manifestPayload = new
            {
                kind = isIncremental ? "tenant_incremental_package" : "tenant_logical_package",
                format = export.Manifest.Format,
                tenantId = export.Manifest.TenantId,
                tenantSlug = export.Manifest.TenantSlug,
                exportedAtUtc = export.Manifest.ExportedAtUtc,
                tableRowCounts = export.Manifest.TableRowCounts,
                excludedCategories = export.Manifest.ExcludedCategories,
                contentHashSha256 = hash,
                backupRunId = context.BackupRunId,
                incrementalSinceUtc = context.IncrementalSinceUtc,
                encrypted = _encryption.IsEnabled,
                note = isIncremental
                    ? "Tenant incremental JSON ZIP — delta only; not a standalone restore source. Validation restore uses System dumps only."
                    : "Tenant-scoped JSON ZIP — not pg_dump; validation restore uses System dumps only."
            };
            var manifestJson = JsonSerializer.Serialize(manifestPayload);
            await File.WriteAllTextAsync(manifestPath, manifestJson, context.CancellationToken);
            await _encryption.EncryptFileInPlaceAsync(manifestPath, context.CancellationToken);
            var manifestHash = await _checksum.ComputeFileSha256HexAsync(manifestPath, context.CancellationToken);
            var manifestFi = new FileInfo(manifestPath);

            _logger.LogInformation(
                "Tenant logical backup written: runId={RunId}, tenantId={TenantId}, bytes={Bytes}, tables={TableCount}, encrypted={Encrypted}, incremental={Incremental}",
                context.BackupRunId,
                tenantId,
                zipFi.Length,
                export.Manifest.TableRowCounts.Count,
                _encryption.IsEnabled,
                isIncremental);

            return new BackupExecutionResult
            {
                Success = true,
                Artifacts = new[]
                {
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = zipName,
                        ByteSize = zipFi.Length,
                        ContentHashSha256 = hash,
                        MetadataJson = JsonSerializer.Serialize(new
                        {
                            packageFormat = export.Manifest.Format,
                            strategy = nameof(BackupStrategyKind.Tenant),
                            packageKind = isIncremental
                                ? BackupIncrementalPackageMetadata.PackageKindIncremental
                                : BackupIncrementalPackageMetadata.PackageKindFull,
                            tenantId,
                            adapterKind = AdapterKind,
                            incrementalSinceUtc = context.IncrementalSinceUtc,
                            encrypted = _encryption.IsEnabled
                        }),
                        RequireOnDiskHashVerification = true
                    },
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.VerificationManifest,
                        StorageDescriptor = manifestName,
                        ByteSize = manifestFi.Length,
                        ContentHashSha256 = manifestHash,
                        MetadataJson = isIncremental
                            ? "{\"kind\":\"tenant-incremental-manifest\"}"
                            : "{\"kind\":\"tenant-logical-manifest\"}",
                        RequireOnDiskHashVerification = true
                    }
                }
            };
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            TryDelete(zipPath);
            return Fail("TENANT_BACKUP_CANCELLED", "Tenant logical backup cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant logical backup failed for run {RunId}", context.BackupRunId);
            TryDelete(zipPath);
            return Fail("TENANT_BACKUP_FAILED", ex.Message);
        }
    }

    private static BackupExecutionResult Fail(string code, string detail) =>
        new() { Success = false, ErrorCode = code, ErrorDetail = detail };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Admin backup run read model: size, duration, and optional original-database size for ratio display.
/// </summary>
public sealed class BackupRunService : IBackupRunService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BackupRunService> _logger;

    public BackupRunService(
        AppDbContext db,
        IConfiguration configuration,
        IOptionsMonitor<BackupOptions> backupOptions,
        IHostEnvironment hostEnvironment,
        ILogger<BackupRunService> logger)
    {
        _db = db;
        _configuration = configuration;
        _backupOptions = backupOptions;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<BackupRunResponseDto?> GetBackupRunAsync(
        Guid runId,
        BackupRunDtoMappingOptions mappingOptions,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .Include(r => r.Verifications)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        if (run == null)
            return null;

        var estimatedOriginalBytes = await EstimateOriginalDumpSizeAsync(run, cancellationToken);

        return BackupRunMapper.ToDto(
            run,
            includeChildren: true,
            pipelinePolicy: mappingOptions.PipelinePolicy,
            materializedChildren: true,
            automaticRetryMaxAttemptsBudget: mappingOptions.AutomaticRetryMaxAttemptsBudget,
            downloadEnrichment: mappingOptions.DownloadEnrichment,
            estimatedOriginalDatabaseBytes: estimatedOriginalBytes > 0 ? estimatedOriginalBytes : null);
    }

    public async Task<IReadOnlyList<BackupListItemResponseDto>> GetBackupListAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var runQuery = _db.BackupRuns.AsNoTracking()
            .Where(r => r.Status == BackupRunStatus.Succeeded);
        runQuery = BackupRunTenantSlugResolver.ApplyTenantHint(runQuery, tenantId);

        var rows = await (
                from artifact in _db.BackupArtifacts.AsNoTracking()
                join run in runQuery on artifact.BackupRunId equals run.Id
                where artifact.ArtifactType == BackupArtifactType.LogicalDump
                orderby artifact.CreatedAt descending
                select new { artifact, run })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<BackupListItemResponseDto>();

        var runs = rows.Select(x => x.run).DistinctBy(r => r.Id).ToList();
        var runIds = runs.Select(r => r.Id).ToList();
        var slugByTenantId = await BackupRunTenantSlugResolver.LoadSlugByTenantIdAsync(
            runs,
            _db,
            cancellationToken);
        var manifests = await _db.BackupArtifacts.AsNoTracking()
            .Where(a => runIds.Contains(a.BackupRunId)
                        && a.ArtifactType == BackupArtifactType.VerificationManifest)
            .ToListAsync(cancellationToken);
        var manifestByRunId = manifests
            .GroupBy(m => m.BackupRunId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

        var opts = _backupOptions.CurrentValue;

        return rows.Select(row =>
        {
            var artifact = row.artifact;
            var run = row.run;
            var tenantSlug = BackupRunTenantSlugResolver.ResolveSlug(run, slugByTenantId);
            var fileName = ResolveArtifactFileName(
                artifact,
                tenantSlug,
                BackupArtifactType.LogicalDump);

            var fileOnDisk = BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                run.Id,
                artifact,
                opts,
                _logger,
                _hostEnvironment,
                "Backup list: download availability",
                out _);

            manifestByRunId.TryGetValue(run.Id, out var manifest);
            string? manifestFileName = null;
            string? manifestDownloadUrl = null;
            Guid? manifestArtifactId = null;
            long? manifestFileSize = null;
            if (manifest != null)
            {
                manifestArtifactId = manifest.Id;
                manifestFileName = ResolveArtifactFileName(manifest, tenantSlug, BackupArtifactType.VerificationManifest);
                manifestFileSize = manifest.ByteSize;
                if (BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                        run.Id,
                        manifest,
                        opts,
                        _logger,
                        _hostEnvironment,
                        "Backup list: manifest download availability",
                        out _))
                {
                    manifestDownloadUrl = BuildArtifactDownloadUrl(run.Id, manifest.Id);
                }
            }

            return new BackupListItemResponseDto
            {
                BackupRunId = run.Id,
                ArtifactId = artifact.Id,
                FileName = fileName,
                FileSize = artifact.ByteSize,
                CreatedAt = artifact.CreatedAt,
                TenantSlug = tenantSlug,
                IsFake = IsSimulatedAdapter(run.AdapterKind),
                DownloadUrl = fileOnDisk
                    ? BuildArtifactDownloadUrl(run.Id, artifact.Id)
                    : null,
                ManifestArtifactId = manifestArtifactId,
                ManifestFileName = manifestFileName,
                ManifestFileSize = manifestFileSize,
                ManifestDownloadUrl = manifestDownloadUrl
            };
        }).ToList();
    }

    private static string ResolveArtifactFileName(
        BackupArtifact artifact,
        string tenantSlug,
        BackupArtifactType artifactType)
    {
        var fromDescriptor = Path.GetFileName(artifact.StorageDescriptor.Trim());
        if (!string.IsNullOrEmpty(fromDescriptor))
            return fromDescriptor;

        return artifactType == BackupArtifactType.VerificationManifest
            ? BackupArtifactFileNameBuilder.BuildManifestFileName(tenantSlug, artifact.CreatedAt)
            : BackupArtifactFileNameBuilder.BuildLogicalDumpFileName(tenantSlug, artifact.CreatedAt);
    }

    internal static string BuildArtifactDownloadUrl(Guid backupRunId, Guid artifactId) =>
        $"/api/admin/backup/runs/{backupRunId:D}/artifacts/{artifactId:D}/download";

    private static bool IsSimulatedAdapter(string? adapterKind) =>
        BackupCompletenessSuccessPolicy.TryParseAdapterKind(adapterKind, out var kind)
        && (kind == BackupExecutionAdapterKind.Fake || kind == BackupExecutionAdapterKind.ProductionStub);

    /// <summary>
    /// Best-effort original size: artifact metadata first, then live <c>pg_database_size</c> for PgDump runs.
    /// </summary>
    internal async Task<long> EstimateOriginalDumpSizeAsync(BackupRun run, CancellationToken cancellationToken)
    {
        if (BackupRunMetricsFormatter.TryGetOriginalByteSizeFromArtifacts(run.Artifacts, out var fromMetadata))
            return fromMetadata;

        if (!BackupCompletenessSuccessPolicy.TryParseAdapterKind(run.AdapterKind, out var adapterKind)
            || adapterKind != BackupExecutionAdapterKind.PgDump)
        {
            return 0;
        }

        try
        {
            var opts = _backupOptions.CurrentValue;
            var connName = string.IsNullOrWhiteSpace(opts.LogicalDumpConnectionStringName)
                ? "DefaultConnection"
                : opts.LogicalDumpConnectionStringName.Trim();
            var connStr = _configuration.GetConnectionString(connName);
            if (string.IsNullOrWhiteSpace(connStr))
                return 0;

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT pg_database_size(current_database())", conn);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is long size && size > 0)
                return size;
            if (result is int i && i > 0)
                return i;
            if (result is decimal d && d > 0)
                return (long)d;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "EstimateOriginalDumpSizeAsync failed for backup run {RunId} (adapter {AdapterKind})",
                run.Id,
                run.AdapterKind);
        }

        return 0;
    }
}

using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly ILogger<BackupRunService> _logger;

    public BackupRunService(
        AppDbContext db,
        IConfiguration configuration,
        IOptionsMonitor<BackupOptions> backupOptions,
        ILogger<BackupRunService> logger)
    {
        _db = db;
        _configuration = configuration;
        _backupOptions = backupOptions;
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

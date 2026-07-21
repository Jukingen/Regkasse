using System.Data;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Detailed backup verification report: logical dump TOC vs live database row counts for monitored tables.
/// Row counts in <see cref="BackupTableStatisticsDto"/> reflect the live database at report generation time
/// (custom-format dumps do not embed per-table row counts without a restore).
/// </summary>
public sealed class BackupVerificationReportService : IBackupVerificationReportService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IPgRestoreListInspector _pgRestoreList;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BackupVerificationReportService> _logger;
    private readonly TimeProvider _timeProvider;

    public BackupVerificationReportService(
        AppDbContext db,
        IOptionsMonitor<BackupOptions> backupOptions,
        IPgRestoreListInspector pgRestoreList,
        IHostEnvironment hostEnvironment,
        ILogger<BackupVerificationReportService> logger,
        TimeProvider timeProvider)
    {
        _db = db;
        _backupOptions = backupOptions;
        _pgRestoreList = pgRestoreList;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<BackupVerificationReportDto> GenerateReportAsync(
        Guid backupRunId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .FirstOrDefaultAsync(r => r.Id == backupRunId, cancellationToken);

        if (run == null)
            throw new KeyNotFoundException($"Backup run {backupRunId} was not found.");

        var artifacts = run.Artifacts?.ToList() ?? new List<BackupArtifact>();
        var totalSize = artifacts.Sum(a => a.ByteSize ?? 0);
        var generatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        var sourceStats = await GetSourceDatabaseStatisticsAsync(cancellationToken);
        var monitoredNames = ResolveMonitoredTableNames(sourceStats.Tables);

        var dumpTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logicalDumpAnalyzed = false;
        string? logicalDumpMessage = null;

        var logicalDump = artifacts.FirstOrDefault(a => a.ArtifactType == BackupArtifactType.LogicalDump);
        if (logicalDump == null)
        {
            logicalDumpMessage = "No logical dump artifact on this run.";
        }
        else if (!BackupArtifactOnDiskResolver.TryResolveForSingleRun(
                     run.Id,
                     logicalDump,
                     _backupOptions.CurrentValue,
                     _logger,
                     _hostEnvironment,
                     "backup_verification_report",
                     out var dumpPath))
        {
            logicalDumpMessage = "Logical dump file is not available on disk (staging or external archive).";
        }
        else
        {
            var catalog = await _pgRestoreList.ReadTableDataCatalogAsync(dumpPath!, cancellationToken);
            logicalDumpAnalyzed = catalog.Success;
            logicalDumpMessage = catalog.Success
                ? $"Parsed {catalog.TableDataEntries.Count} TABLE DATA entries from pg_restore --list."
                : catalog.StdErrSnippet ?? $"pg_restore --list failed (exit {catalog.ExitCode}).";

            foreach (var entry in catalog.TableDataEntries)
                dumpTableNames.Add(entry.TableName);
        }

        var sourceByTable = sourceStats.Tables
            .Where(t => t.TableExists)
            .ToDictionary(t => t.TableName, t => t.RowCount, StringComparer.OrdinalIgnoreCase);

        var tableStatistics = new List<BackupTableStatisticsDto>();
        foreach (var tableName in monitoredNames)
        {
            var sourceRow = sourceByTable.GetValueOrDefault(tableName);
            var sourceSize = sourceStats.Tables.FirstOrDefault(t =>
                string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase))?.EstimatedSizeBytes ?? 0;
            var inDump = dumpTableNames.Contains(tableName);
            var presentInDump = logicalDumpAnalyzed && inDump;

            string? message;
            bool isVerified;
            if (!logicalDumpAnalyzed)
            {
                isVerified = false;
                message = logicalDumpMessage ?? "Logical dump could not be analyzed.";
            }
            else if (!inDump)
            {
                isVerified = false;
                message = "Table data section not found in logical dump TOC.";
            }
            else
            {
                isVerified = true;
                message =
                    "Table present in logical dump; row count is live database at report time (dump format has no embedded row counts).";
            }

            tableStatistics.Add(new BackupTableStatisticsDto
            {
                SchemaName = "public",
                TableName = tableName,
                RowCount = sourceRow,
                EstimatedSizeBytes = sourceSize,
                PresentInLogicalDump = presentInDump,
                IsVerified = isVerified,
                VerificationMessage = message,
            });
        }

        var dumpBackedCounts = tableStatistics
            .Where(t => t.PresentInLogicalDump)
            .ToDictionary(t => t.TableName, t => t.RowCount, StringComparer.OrdinalIgnoreCase);

        var score = BackupVerificationReportScorer.CalculateScore(
            logicalDumpAnalyzed,
            monitoredNames,
            dumpTableNames,
            sourceByTable,
            dumpBackedCounts);

        return new BackupVerificationReportDto
        {
            BackupRunId = backupRunId,
            GeneratedAtUtc = generatedAt,
            BackupCompletedAtUtc = run.CompletedAt,
            ArtifactCount = artifacts.Count,
            TotalSizeBytes = totalSize,
            TotalSizeFormatted = BackupRunMetricsFormatter.FormatBytes(totalSize) ?? "0 B",
            LogicalDumpAnalyzed = logicalDumpAnalyzed,
            LogicalDumpAnalysisMessage = logicalDumpMessage,
            TableStatistics = tableStatistics,
            SourceStatistics = sourceStats,
            VerificationScore = score,
            Status = BackupVerificationReportScorer.MapStatus(score),
        };
    }

    private static IReadOnlyList<string> ResolveMonitoredTableNames(IReadOnlyList<BackupTableRowCountDto> existingTables)
    {
        var existing = existingTables
            .Where(t => t.TableExists)
            .Select(t => t.TableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var list = new List<string>();
        foreach (var preferred in PgRestoreListTableDataParser.PreferredMonitoredTableNames)
        {
            if (existing.Contains(preferred))
                list.Add(preferred);
        }

        return list;
    }

    private async Task<BackupSourceDatabaseStatisticsDto> GetSourceDatabaseStatisticsAsync(
        CancellationToken cancellationToken)
    {
        var analyzedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var tables = new List<BackupTableRowCountDto>();
        var existing = await ListExistingPublicTablesAsync(cancellationToken);
        var namesToScan = PgRestoreListTableDataParser.PreferredMonitoredTableNames
            .Where(existing.Contains)
            .ToList();

        await using var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        foreach (var tableName in namesToScan)
        {
            var (rowCount, sizeBytes, exists) = await ReadTableMetricsAsync(conn, tableName, cancellationToken);
            tables.Add(new BackupTableRowCountDto
            {
                SchemaName = "public",
                TableName = tableName,
                RowCount = rowCount,
                EstimatedSizeBytes = sizeBytes,
                TableExists = exists,
            });
        }

        return new BackupSourceDatabaseStatisticsDto
        {
            AnalyzedAtUtc = analyzedAt,
            Tables = tables,
            TotalRowCount = tables.Sum(t => t.RowCount),
        };
    }

    private async Task<HashSet<string>> ListExistingPublicTablesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            """;

        await using var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
            set.Add(reader.GetString(0));
        return set;
    }

    private static async Task<(long RowCount, long SizeBytes, bool Exists)> ReadTableMetricsAsync(
        NpgsqlConnection conn,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var countCmd = new NpgsqlCommand(
            $"SELECT COUNT(*)::bigint FROM public.{QuotePgIdentifier(tableName)}",
            conn);
        var countObj = await countCmd.ExecuteScalarAsync(cancellationToken);
        var rowCount = countObj is long l ? l : Convert.ToInt64(countObj);

        await using var sizeCmd = new NpgsqlCommand(
            "SELECT pg_total_relation_size(format('%I.%I', 'public', @name)::regclass)::bigint",
            conn);
        sizeCmd.Parameters.AddWithValue("name", tableName);
        var sizeObj = await sizeCmd.ExecuteScalarAsync(cancellationToken);
        var sizeBytes = sizeObj is long s ? s : Convert.ToInt64(sizeObj);

        return (rowCount, sizeBytes, true);
    }

    private static string QuotePgIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

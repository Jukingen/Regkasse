using System.Security.Cryptography;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface IDepExportArchiveService
{
    /// <summary>
    /// Archives a completed DEP history row. When <paramref name="exportJson"/> is set and
    /// <see cref="DepExportHistory.StoragePath"/> is missing, writes JSON directly into the archive tree.
    /// </summary>
    Task<DepExportArchiveResult> ArchiveExportAsync(
        Guid exportId,
        string? exportJson = null,
        CancellationToken cancellationToken = default);

    Task<DepExportArchiveReport> GetArchiveReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<DepExportPurgeResult> PurgeOldExportsAsync(
        int? retentionYears = null,
        CancellationToken cancellationToken = default);

    /// <summary>Archives completed history rows that still have an on-disk storage file but no archive.</summary>
    Task<int> ArchivePendingExportsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Copies completed DEP export JSON into a 7-year retention archive tree under App_Data.
/// Targets <see cref="DepExportHistory"/> — not cron <see cref="DepExportSchedule"/>.
/// </summary>
public sealed class DepExportArchiveService : IDepExportArchiveService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<DepExportArchiveOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly IDepExportAuditService _audit;
    private readonly ILogger<DepExportArchiveService> _logger;

    public DepExportArchiveService(
        AppDbContext db,
        IOptionsMonitor<DepExportArchiveOptions> options,
        IHostEnvironment env,
        IDepExportAuditService audit,
        ILogger<DepExportArchiveService> logger)
    {
        _db = db;
        _options = options;
        _env = env;
        _audit = audit;
        _logger = logger;
    }

    public async Task<DepExportArchiveResult> ArchiveExportAsync(
        Guid exportId,
        string? exportJson = null,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return DepExportArchiveResult.Fail(exportId, "DEP export archiving is disabled.");

        var export = await _db.DepExportHistories
            .FirstOrDefaultAsync(h => h.Id == exportId, cancellationToken)
            .ConfigureAwait(false);

        if (export is null)
            return DepExportArchiveResult.Fail(exportId, "Export not found");

        if (!string.Equals(export.Status, DepExportStatus.Completed.ToString(), StringComparison.Ordinal))
            return DepExportArchiveResult.Fail(exportId, "Only completed exports can be archived.");

        if (export.PurgedAt.HasValue)
            return DepExportArchiveResult.Fail(exportId, "Export archive was already purged.");

        if (export.ArchivedAt.HasValue &&
            !string.IsNullOrWhiteSpace(export.ArchivePath) &&
            File.Exists(export.ArchivePath) &&
            !string.IsNullOrWhiteSpace(export.ArchiveChecksum))
        {
            return DepExportArchiveResult.Ok(
                exportId,
                export.ArchivePath,
                export.ArchiveChecksum,
                export.ArchivedAt.Value,
                export.RetentionUntil ?? export.ArchivedAt.Value.AddYears(Math.Max(1, opts.RetentionYears)),
                alreadyArchived: true);
        }

        try
        {
            var retentionYears = Math.Max(1, opts.RetentionYears);
            var year = export.FromUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(export.FromUtc, DateTimeKind.Utc).Year
                : export.FromUtc.ToUniversalTime().Year;

            var archiveDir = Path.Combine(
                ResolveArchiveRoot(opts),
                export.TenantId.ToString("D"),
                year.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(archiveDir);

            var safeName = SanitizeFileName(export.FileName, export.Id);
            var archiveFile = Path.Combine(archiveDir, safeName);

            if (!string.IsNullOrWhiteSpace(export.StoragePath) && File.Exists(export.StoragePath))
            {
                File.Copy(export.StoragePath, archiveFile, overwrite: true);
            }
            else if (!string.IsNullOrWhiteSpace(exportJson))
            {
                await File.WriteAllTextAsync(archiveFile, exportJson, cancellationToken)
                    .ConfigureAwait(false);
                // Keep download path available for history UI when original storage was never set.
                if (string.IsNullOrWhiteSpace(export.StoragePath))
                    export.StoragePath = archiveFile;
            }
            else
            {
                return DepExportArchiveResult.Fail(
                    exportId,
                    "Export JSON not available (no stored file and no in-memory payload).");
            }

            var checksum = await CalculateChecksumAsync(archiveFile, cancellationToken)
                .ConfigureAwait(false);
            var archivedAt = DateTime.UtcNow;

            export.ArchivedAt = archivedAt;
            export.ArchivePath = archiveFile;
            export.ArchiveChecksum = checksum;
            export.RetentionUntil = archivedAt.AddYears(retentionYears);
            export.PurgedAt = null;
            export.PurgeReason = null;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "DEP export archived id={ExportId} path={ArchivePath} checksum={Checksum}",
                export.Id,
                archiveFile,
                checksum);

            try
            {
                await _audit.LogExportActionAsync(
                        new DepExportAuditEntry
                        {
                            TenantId = export.TenantId,
                            Action = DepExportAuditActions.Archived,
                            ExportName = export.FileName,
                            ExportHistoryId = export.Id,
                            UserId = export.ExportedByUserId,
                            Details = $"path={archiveFile}; checksum={checksum}; retentionUntil={export.RetentionUntil:O}",
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception auditEx) when (auditEx is not OperationCanceledException)
            {
                _logger.LogWarning(auditEx, "DEP archive audit log failed for {ExportId}", export.Id);
            }

            return DepExportArchiveResult.Ok(
                exportId,
                archiveFile,
                checksum,
                archivedAt,
                export.RetentionUntil.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEP export archive failed for history {ExportId}", exportId);
            return DepExportArchiveResult.Fail(exportId, $"Archive error: {ex.Message}");
        }
    }

    public async Task<DepExportArchiveReport> GetArchiveReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var rows = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Status == DepExportStatus.Completed.ToString())
            .OrderByDescending(h => h.ExportedAt)
            .Take(100)
            .Select(h => new
            {
                h.Id,
                h.CashRegisterId,
                h.FileName,
                h.ExportedAt,
                h.FileSizeBytes,
                h.ArchivedAt,
                h.ArchivePath,
                h.ArchiveChecksum,
                h.RetentionUntil,
                h.PurgedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeArchived = rows.Where(r => r.ArchivedAt != null && r.PurgedAt == null).ToList();
        var archived = activeArchived.Count;
        var purged = rows.Count(r => r.PurgedAt != null);
        var pending = rows.Count(r => r.ArchivedAt == null && r.PurgedAt == null);

        return new DepExportArchiveReport
        {
            TenantId = tenantId,
            GeneratedAtUtc = DateTime.UtcNow,
            TotalCompletedExports = rows.Count,
            ArchivedCount = archived,
            PendingArchiveCount = pending,
            PurgedCount = purged,
            RetentionYears = Math.Max(1, opts.RetentionYears),
            TotalArchivedSizeBytes = activeArchived.Sum(r => r.FileSizeBytes),
            OldestArchivedExportAt = activeArchived.Count > 0
                ? activeArchived.Min(r => r.ExportedAt)
                : null,
            Recent = rows.Select(r => new DepExportArchiveSummaryItem
            {
                ExportId = r.Id,
                CashRegisterId = r.CashRegisterId,
                FileName = r.FileName,
                ExportedAt = r.ExportedAt,
                FileSizeBytes = r.FileSizeBytes,
                ArchivedAt = r.ArchivedAt,
                RetentionUntil = r.RetentionUntil,
                PurgedAt = r.PurgedAt,
                ArchiveChecksum = r.ArchiveChecksum,
                HasArchiveFile = !string.IsNullOrWhiteSpace(r.ArchivePath) &&
                                 r.PurgedAt == null &&
                                 File.Exists(r.ArchivePath),
            }).ToList(),
        };
    }

    public async Task<DepExportPurgeResult> PurgeOldExportsAsync(
        int? retentionYears = null,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var years = Math.Max(1, retentionYears ?? opts.RetentionYears);
        var now = DateTime.UtcNow;
        var cutoff = now.AddYears(-years);

        var result = new DepExportPurgeResult { CutoffUtc = cutoff };

        if (!opts.Enabled || !opts.PurgeEnabled)
            return result;

        // IgnoreQueryFilters: hosted sweep must see all tenants.
        var oldExports = await _db.DepExportHistories
            .IgnoreQueryFilters()
            .Where(e =>
                e.ArchivedAt != null &&
                e.PurgedAt == null &&
                e.ArchivedAt < cutoff &&
                e.RetentionUntil != null &&
                e.RetentionUntil < now)
            .OrderBy(e => e.RetentionUntil)
            .Take(Math.Clamp(opts.MaxBatchSize, 1, 500))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        result.ExaminedCount = oldExports.Count;

        foreach (var export in oldExports)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(export.ArchivePath) && File.Exists(export.ArchivePath))
                    File.Delete(export.ArchivePath);

                // Hot storage copy (scheduler temp / linked archive) may still exist.
                if (!string.IsNullOrWhiteSpace(export.StoragePath) &&
                    !string.Equals(export.StoragePath, export.ArchivePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(export.StoragePath))
                {
                    try
                    {
                        File.Delete(export.StoragePath);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Could not delete DEP storage file for purged history {ExportId}",
                            export.Id);
                    }
                }

                export.PurgedAt = DateTime.UtcNow;
                export.PurgeReason = $"Retention period expired ({years} years)";
                result.PurgedCount++;

                try
                {
                    await _audit.LogExportActionAsync(
                            new DepExportAuditEntry
                            {
                                TenantId = export.TenantId,
                                Action = DepExportAuditActions.Deleted,
                                ExportName = export.FileName,
                                ExportHistoryId = export.Id,
                                UserId = "system-purge",
                                UserRole = "System",
                                Details = export.PurgeReason,
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception auditEx) when (auditEx is not OperationCanceledException)
                {
                    _logger.LogWarning(auditEx, "DEP purge audit log failed for {ExportId}", export.Id);
                }
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                _logger.LogWarning(ex, "Failed to purge DEP archive for history {ExportId}", export.Id);
            }
        }

        if (result.PurgedCount > 0 || result.FailedCount > 0)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async Task<int> ArchivePendingExportsAsync(CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled || !opts.AutoArchiveOnComplete)
            return 0;

        var pending = await _db.DepExportHistories
            .IgnoreQueryFilters()
            .Where(h =>
                h.Status == DepExportStatus.Completed.ToString() &&
                h.ArchivedAt == null &&
                h.PurgedAt == null &&
                h.StoragePath != null &&
                h.StoragePath != "")
            .OrderBy(h => h.ExportedAt)
            .Take(Math.Clamp(opts.MaxBatchSize, 1, 500))
            .Select(h => h.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var archived = 0;
        foreach (var id in pending)
        {
            // Re-load tracked entity per id (IgnoreQueryFilters list may be large).
            var result = await ArchiveExportAsync(id, exportJson: null, cancellationToken)
                .ConfigureAwait(false);
            if (result.Success)
                archived++;
        }

        return archived;
    }

    private string ResolveArchiveRoot(DepExportArchiveOptions opts)
    {
        var configured = string.IsNullOrWhiteSpace(opts.ArchiveRootRelativeDirectory)
            ? "App_Data/dep-export-archives"
            : opts.ArchiveRootRelativeDirectory.Trim();

        if (Path.IsPathRooted(configured))
            return configured;

        return Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }

    private static string SanitizeFileName(string? fileName, Guid exportId)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            return $"dep-export_{exportId:N}.json";

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    internal static async Task<string> CalculateChecksumAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

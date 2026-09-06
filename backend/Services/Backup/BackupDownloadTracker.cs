using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.Constants;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Records backup downloads: <c>download_history</c>, run counter, and <see cref="AuditEventType.BackupDownloaded"/>.
/// </summary>
public sealed class BackupDownloadTracker : IBackupDownloadTracker
{
    public const string SourceKind = "backup";

    private readonly AppDbContext _db;
    private readonly IDownloadHistoryService _history;
    private readonly IAuditLogService _audit;
    private readonly ILogger<BackupDownloadTracker> _logger;

    public BackupDownloadTracker(
        AppDbContext db,
        IDownloadHistoryService history,
        IAuditLogService audit,
        ILogger<BackupDownloadTracker> logger)
    {
        _db = db;
        _history = history;
        _audit = audit;
        _logger = logger;
    }

    public async Task TrackAsync(BackupDownloadTrackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Run);

        var historyTenantId = request.Run.TenantId is Guid tid && tid != Guid.Empty
            ? tid
            : request.AuditTenantId is Guid auditTid && auditTid != Guid.Empty
                ? auditTid
                : SystemTenantIds.Platform;

        var ext = Path.GetExtension(request.FileName);
        var fileType = string.IsNullOrWhiteSpace(ext) ? "bin" : ext.TrimStart('.').ToLowerInvariant();

        try
        {
            await _history.RecordAsync(
                    new DownloadHistoryRecordRequest
                    {
                        TenantId = historyTenantId,
                        UserId = request.UserId,
                        FileName = request.FileName,
                        FileType = fileType,
                        FileSize = request.FileSizeBytes,
                        DownloadUrl = $"/api/admin/backup/runs/{request.Run.Id:D}/download",
                        IpAddress = request.IpAddress,
                        UserAgent = request.UserAgent,
                        SourceKind = SourceKind,
                        SourceId = request.Run.Id
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record backup download history for run {RunId}", request.Run.Id);
        }

        try
        {
            var run = await _db.BackupRuns
                .FirstOrDefaultAsync(r => r.Id == request.Run.Id, cancellationToken)
                .ConfigureAwait(false);
            if (run != null)
            {
                run.DownloadCount = Math.Max(0, run.DownloadCount) + 1;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment backup download count for run {RunId}", request.Run.Id);
        }

        var isSystem = request.Run.Strategy == BackupStrategyKind.System;
        try
        {
            await _audit.LogSystemOperationAsync(
                    action: "BACKUP_DOWNLOADED",
                    entityType: "BackupRun",
                    userId: request.UserId,
                    userRole: request.UserRole,
                    description: $"Backup downloaded (run={request.Run.Id}, artifact={request.ArtifactId}).",
                    status: AuditLogStatus.Success,
                    requestData: new
                    {
                        backupRunId = request.Run.Id,
                        artifactId = request.ArtifactId,
                        strategy = request.Run.Strategy.ToString()
                    },
                    responseData: new { downloadFileName = request.FileName },
                    correlationIdOverride: request.CorrelationId,
                    actionType: isSystem ? AuditEventType.SystemBackupDownloaded : AuditEventType.BackupDownloaded,
                    entityId: request.Run.Id,
                    tenantId: request.AuditTenantId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit backup download for run {RunId}", request.Run.Id);
        }
    }
}

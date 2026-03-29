using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupManualTriggerService : IBackupManualTriggerService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupAlertPublisher _alerts;
    private readonly ILogger<BackupManualTriggerService> _logger;

    public BackupManualTriggerService(
        AppDbContext db,
        IAuditLogService audit,
        IOptionsMonitor<BackupOptions> options,
        IBackupAlertPublisher alerts,
        ILogger<BackupManualTriggerService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
        _options = options;
        _alerts = alerts;
    }

    public async Task<BackupManualTriggerOutcome> RequestManualBackupAsync(
        string? requestedByUserId,
        string requestedByRole,
        string? idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var adapterKind = _options.CurrentValue.ExecutionAdapterKind.ToString();

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.BackupRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing != null)
            {
                _logger.LogInformation(
                    "Backup manual trigger idempotent hit: key={Key}, runId={RunId}",
                    idempotencyKey,
                    existing.Id);
                return new BackupManualTriggerOutcome
                {
                    Run = existing,
                    Kind = BackupManualTriggerResultKind.IdempotentReplay
                };
            }
        }

        var activeManual = await _db.BackupRuns.AsNoTracking()
            .Where(r => r.TriggerSource == BackupTriggerSource.Manual)
            .Where(r =>
                r.Status == BackupRunStatus.Queued
                || r.Status == BackupRunStatus.Running
                || r.Status == BackupRunStatus.AwaitingVerification)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeManual != null)
        {
            _logger.LogInformation(
                "Backup manual trigger suppressed — active run exists: runId={RunId}",
                activeManual.Id);
            _alerts.Publish(new BackupAlertEvent(
                BackupAlertKind.DuplicateExecutionPrevented,
                activeManual.Id,
                correlationId,
                "Manual backup already queued or running.",
                new Dictionary<string, string>
                {
                    ["activeRunId"] = activeManual.Id.ToString(),
                    ["activeRunStatus"] = activeManual.Status.ToString()
                }));
            return new BackupManualTriggerOutcome
            {
                Run = activeManual,
                Kind = BackupManualTriggerResultKind.DuplicateActiveManualPrevented
            };
        }

        var run = new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = adapterKind,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            RequestedByUserId = requestedByUserId,
            RequestedAt = DateTime.UtcNow,
            QueuedAt = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _db.BackupRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var actorId = requestedByUserId ?? "system";
        await _audit.LogSystemOperationAsync(
            action: "BACKUP_MANUAL_ENQUEUED",
            entityType: "BackupRun",
            userId: actorId,
            userRole: requestedByRole,
            description: $"Manual backup run {run.Id} enqueued (adapter={adapterKind}).",
            notes: null,
            status: AuditLogStatus.Success,
            errorDetails: null,
            requestData: new { run.Id, run.IdempotencyKey, correlationId },
            responseData: new { run.Status },
            correlationIdOverride: correlationId);

        return new BackupManualTriggerOutcome { Run = run, Kind = BackupManualTriggerResultKind.NewRunQueued };
    }
}

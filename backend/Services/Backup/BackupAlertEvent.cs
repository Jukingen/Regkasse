using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Alerting contract — Phase 1 logs only; swap <see cref="IBackupAlertPublisher"/> for webhook/email later.
/// </summary>
public sealed record BackupAlertEvent(
    BackupAlertKind Kind,
    Guid? BackupRunId,
    string? CorrelationId,
    string Message,
    IReadOnlyDictionary<string, string>? Data = null);

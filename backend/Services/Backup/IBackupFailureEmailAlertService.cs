namespace KasseAPI_Final.Services.Backup;

/// <summary>German ops email for terminal backup failures (SMTP; no-op when not configured).</summary>
public interface IBackupFailureEmailAlertService
{
    /// <summary>
    /// Sends a plain-text alert to configured ops recipients.
    /// Never throws — delivery failures are logged only.
    /// </summary>
    Task SendFailureAlertAsync(
        string tenantSlug,
        string error,
        Guid? backupRunId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}

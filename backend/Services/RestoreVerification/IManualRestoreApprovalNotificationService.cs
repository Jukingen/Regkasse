namespace KasseAPI_Final.Services.RestoreVerification;

public interface IManualRestoreApprovalNotificationService
{
    /// <summary>Sends approval token to second Super Admin(s) via email (SMS hook reserved).</summary>
    Task<int> SendApprovalTokenAsync(
        IReadOnlyList<string> approverEmails,
        string approvalToken,
        ManualRestoreApprovalNotificationContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ManualRestoreApprovalNotificationContext(
    Guid RequestId,
    string RequestedBy,
    Guid BackupRunId,
    string TargetDatabaseName,
    DateTime ExpiresAtUtc,
    string? Reason);

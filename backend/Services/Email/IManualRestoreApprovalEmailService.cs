namespace KasseAPI_Final.Services.Email;

public interface IManualRestoreApprovalEmailService
{
    bool IsConfigured { get; }

    Task<int> TrySendApprovalRequestsAsync(
        ManualRestoreApprovalEmailRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ManualRestoreApprovalEmailRequest(
    IReadOnlyList<string> ApproverEmails,
    Guid RequestId,
    string RequestedByEmail,
    string TargetDatabaseName,
    Guid BackupRunId,
    string ApprovalToken,
    DateTime ExpiresAtUtc,
    string? Reason);

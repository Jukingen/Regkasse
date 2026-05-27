using KasseAPI_Final.Services.Email;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Delivers manual-restore approval tokens (email today; SMS extension point).</summary>
public sealed class ManualRestoreApprovalNotificationService : IManualRestoreApprovalNotificationService
{
    private readonly IManualRestoreApprovalEmailService _email;
    private readonly ILogger<ManualRestoreApprovalNotificationService> _logger;

    public ManualRestoreApprovalNotificationService(
        IManualRestoreApprovalEmailService email,
        ILogger<ManualRestoreApprovalNotificationService> logger)
    {
        _email = email;
        _logger = logger;
    }

    public Task<int> SendApprovalTokenAsync(
        IReadOnlyList<string> approverEmails,
        string approvalToken,
        ManualRestoreApprovalNotificationContext context,
        CancellationToken cancellationToken = default)
    {
        var emailCount = _email.TrySendApprovalRequestsAsync(
            new ManualRestoreApprovalEmailRequest(
                approverEmails,
                context.RequestId,
                context.RequestedBy,
                context.TargetDatabaseName,
                context.BackupRunId,
                approvalToken,
                context.ExpiresAtUtc,
                context.Reason),
            cancellationToken);

        // SMS: not wired; log once per batch when approvers exist.
        if (approverEmails.Count > 0)
        {
            _logger.LogDebug(
                "Manual restore approval SMS not configured; email channel only. RequestId={RequestId}",
                context.RequestId);
        }

        return emailCount;
    }
}

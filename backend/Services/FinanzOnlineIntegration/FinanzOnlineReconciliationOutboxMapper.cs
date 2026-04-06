using System;
using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Shared mapping from outbox rows to reconciliation DTO fields (read-model only).
/// </summary>
public static class FinanzOnlineReconciliationOutboxMapper
{
    private const int LastResponsePreviewMaxChars = 400;

    public static string? MapLifecyclePhase(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        return status switch
        {
            FinanzOnlineOutboxStatuses.Pending => "PendingQueued",
            FinanzOnlineOutboxStatuses.Processing => "Sent",
            FinanzOnlineOutboxStatuses.AwaitingProtocol => "AwaitingProtocol",
            FinanzOnlineOutboxStatuses.ProtocolSuccess => "ProtocolSuccess",
            FinanzOnlineOutboxStatuses.RetryableFailure => "RetryableFailure",
            FinanzOnlineOutboxStatuses.PermanentFailure or FinanzOnlineOutboxStatuses.ProtocolFailure => "PermanentFailure",
            FinanzOnlineOutboxStatuses.DeadLetter => "DeadLetter",
            FinanzOnlineOutboxStatuses.ManualReviewRequired => "ManualReviewRequired",
            _ => status
        };
    }

    public static bool IsTerminalStatus(string? status)
    {
        return status is FinanzOnlineOutboxStatuses.ProtocolSuccess
            or FinanzOnlineOutboxStatuses.ProtocolFailure
            or FinanzOnlineOutboxStatuses.PermanentFailure
            or FinanzOnlineOutboxStatuses.ManualReviewRequired
            or FinanzOnlineOutboxStatuses.DeadLetter;
    }

    public static string? TruncatePreview(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        var t = json.Trim();
        if (t.Length <= LastResponsePreviewMaxChars)
            return t;
        return t.Substring(0, LastResponsePreviewMaxChars - 3) + "...";
    }
}

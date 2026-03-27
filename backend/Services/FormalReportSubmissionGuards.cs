using KasseAPI_Final.Data;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public enum FormalReportSubmitPrecheckDecision
{
    ProceedWithNewAttempt,
    ReturnExistingWithoutEnqueue,
    RejectSuperseded,
    RejectAlreadyAccepted
}

/// <summary>
/// Shared rules for formal report FinanzOnline enqueue: supersede chain, accepted artefact, in-flight idempotency.
/// </summary>
public static class FormalReportSubmissionGuards
{
    public static bool IsSupersededReport(string? reportStatus, Guid? supersededByReportId) =>
        string.Equals(reportStatus, "Superseded", StringComparison.OrdinalIgnoreCase)
        || supersededByReportId != null;

    public static bool IsOutboxAccepted(string? status) =>
        string.Equals(status, FinanzOnlineOutboxStatuses.ProtocolSuccess, StringComparison.Ordinal);

    public static bool IsOutboxInFlight(string? status) =>
        status is FinanzOnlineOutboxStatuses.Pending
            or FinanzOnlineOutboxStatuses.Processing
            or FinanzOnlineOutboxStatuses.RetryableFailure
            or FinanzOnlineOutboxStatuses.AwaitingProtocol;

    public static async Task<int> CountOutboxMessagesForAggregateAsync(
        AppDbContext db,
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken) =>
        await db.FinanzOnlineOutboxMessages.AsNoTracking()
            .CountAsync(x => x.AggregateType == aggregateType && x.AggregateId == aggregateId, cancellationToken);

    public static async Task<FormalReportSubmitPrecheckDecision> EvaluateSubmitPrecheckAsync(
        AppDbContext db,
        string aggregateType,
        Guid reportId,
        string? reportStatus,
        Guid? supersededByReportId,
        Guid? lastOutboxMessageId,
        CancellationToken cancellationToken)
    {
        if (IsSupersededReport(reportStatus, supersededByReportId))
            return FormalReportSubmitPrecheckDecision.RejectSuperseded;

        if (!lastOutboxMessageId.HasValue)
            return FormalReportSubmitPrecheckDecision.ProceedWithNewAttempt;

        var msg = await db.FinanzOnlineOutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == lastOutboxMessageId.Value, cancellationToken);
        if (msg == null)
            return FormalReportSubmitPrecheckDecision.ProceedWithNewAttempt;

        if (IsOutboxAccepted(msg.Status))
            return FormalReportSubmitPrecheckDecision.RejectAlreadyAccepted;

        if (IsOutboxInFlight(msg.Status))
            return FormalReportSubmitPrecheckDecision.ReturnExistingWithoutEnqueue;

        return FormalReportSubmitPrecheckDecision.ProceedWithNewAttempt;
    }
}

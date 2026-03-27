using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class ReportSubmissionCompatibilityService : IReportSubmissionCompatibilityService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<FinanzOnlineOutboxOptions> _outboxOptions;

    public ReportSubmissionCompatibilityService(
        AppDbContext db,
        IOptionsMonitor<FinanzOnlineOutboxOptions> outboxOptions)
    {
        _db = db;
        _outboxOptions = outboxOptions;
    }

    public async Task<ReportSubmissionEnvelopeDto> BuildEnvelopeAsync(
        BuildReportSubmissionEnvelopeRequest request,
        CancellationToken cancellationToken = default)
    {
        var opts = _outboxOptions.CurrentValue;
        var envelope = new ReportSubmissionEnvelopeDto
        {
            ReportType = request.ReportType,
            ReportId = request.ReportId,
            ReportState = request.ReportState,
            SubmissionState = "not_submitted",
            OutboxMessageId = request.OutboxMessageId,
            LegalExportPackageReference = request.LegalExportPackageReference ?? $"fiscal-export:{request.ReportType}:{request.ReportId:N}",
            SupersedesReportReference = request.SupersedesReportId?.ToString(),
            SupersededByReportReference = request.SupersededByReportId?.ToString(),
            RetryPolicy = new RetryPolicyDto
            {
                MaxAttempts = opts.MaxAttempts,
                BaseDelaySeconds = opts.BaseDelaySeconds,
                BackoffCapSeconds = opts.BackoffCapSeconds,
                JitterMaxSeconds = opts.JitterMaxSeconds,
                IdempotentEnqueue = true
            }
        };

        if (!request.OutboxMessageId.HasValue)
        {
            envelope.State = new ReportSubmissionStateDto
            {
                Lifecycle = "not_submitted",
                IsTerminal = false,
                IsAccepted = false,
                RequiresCorrection = false,
                RetryScheduled = false
            };
            envelope.RemediationHintsDe = new[] { "Noch nicht an FinanzOnline übermittelt." };
            return envelope;
        }

        var msg = await _db.FinanzOnlineOutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OutboxMessageId.Value, cancellationToken);
        if (msg == null)
        {
            envelope.SubmissionState = "unknown";
            envelope.State = new ReportSubmissionStateDto
            {
                Lifecycle = "unknown",
                IsTerminal = false,
                IsAccepted = false,
                RequiresCorrection = true,
                RetryScheduled = false,
                LastErrorMessage = "Outbox row missing."
            };
            envelope.RejectionReasons = new[] { "outbox_row_missing" };
            envelope.RemediationHintsDe = new[] { "Übermittlungszustand unklar — Outbox-Zeile fehlt, manuell prüfen." };
            return envelope;
        }

        var lifecycle = MapOutboxToLifecycle(msg.Status);
        var requiresCorrection = msg.Status == FinanzOnlineOutboxStatuses.ManualReviewRequired ||
                                 msg.Status == FinanzOnlineOutboxStatuses.ProtocolFailure ||
                                 msg.Status == FinanzOnlineOutboxStatuses.PermanentFailure ||
                                 msg.Status == FinanzOnlineOutboxStatuses.DeadLetter;
        var isAccepted = msg.Status == FinanzOnlineOutboxStatuses.ProtocolSuccess;
        var isTerminal = msg.Status is FinanzOnlineOutboxStatuses.ProtocolSuccess
            or FinanzOnlineOutboxStatuses.ProtocolFailure
            or FinanzOnlineOutboxStatuses.PermanentFailure
            or FinanzOnlineOutboxStatuses.DeadLetter;

        envelope.SubmissionState = lifecycle;
        envelope.OutboxStatus = msg.Status;
        envelope.CorrelationId = msg.CorrelationId;
        envelope.BusinessKey = msg.BusinessKey;
        envelope.MessageType = msg.MessageType;
        envelope.AggregateType = msg.AggregateType;
        envelope.State = new ReportSubmissionStateDto
        {
            Lifecycle = lifecycle,
            IsTerminal = isTerminal,
            IsAccepted = isAccepted,
            RequiresCorrection = requiresCorrection,
            RetryScheduled = msg.Status == FinanzOnlineOutboxStatuses.RetryableFailure || msg.Status == FinanzOnlineOutboxStatuses.Pending,
            ExternalReferenceId = msg.ExternalReferenceId,
            TransmissionId = msg.TransmissionId,
            LastErrorCode = msg.LastErrorCode,
            LastErrorMessage = msg.LastErrorMessage
        };
        envelope.Attempts = new[]
        {
            new SubmissionAttemptDto
            {
                AttemptCount = msg.AttemptCount,
                Status = msg.Status,
                NextAttemptAtUtc = msg.NextAttemptAt,
                ProcessedAtUtc = msg.ProcessedAt,
                FailureCategory = msg.FailureCategory,
                Result = new SubmissionResultDto
                {
                    Success = msg.Status == FinanzOnlineOutboxStatuses.ProtocolSuccess || msg.Status == FinanzOnlineOutboxStatuses.AwaitingProtocol,
                    AwaitingAcknowledgement = msg.Status == FinanzOnlineOutboxStatuses.AwaitingProtocol,
                    AcknowledgementMissing = msg.Status == FinanzOnlineOutboxStatuses.AwaitingProtocol && msg.TransmissionId != null,
                    ProtocolCode = msg.ProtocolCode,
                    ExternalStatus = msg.ExternalStatus
                }
            }
        };

        var rejectionReasons = new List<string>();
        if (!string.IsNullOrWhiteSpace(msg.LastErrorCode))
            rejectionReasons.Add(msg.LastErrorCode);
        if (requiresCorrection && rejectionReasons.Count == 0)
            rejectionReasons.Add("correction_required");
        envelope.RejectionReasons = rejectionReasons;
        envelope.RemediationHintsDe = BuildRemediationHints(msg.Status, msg.LastErrorMessage);
        return envelope;
    }

    public TagesberichtSubmissionStateDto ToLegacySubmissionState(ReportSubmissionEnvelopeDto envelope)
    {
        return new TagesberichtSubmissionStateDto
        {
            Lifecycle = envelope.State.Lifecycle,
            FinanzOnlineOutboxMessageId = envelope.OutboxMessageId,
            OutboxStatus = envelope.OutboxStatus,
            ExternalReferenceId = envelope.State.ExternalReferenceId,
            TransmissionId = envelope.State.TransmissionId,
            LastErrorCode = envelope.State.LastErrorCode,
            LastErrorMessage = envelope.State.LastErrorMessage,
            OperatorHintDe = envelope.RemediationHintsDe.FirstOrDefault()
        };
    }

    private static string MapOutboxToLifecycle(string status)
    {
        return status switch
        {
            FinanzOnlineOutboxStatuses.Pending => "queued",
            FinanzOnlineOutboxStatuses.Processing => "pending",
            FinanzOnlineOutboxStatuses.RetryableFailure => "retry_pending",
            FinanzOnlineOutboxStatuses.AwaitingProtocol => "awaiting_protocol",
            FinanzOnlineOutboxStatuses.ProtocolSuccess => "accepted",
            FinanzOnlineOutboxStatuses.ProtocolFailure => "rejected",
            FinanzOnlineOutboxStatuses.PermanentFailure => "rejected",
            FinanzOnlineOutboxStatuses.ManualReviewRequired => "correction_required",
            FinanzOnlineOutboxStatuses.DeadLetter => "failed_terminal",
            _ => "unknown"
        };
    }

    private static IReadOnlyList<string> BuildRemediationHints(string status, string? error)
    {
        if (status == FinanzOnlineOutboxStatuses.ProtocolSuccess)
            return new[] { "Übermittlung erfolgreich (Outbox)." };
        if (status == FinanzOnlineOutboxStatuses.RetryableFailure)
            return new[] { "Vorübergehender Fehler — automatischer Wiederholungsversuch läuft." };
        if (status == FinanzOnlineOutboxStatuses.AwaitingProtocol)
            return new[] { "Warte auf Protokollbestätigung." };
        if (status == FinanzOnlineOutboxStatuses.ManualReviewRequired)
            return new[] { "Manuelle Prüfung erforderlich.", "Bei Korrektur bitte neuen Bericht mit Supersede-Kette erzeugen." };
        if (status is FinanzOnlineOutboxStatuses.DeadLetter or FinanzOnlineOutboxStatuses.PermanentFailure or FinanzOnlineOutboxStatuses.ProtocolFailure)
            return new[] { string.IsNullOrWhiteSpace(error) ? "Dauerhafte Ablehnung oder Fehler — bitte prüfen." : error };
        return Array.Empty<string>();
    }
}

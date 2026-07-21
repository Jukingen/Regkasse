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
            SubmissionVersusReportNoteDe =
                "Berichtsstatus (Provisional/Finalized/Superseded) und FinanzOnline-Abgabe sind getrennt. Export-Profil steuert keine Meldung.",
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
            envelope.RemediationHintsDe = MergeChainHints(request, new[] { "Noch nicht an FinanzOnline übermittelt." });
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
            envelope.RemediationHintsDe = MergeChainHints(request, new[]
            {
                "Übermittlungszustand unklar — Outbox-Zeile fehlt oder veraltete Referenz.",
                "Outbox-Admin prüfen; ggf. erneut einreihen nach Datenbank-Konsistenz."
            });
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
            or FinanzOnlineOutboxStatuses.DeadLetter
            or FinanzOnlineOutboxStatuses.ManualReviewRequired;

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
        envelope.RemediationHintsDe = MergeChainHints(request, BuildRemediationHints(request, msg));
        return envelope;
    }

    public TagesberichtSubmissionStateDto ToLegacySubmissionState(ReportSubmissionEnvelopeDto envelope)
    {
        var hints = envelope.RemediationHintsDe?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        return new TagesberichtSubmissionStateDto
        {
            Lifecycle = envelope.State.Lifecycle,
            FinanzOnlineOutboxMessageId = envelope.OutboxMessageId,
            OutboxStatus = envelope.OutboxStatus,
            ExternalReferenceId = envelope.State.ExternalReferenceId,
            TransmissionId = envelope.State.TransmissionId,
            LastErrorCode = envelope.State.LastErrorCode,
            LastErrorMessage = envelope.State.LastErrorMessage,
            OperatorHintDe = hints.Count > 0 ? string.Join(" | ", hints) : null
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

    private static IReadOnlyList<string> BuildRemediationHints(
        BuildReportSubmissionEnvelopeRequest request,
        FinanzOnlineOutboxMessage msg)
    {
        var status = msg.Status;
        var error = msg.LastErrorMessage;
        if (status == FinanzOnlineOutboxStatuses.Pending)
            return new[] { "In Warteschlange — Abgabe noch nicht gestartet." };
        if (status == FinanzOnlineOutboxStatuses.Processing)
            return new[] { "Wird verarbeitet — bitte kurz warten (Outbox-Worker)." };
        if (status == FinanzOnlineOutboxStatuses.ProtocolSuccess)
        {
            var list = new List<string> { "Übermittlung akzeptiert (Outbox-Status ProtocolSuccess)." };
            if (string.Equals(request.ReportState, "Superseded", StringComparison.OrdinalIgnoreCase) && request.SupersededByReportId != null)
                list.Add("Historischer Stand: Abgabe kann zur alten Version gehören — aktuelle Kette prüfen.");
            return list;
        }
        if (status == FinanzOnlineOutboxStatuses.RetryableFailure)
            return new[]
            {
                "Vorübergehender Fehler — automatischer Wiederholungsversuch läuft.",
                "Bei anhaltendem Fehler Outbox-Eintrag und Korrelation prüfen."
            };
        if (status == FinanzOnlineOutboxStatuses.AwaitingProtocol)
            return new[]
            {
                "Warte auf Protokollbestätigung / SOAP-Nachreicher (AwaitingProtocol).",
                "Kein erneuter Klick nötig — Reconciliation läuft im Hintergrund."
            };
        if (status == FinanzOnlineOutboxStatuses.ManualReviewRequired)
            return new[]
            {
                "Manuelle Prüfung erforderlich (ManualReviewRequired).",
                "Keine erneute Abgabe desselben Artefakts ohne Klärung — ggf. neuen Korrekturbericht erzeugen."
            };
        if (status is FinanzOnlineOutboxStatuses.DeadLetter or FinanzOnlineOutboxStatuses.PermanentFailure or FinanzOnlineOutboxStatuses.ProtocolFailure)
        {
            var core = string.IsNullOrWhiteSpace(error) ? "Dauerhafte Ablehnung oder Fehler — bitte prüfen." : error;
            return new[]
            {
                core,
                "Erneuter Versand: „An FinanzOnline senden“ erzeugt einen neuen Versuch (neuer Payload/Attempt), sofern Bericht nicht bereits akzeptiert."
            };
        }
        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> MergeChainHints(
        BuildReportSubmissionEnvelopeRequest request,
        IReadOnlyList<string> baseHints)
    {
        var list = new List<string>();
        if (string.Equals(request.ReportState, "Superseded", StringComparison.OrdinalIgnoreCase))
            list.Add("Bericht ist Superseded — neue FinanzOnline-Abgabe erfolgt über den Nachfolgebericht (eigene Outbox-Kette).");
        else if (request.SupersededByReportId != null)
            list.Add("Nachfolgebericht vorhanden — für aktuelle Abgabe dessen Status nutzen.");
        foreach (var h in baseHints)
        {
            if (!string.IsNullOrWhiteSpace(h))
                list.Add(h);
        }
        return list;
    }
}

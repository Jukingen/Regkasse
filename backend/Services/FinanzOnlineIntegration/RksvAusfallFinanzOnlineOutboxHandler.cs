using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>Processes Ausfall / Wiederinbetriebnahme outbox rows via rkdb XML + existing SOAP transport.</summary>
public sealed class RksvAusfallFinanzOnlineOutboxHandler
{
    private readonly IFinanzOnlineSubmissionService _submissionService;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _rkdbOptions;
    private readonly ILogger<RksvAusfallFinanzOnlineOutboxHandler> _logger;

    public RksvAusfallFinanzOnlineOutboxHandler(
        IFinanzOnlineSubmissionService submissionService,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> rkdbOptions,
        ILogger<RksvAusfallFinanzOnlineOutboxHandler> logger)
    {
        _submissionService = submissionService;
        _rkdbOptions = rkdbOptions;
        _logger = logger;
    }

    public async Task ProcessAsync(
        AppDbContext context,
        IAuditLogService audit,
        FinanzOnlineOutboxMessage active,
        FinanzOnlineOutboxPayload outerPayload,
        FinanzOnlineOutboxOptions outboxOpts,
        CancellationToken cancellationToken)
    {
        RksvAusfallOutboxPayloadBody? inner;
        try
        {
            inner = JsonSerializer.Deserialize<RksvAusfallOutboxPayloadBody>(
                outerPayload.PayloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ausfall FO outbox inner payload invalid OutboxId={OutboxId}", active.Id);
            await MarkPermanentFailureAsync(context, active, null, "AUSFALL_MALFORMED_PAYLOAD", "Cannot parse Ausfall inner payload.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (inner == null || inner.EpisodeId == Guid.Empty)
        {
            await MarkPermanentFailureAsync(context, active, null, "AUSFALL_PAYLOAD_MISSING", "Ausfall inner payload missing episode id.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var episode = await context.RksvAusfallEpisodes
            .FirstOrDefaultAsync(e => e.Id == inner.EpisodeId, cancellationToken)
            .ConfigureAwait(false);
        if (episode == null)
        {
            await MarkPermanentFailureAsync(context, active, null, "AUSFALL_EPISODE_MISSING", "Episode row not found.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.Equals(episode.Status, RksvAusfallEpisodeStatuses.Verified, StringComparison.Ordinal) ||
            string.Equals(episode.Status, RksvAusfallEpisodeStatuses.Closed, StringComparison.Ordinal))
        {
            active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
            active.ProcessedAt = DateTime.UtcNow;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            active.ExternalReferenceId = episode.ExternalReference;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var cmd = new FinanzOnlineRkdbAusfallCommand
        {
            EpisodeType = episode.EpisodeType,
            OperationKind = episode.OperationKind,
            Begruendung = episode.Begruendung,
            CertificateSerial = episode.CertificateSerial,
            KassenIdentifikationsnummer = episode.KassenId,
            BeginnAusfallUtc = episode.BeginnUtc,
            EndeAusfallUtc = episode.EndeUtc,
            PaketNr = 1,
            SatzNr = 1,
            TsErstellungUtc = DateTimeOffset.UtcNow,
        };

        var validation = FinanzOnlineRkdbAusfallValidator.Validate(cmd);
        if (validation.Count > 0)
        {
            await MarkPermanentFailureAsync(
                    context,
                    active,
                    episode,
                    "RKDB_COMMAND_INVALID",
                    string.Join("; ", validation),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Ensure XML can be built (mapper will rebuild); catch early structural issues.
        var ns = string.IsNullOrWhiteSpace(_rkdbOptions.CurrentValue.SoapNamespace)
            ? "https://finanzonline.bmf.gv.at/rkdb"
            : _rkdbOptions.CurrentValue.SoapNamespace.Trim();
        _ = FinanzOnlineRkdbAusfallXmlBuilder.Build(ns, cmd);

        var response = await _submissionService.SubmitAsync(
                new FinanzOnlineRegisterSubmissionRequest
                {
                    Mode = outerPayload.Mode,
                    Scope = outerPayload.Scope,
                    Correlation = outerPayload.Correlation,
                    SubmissionKind = outerPayload.SubmissionKind,
                    PayloadJson = "{}",
                    RkdbAusfall = cmd,
                },
                cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        episode.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (response.Success)
        {
            episode.Status = RksvAusfallEpisodeStatuses.Verified;
            episode.ExternalReference = Truncate(
                FirstNonEmpty(response.TransmissionId, response.ReferenceId, response.ProtocolCode),
                120);
            episode.LastErrorCode = null;
            episode.LastErrorMessage = null;

            active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
            active.ExternalReferenceId = episode.ExternalReference;
            active.ProcessedAt = now;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await audit.LogSystemOperationAsync(
                    action: "RKSV_AUSFALL_FON_VERIFIED",
                    entityType: nameof(RksvAusfallEpisode),
                    userId: "system",
                    userRole: "System",
                    description: "Ausfall/Wiederinbetriebnahme FinanzOnline submission verified.",
                    actionType: AuditEventType.RksvAusfallEpisodeEnqueued,
                    entityId: episode.Id,
                    tenantId: episode.TenantId,
                    newValues: new { episode.Status, episode.ExternalReference, outboxId = active.Id })
                .ConfigureAwait(false);
            return;
        }

        var errorCode = string.IsNullOrWhiteSpace(response.ErrorCode) ? "RKDB_SUBMIT_FAILED" : response.ErrorCode.Trim();
        var retryable = RksvFinanzOnlineSubmissionResultMapper.IsTransientErrorCode(errorCode);
        episode.LastErrorCode = Truncate(errorCode, 80);
        episode.LastErrorMessage = Truncate(response.ErrorMessage, 500);
        episode.Status = retryable ? RksvAusfallEpisodeStatuses.Submitted : RksvAusfallEpisodeStatuses.Failed;

        if (retryable)
        {
            var attempt = active.AttemptCount + 1;
            var delay = Math.Min(outboxOpts.BaseDelaySeconds * (int)Math.Pow(2, Math.Min(attempt, 10)), outboxOpts.BackoffCapSeconds);
            active.AttemptCount = attempt;
            active.Status = FinanzOnlineOutboxStatuses.Pending;
            active.NextAttemptAt = now.AddSeconds(delay);
            active.LastErrorCode = errorCode;
            active.LastErrorMessage = Truncate(response.ErrorMessage, 500);
            active.FailureCategory = FinanzOnlineFailureCategories.RetryableTransient;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            if (attempt >= outboxOpts.MaxAttempts)
            {
                active.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
                episode.Status = RksvAusfallEpisodeStatuses.Failed;
            }
        }
        else
        {
            active.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
            active.LastErrorCode = errorCode;
            active.LastErrorMessage = Truncate(response.ErrorMessage, 500);
            active.FailureCategory = FinanzOnlineFailureCategories.PermanentBusiness;
            active.ProcessedAt = now;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task MarkPermanentFailureAsync(
        AppDbContext context,
        FinanzOnlineOutboxMessage active,
        RksvAusfallEpisode? episode,
        string code,
        string message,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        active.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
        active.LastErrorCode = code;
        active.LastErrorMessage = Truncate(message, 500);
        active.FailureCategory = FinanzOnlineFailureCategories.PermanentBusiness;
        active.ProcessedAt = now;
        active.ProcessingToken = null;
        active.ProcessingStartedAt = null;
        if (episode != null)
        {
            episode.Status = RksvAusfallEpisodeStatuses.Failed;
            episode.LastErrorCode = Truncate(code, 80);
            episode.LastErrorMessage = Truncate(message, 500);
            episode.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}

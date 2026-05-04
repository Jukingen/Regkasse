using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public static class FinanzOnlineOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string RetryableFailure = "RetryableFailure";
    public const string AwaitingProtocol = "AwaitingProtocol";
    public const string ProtocolSuccess = "ProtocolSuccess";
    public const string ProtocolFailure = "ProtocolFailure";
    public const string PermanentFailure = "PermanentFailure";
    public const string ManualReviewRequired = "ManualReviewRequired";
    public const string DeadLetter = "DeadLetter";
}

public static class FinanzOnlineFailureCategories
{
    public const string RetryableTransient = "RetryableTransientFailure";
    public const string PermanentBusiness = "PermanentValidationBusinessFailure";
    public const string Authorization = "AuthorizationFailure";
    public const string Session = "SessionFailure";
    public const string AwaitingProtocol = "AwaitingProtocol";
    public const string ManualReview = "ManualReviewRequired";
}

public sealed class FinanzOnlineOutboxOptions
{
    public const string SectionName = "FinanzOnlineOutbox";
    public bool Enabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxAttempts { get; set; } = 8;
    public int BaseDelaySeconds { get; set; } = 30;
    public int BackoffCapSeconds { get; set; } = 3600;
    public int JitterMaxSeconds { get; set; } = 15;
    public int ProcessingTimeoutSeconds { get; set; } = 300;
}

public sealed class FinanzOnlineOutboxPayload
{
    public FinanzOnlineIntegrationMode Mode { get; set; } = FinanzOnlineIntegrationMode.TEST;
    public FinanzOnlineScope Scope { get; set; } = new();
    public FinanzOnlineCorrelationContext Correlation { get; set; } = new();
    public FinanzOnlineSubmissionKind SubmissionKind { get; set; } = FinanzOnlineSubmissionKind.Register;
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Optional RKDB TEST command; persisted for worker replay.</summary>
    public FinanzOnlineRkdbBelegpruefungCommand? RkdbBelegpruefung { get; set; }

    /// <summary>From rkdb submit response; needed for status_kasse SOAP reconciliation.</summary>
    public string? RkdbTsErstellungIso { get; set; }

    /// <summary>From rkdb submit response or belegpruefung command.</summary>
    public int? RkdbSatzNr { get; set; }
}

public interface IFinanzOnlineOutboxService
{
    /// <param name="persistImmediately">When false, the outbox row is only added to the current <see cref="AppDbContext"/>; caller must <c>SaveChanges</c> (e.g. same DB transaction as fiscal writes).</param>
    Task<FinanzOnlineOutboxMessage> EnqueueSubmissionAsync(
        string aggregateType,
        Guid aggregateId,
        string messageType,
        string businessKey,
        FinanzOnlineOutboxPayload payload,
        CancellationToken cancellationToken = default,
        bool persistImmediately = true);
}

public sealed class FinanzOnlineOutboxService : IFinanzOnlineOutboxService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FinanzOnlineOutboxService> _logger;

    public FinanzOnlineOutboxService(AppDbContext context, ILogger<FinanzOnlineOutboxService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FinanzOnlineOutboxMessage> EnqueueSubmissionAsync(
        string aggregateType,
        Guid aggregateId,
        string messageType,
        string businessKey,
        FinanzOnlineOutboxPayload payload,
        CancellationToken cancellationToken = default,
        bool persistImmediately = true)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadHash = ComputeSha256(payloadJson);
        var idempotencyKey = ComputeSha256($"{aggregateType}|{aggregateId:N}|{messageType}|{businessKey}|{payloadHash}|{payload.Mode}");

        // Same DbContext may hold an unsaved outbox row (deferSave path); check Local before DB query.
        var tracked = _context.FinanzOnlineOutboxMessages.Local
            .FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (tracked != null)
            return tracked;

        var existing = await _context.FinanzOnlineOutboxMessages
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing != null)
            return existing;

        var item = new FinanzOnlineOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = payload.Scope.TenantId,
            BranchId = payload.Scope.BranchId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            MessageType = messageType,
            BusinessKey = businessKey,
            IdempotencyKey = idempotencyKey,
            PayloadJson = payloadJson,
            PayloadHash = payloadHash,
            Mode = payload.Mode.ToString(),
            Status = FinanzOnlineOutboxStatuses.Pending,
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = payload.Correlation.CorrelationId,
            CreatedAt = DateTime.UtcNow
        };

        _context.FinanzOnlineOutboxMessages.Add(item);
        if (persistImmediately)
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("FinanzOnline outbox enqueued MessageType={MessageType} AggregateType={AggregateType} AggregateId={AggregateId} CorrelationId={CorrelationId} PersistImmediately={PersistImmediately}",
            messageType, aggregateType, aggregateId, item.CorrelationId, persistImmediately);
        return item;
    }

    private static string ComputeSha256(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class FinanzOnlineOutboxHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<FinanzOnlineOutboxOptions> _options;
    private readonly ILogger<FinanzOnlineOutboxHostedService> _logger;
    private long _cycleIndex;

    public FinanzOnlineOutboxHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<FinanzOnlineOutboxOptions> options,
        ILogger<FinanzOnlineOutboxHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _logger.LogInformation("FinanzOnline outbox worker disabled (FinanzOnlineOutbox:Enabled=false).");
            return;
        }

        _logger.LogInformation(
            "FinanzOnline outbox worker started: PollInterval={PollInterval}s ProcessingTimeoutSeconds={ProcessingTimeoutSeconds} MaxAttempts={MaxAttempts}",
            Math.Max(1, (int)opts.PollInterval.TotalSeconds),
            opts.ProcessingTimeoutSeconds,
            opts.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOneAsync(stoppingToken).ConfigureAwait(false);
                await ReconcileOneAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline outbox cycle failed.");
            }

            _cycleIndex++;
            if (_cycleIndex == 1 || _cycleIndex % 60 == 0)
            {
                _logger.LogInformation(
                    "FinanzOnline outbox worker heartbeat: completedCycles={Cycles} pollIntervalSeconds={PollIntervalSeconds}",
                    _cycleIndex,
                    Math.Max(1, (int)_options.CurrentValue.PollInterval.TotalSeconds));
            }

            await Task.Delay(_options.CurrentValue.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Test assembly entry point only (<c>InternalsVisibleTo</c>). One dequeue/claim/submit cycle; no poll delay. Not for production calls.
    /// </summary>
    internal Task ProcessOneForIntegrationTestsAsync(CancellationToken cancellationToken = default) =>
        ProcessOneAsync(cancellationToken);

    /// <summary>
    /// Test assembly entry point only (<c>InternalsVisibleTo</c>). One AwaitingProtocol status_kasse reconciliation pass. Not for production calls.
    /// </summary>
    internal Task ReconcileOneForIntegrationTestsAsync(CancellationToken cancellationToken = default) =>
        ReconcileOneAsync(cancellationToken);

    private async Task ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submissionService = scope.ServiceProvider.GetRequiredService<IFinanzOnlineSubmissionService>();
        var rksvSpecialReceiptOutboxHandler = scope.ServiceProvider.GetRequiredService<RksvSpecialReceiptFinanzOnlineOutboxHandler>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var opts = _options.CurrentValue;
        var now = DateTime.UtcNow;
        var staleBefore = now.AddSeconds(-Math.Max(30, opts.ProcessingTimeoutSeconds));

        var candidate = await context.FinanzOnlineOutboxMessages
            .Where(x =>
                ((x.Status == FinanzOnlineOutboxStatuses.Pending || x.Status == FinanzOnlineOutboxStatuses.RetryableFailure) ||
                 (x.Status == FinanzOnlineOutboxStatuses.Processing && x.ProcessingStartedAt != null && x.ProcessingStartedAt < staleBefore)) &&
                x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidate == null)
            return;

        var claimToken = Guid.NewGuid().ToString("N");
        var claimed = await context.FinanzOnlineOutboxMessages
            .Where(x => x.Id == candidate.Id &&
                ((x.Status == FinanzOnlineOutboxStatuses.Pending || x.Status == FinanzOnlineOutboxStatuses.RetryableFailure) && x.ProcessingToken == null ||
                 (x.Status == FinanzOnlineOutboxStatuses.Processing && x.ProcessingStartedAt == candidate.ProcessingStartedAt && x.ProcessingStartedAt < staleBefore)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, FinanzOnlineOutboxStatuses.Processing)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.NextAttemptAt, DateTime.UtcNow.AddMinutes(5))
                .SetProperty(x => x.ProcessingToken, claimToken)
                .SetProperty(x => x.ProcessingStartedAt, DateTime.UtcNow), cancellationToken)
            .ConfigureAwait(false);
        if (claimed == 0)
            return;

        // ExecuteUpdateAsync updates the row in the database but does not refresh the tracked candidate.
        // Failure classification uses AttemptCount; stale tracker values can mis-route MaxAttempts=1 to RetryableFailure instead of DeadLetter.
        var messageId = candidate.Id;
        context.Entry(candidate).State = EntityState.Detached;
        var active = await context.FinanzOnlineOutboxMessages
            .FirstAsync(x => x.Id == messageId && x.ProcessingToken == claimToken, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var payload = JsonSerializer.Deserialize<FinanzOnlineOutboxPayload>(active.PayloadJson);
            if (payload == null)
            {
                await MarkPermanentFailureAsync(context, active, "MALFORMED_PAYLOAD", "Cannot parse outbox payload.", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.Equals(active.MessageType, FinanzOnlineTagesberichtMessageTypes.TagesberichtDailySummary, StringComparison.Ordinal))
            {
                active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
                active.ExternalReferenceId = Truncate($"TBR-{active.AggregateId:N}", 120);
                active.TransmissionId = Truncate($"TBR-TX-{Guid.NewGuid():N}", 120);
                active.ProcessingToken = null;
                active.ProcessingStartedAt = null;
                active.ProcessedAt = DateTime.UtcNow;
                active.LastErrorCode = null;
                active.LastErrorMessage = null;
                active.FailureCategory = null;
                active.LastResponseJson = JsonSerializer.Serialize(new
                {
                    result = "TagesberichtInformational",
                    reportId = active.AggregateId,
                    note = "Non-DEP summary pipeline; see TagesberichtReport snapshot hash."
                });
                await TagesberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, active, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "FinanzOnline outbox Tagesbericht informational success Id={OutboxId} AggregateId={AggregateId}",
                    active.Id, active.AggregateId);
                await LogOutboxAttemptAsync(audit, active, "informational_success", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.Equals(active.MessageType, FinanzOnlineMonatsberichtMessageTypes.MonatsberichtMonthlySummary, StringComparison.Ordinal))
            {
                active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
                active.ExternalReferenceId = Truncate($"MBR-{active.AggregateId:N}", 120);
                active.TransmissionId = Truncate($"MBR-TX-{Guid.NewGuid():N}", 120);
                active.ProcessingToken = null;
                active.ProcessingStartedAt = null;
                active.ProcessedAt = DateTime.UtcNow;
                active.LastErrorCode = null;
                active.LastErrorMessage = null;
                active.FailureCategory = null;
                active.LastResponseJson = JsonSerializer.Serialize(new
                {
                    result = "MonatsberichtInformational",
                    reportId = active.AggregateId,
                    note = "Non-DEP monthly summary; see MonatsberichtReport snapshot hash."
                });
                await MonatsberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, active, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "FinanzOnline outbox Monatsbericht informational success Id={OutboxId} AggregateId={AggregateId}",
                    active.Id, active.AggregateId);
                await LogOutboxAttemptAsync(audit, active, "informational_success", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.Equals(active.MessageType, FinanzOnlineJahresberichtMessageTypes.JahresberichtAnnualSummary, StringComparison.Ordinal))
            {
                active.Status = FinanzOnlineOutboxStatuses.ProtocolSuccess;
                active.ExternalReferenceId = Truncate($"YBR-{active.AggregateId:N}", 120);
                active.TransmissionId = Truncate($"YBR-TX-{Guid.NewGuid():N}", 120);
                active.ProcessingToken = null;
                active.ProcessingStartedAt = null;
                active.ProcessedAt = DateTime.UtcNow;
                active.LastErrorCode = null;
                active.LastErrorMessage = null;
                active.FailureCategory = null;
                active.LastResponseJson = JsonSerializer.Serialize(new
                {
                    result = "JahresberichtInformational",
                    reportId = active.AggregateId,
                    note = "Non-DEP annual summary; see JahresberichtReport snapshot hash."
                });
                await JahresberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, active, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "FinanzOnline outbox Jahresbericht informational success Id={OutboxId} AggregateId={AggregateId}",
                    active.Id, active.AggregateId);
                await LogOutboxAttemptAsync(audit, active, "informational_success", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.Equals(active.MessageType, FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission, StringComparison.Ordinal))
            {
                await rksvSpecialReceiptOutboxHandler.ProcessAsync(
                    context,
                    audit,
                    active,
                    payload,
                    opts,
                    isJahresbeleg: false,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.Equals(active.MessageType, FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvJahresbelegSubmission, StringComparison.Ordinal))
            {
                await rksvSpecialReceiptOutboxHandler.ProcessAsync(
                    context,
                    audit,
                    active,
                    payload,
                    opts,
                    isJahresbeleg: true,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var result = await submissionService.SubmitAsync(new FinanzOnlineRegisterSubmissionRequest
            {
                Mode = payload.Mode,
                Scope = payload.Scope,
                Correlation = payload.Correlation,
                SubmissionKind = payload.SubmissionKind,
                PayloadJson = payload.PayloadJson,
                RkdbBelegpruefung = payload.RkdbBelegpruefung
            }, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                payload.RkdbTsErstellungIso = result.RkdbTsErstellungIso ?? payload.RkdbTsErstellungIso;
                payload.RkdbSatzNr = result.RkdbSatzNr ?? payload.RkdbBelegpruefung?.SatzNr ?? 1;
                active.PayloadJson = JsonSerializer.Serialize(payload);

                active.Status = string.IsNullOrWhiteSpace(result.TransmissionId)
                    ? FinanzOnlineOutboxStatuses.ProtocolSuccess
                    : FinanzOnlineOutboxStatuses.AwaitingProtocol;
                active.FailureCategory = string.IsNullOrWhiteSpace(result.TransmissionId)
                    ? null
                    : FinanzOnlineFailureCategories.AwaitingProtocol;
                active.LastErrorCode = null;
                active.LastErrorMessage = null;
                active.TransmissionId = Truncate(result.TransmissionId, 120);
                active.ExternalReferenceId = Truncate(result.ReferenceId, 120);
                active.ExternalStatus = Truncate(result.Status, 40);
                active.ProtocolCode = Truncate(result.ProtocolCode, 80);
                if (!string.IsNullOrWhiteSpace(result.ProtocolSummary))
                    active.ProtocolSummary = Truncate(result.ProtocolSummary, 500);
                active.LastResponseJson = JsonSerializer.Serialize(new
                {
                    result.Success,
                    result.TransmissionId,
                    result.ReferenceId,
                    result.Status,
                    result.ProtocolCode,
                    result.ProtocolSummary,
                    result.ErrorCode,
                    result.ErrorMessage,
                    result.RkdbTsErstellungIso,
                    result.RkdbSatzNr
                });
                active.ProcessedAt = DateTime.UtcNow;
            }
            else
            {
                var classified = ClassifyFailure(result.ErrorCode);
                var retryable = classified.retryable;
                if (retryable && active.AttemptCount < opts.MaxAttempts)
                {
                    var delay = ComputeBackoffSecondsWithJitter(active.Id, active.AttemptCount, opts.BaseDelaySeconds, opts.BackoffCapSeconds, opts.JitterMaxSeconds);
                    active.Status = FinanzOnlineOutboxStatuses.RetryableFailure;
                    active.NextAttemptAt = DateTime.UtcNow.AddSeconds(delay);
                    active.FailureCategory = FinanzOnlineFailureCategories.RetryableTransient;
                }
                else
                {
                    active.Status = active.AttemptCount >= opts.MaxAttempts ? FinanzOnlineOutboxStatuses.DeadLetter : classified.terminalStatus;
                    active.FailureCategory = active.Status == FinanzOnlineOutboxStatuses.DeadLetter
                        ? "MaxAttemptsExceeded"
                        : classified.category;
                    active.ProcessedAt = DateTime.UtcNow;
                }

                active.LastErrorCode = Truncate(result.ErrorCode, 80);
                active.LastErrorMessage = Truncate(result.ErrorMessage, 500);
                active.ExternalStatus = Truncate(result.Status, 40);
                active.ProtocolCode = Truncate(result.ProtocolCode, 80);
                active.LastResponseJson = JsonSerializer.Serialize(new
                {
                    result.Success,
                    result.TransmissionId,
                    result.ReferenceId,
                    result.Status,
                    result.ProtocolCode,
                    result.ErrorCode,
                    result.ErrorMessage
                });
            }
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;

            await TagesberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, active, cancellationToken).ConfigureAwait(false);
            await MonatsberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, active, cancellationToken).ConfigureAwait(false);
            await JahresberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, active, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("FinanzOnline outbox processed Id={OutboxId} Status={Status} Category={Category} Attempt={Attempt} CorrelationId={CorrelationId}",
                active.Id, active.Status, active.FailureCategory ?? "", active.AttemptCount, active.CorrelationId);
            await LogOutboxAttemptAsync(audit, active, "processed", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            var delay = ComputeBackoffSecondsWithJitter(active.Id, active.AttemptCount, opts.BaseDelaySeconds, opts.BackoffCapSeconds, opts.JitterMaxSeconds);
            active.Status = active.AttemptCount >= opts.MaxAttempts ? FinanzOnlineOutboxStatuses.DeadLetter : FinanzOnlineOutboxStatuses.RetryableFailure;
            active.NextAttemptAt = DateTime.UtcNow.AddSeconds(delay);
            active.LastErrorCode = active.AttemptCount >= opts.MaxAttempts ? "TRANSIENT_MAX_ATTEMPTS" : "TRANSIENT_NETWORK_FAILURE";
            active.LastErrorMessage = "Transient network failure.";
            active.FailureCategory = active.Status == FinanzOnlineOutboxStatuses.DeadLetter ? "MaxAttemptsExceeded" : FinanzOnlineFailureCategories.RetryableTransient;
            active.ProcessingToken = null;
            active.ProcessingStartedAt = null;
            if (active.Status == FinanzOnlineOutboxStatuses.DeadLetter)
                active.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await LogOutboxAttemptAsync(audit, active, "transient_failure", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task LogOutboxAttemptAsync(
        IAuditLogService audit,
        FinanzOnlineOutboxMessage message,
        string eventName,
        CancellationToken cancellationToken)
    {
        await audit.LogSystemOperationAsync(
            $"FinanzOnlineOutboxAttempt.{eventName}",
            nameof(FinanzOnlineOutboxMessage),
            "system",
            "OutboxWorker",
            description: "FinanzOnline outbox attempt persisted",
            requestData: new
            {
                outboxId = message.Id,
                message.AggregateType,
                message.AggregateId,
                message.MessageType,
                message.CorrelationId,
                message.AttemptCount
            },
            responseData: new
            {
                message.Status,
                message.FailureCategory,
                message.LastErrorCode,
                message.LastErrorMessage,
                message.TransmissionId,
                message.ExternalReferenceId,
                message.ProtocolCode
            },
            status: AuditLogStatus.Success);
    }

    private async Task ReconcileOneAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queryClient = scope.ServiceProvider.GetRequiredService<IFinanzOnlineTransmissionQueryClient>();
        var now = DateTime.UtcNow;

        var candidate = await context.FinanzOnlineOutboxMessages
            .Where(x => x.Status == FinanzOnlineOutboxStatuses.AwaitingProtocol && x.NextAttemptAt <= now && x.TransmissionId != null)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidate == null)
            return;

        var payload = JsonSerializer.Deserialize<FinanzOnlineOutboxPayload>(candidate.PayloadJson);
        if (payload == null || string.IsNullOrWhiteSpace(candidate.TransmissionId))
        {
            candidate.Status = FinanzOnlineOutboxStatuses.ManualReviewRequired;
            candidate.FailureCategory = FinanzOnlineFailureCategories.ManualReview;
            candidate.LastErrorCode = "RECONCILE_PAYLOAD_MISSING";
            candidate.LastErrorMessage = "Outbox payload/transmission missing for reconciliation.";
            candidate.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var response = await queryClient.QueryStatusAsync(new FinanzOnlineTransmissionStatusQueryRequest
        {
            Mode = payload.Mode,
            Scope = payload.Scope,
            Correlation = payload.Correlation,
            TransmissionId = candidate.TransmissionId!,
            RkdbTsErstellungIso = payload.RkdbTsErstellungIso,
            RkdbSatzNr = payload.RkdbSatzNr ?? payload.RkdbBelegpruefung?.SatzNr ?? 1,
            ExternalReferenceFastNr = candidate.ExternalReferenceId
        }, cancellationToken).ConfigureAwait(false);

        candidate.AttemptCount += 1;
        candidate.ExternalStatus = Truncate(response.Status, 40);
        candidate.LastErrorCode = Truncate(response.ErrorCode, 80);
        candidate.LastErrorMessage = Truncate(response.ErrorMessage, 500);
        candidate.LastResponseJson = JsonSerializer.Serialize(response);
        candidate.ProtocolPayloadHash = TestModeFinanzOnlineTransmissionQueryClient.ComputeProtocolHash(response.Protocol);
        candidate.ProtocolSummary = Truncate(BuildProtocolSummary(response), 500);

        var transition = MapProtocolState(response);
        candidate.Status = transition.nextStatus;
        candidate.FailureCategory = transition.category;
        candidate.NextAttemptAt = transition.nextAttemptAt;
        if (transition.isFinal)
            candidate.ProcessedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TagesberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, candidate, cancellationToken).ConfigureAwait(false);
        await MonatsberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, candidate, cancellationToken).ConfigureAwait(false);
        await JahresberichtOutboxAggregateUpdater.ApplyAfterOutboxPersistAsync(context, candidate, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static (bool retryable, string category, string terminalStatus) ClassifyFailure(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return (true, FinanzOnlineFailureCategories.RetryableTransient, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("HTTP_5", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("HTTP_429", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
            return (true, FinanzOnlineFailureCategories.RetryableTransient, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("SESSION", StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.Session, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.Authorization, FinanzOnlineOutboxStatuses.PermanentFailure);
        if (errorCode.Contains("RKDB_XML_STRUCTURE_INVALID", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("RKDB_COMMAND_INVALID", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("RKDB_MODE_NOT_SUPPORTED", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("RKDB_XML_PAYLOAD_REQUIRED", StringComparison.OrdinalIgnoreCase))
            return (false, FinanzOnlineFailureCategories.PermanentBusiness, FinanzOnlineOutboxStatuses.PermanentFailure);
        return (false, FinanzOnlineFailureCategories.PermanentBusiness, FinanzOnlineOutboxStatuses.PermanentFailure);
    }

    private static int ComputeBackoffSecondsWithJitter(Guid messageId, int attempt, int baseDelaySeconds, int capSeconds, int jitterMaxSeconds)
    {
        var delay = baseDelaySeconds * (int)Math.Pow(2, Math.Min(attempt, 20));
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{messageId:N}:{attempt}"));
        var seed = BitConverter.ToUInt32(seedBytes, 0);
        var jitter = jitterMaxSeconds <= 0 ? 0 : (int)(seed % (uint)(jitterMaxSeconds + 1));
        return Math.Min(delay + jitter, capSeconds);
    }

    private static (string nextStatus, DateTime nextAttemptAt, bool isFinal, string category) MapProtocolState(FinanzOnlineTransmissionStatusQueryResponse response)
    {
        var now = DateTime.UtcNow;
        var status = (response.Status ?? "").Trim().ToLowerInvariant();
        var code = (response.ErrorCode ?? "").Trim().ToUpperInvariant();

        if (!response.Success)
        {
            if (code.Contains("MAINTENANCE", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("503", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("HTTP_5", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
                return (FinanzOnlineOutboxStatuses.RetryableFailure, now.AddMinutes(2), false, FinanzOnlineFailureCategories.RetryableTransient);
            if (code.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("SESSION_INVALID", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("SESSION_UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
                return (FinanzOnlineOutboxStatuses.PermanentFailure, now, true, FinanzOnlineFailureCategories.Authorization);
            if (code.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                return (FinanzOnlineOutboxStatuses.ManualReviewRequired, now, true, FinanzOnlineFailureCategories.ManualReview);
            if (code.Contains("PROTOCOL_QUERY_CONTEXT_MISSING", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("RKDB_QUERY_CONTEXT_INVALID", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("MALFORMED_RESPONSE", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("SOAP_FAULT", StringComparison.OrdinalIgnoreCase))
                return (FinanzOnlineOutboxStatuses.ManualReviewRequired, now, true, FinanzOnlineFailureCategories.ManualReview);
            return (FinanzOnlineOutboxStatuses.ManualReviewRequired, now, true, FinanzOnlineFailureCategories.ManualReview);
        }

        if (code.Contains("401", StringComparison.OrdinalIgnoreCase) || code.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase) || code.Contains("SESSION_INVALID", StringComparison.OrdinalIgnoreCase))
            return (FinanzOnlineOutboxStatuses.PermanentFailure, now, true, FinanzOnlineFailureCategories.Authorization);
        if (code.Contains("MAINTENANCE", StringComparison.OrdinalIgnoreCase) || code.Contains("503", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("TRANSIENT", StringComparison.OrdinalIgnoreCase))
            return (FinanzOnlineOutboxStatuses.RetryableFailure, now.AddMinutes(2), false, FinanzOnlineFailureCategories.RetryableTransient);
        if (code.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            return (FinanzOnlineOutboxStatuses.ManualReviewRequired, now, true, FinanzOnlineFailureCategories.ManualReview);

        if (status is "success" or "submitted" or "accepted" or "completed")
            return (FinanzOnlineOutboxStatuses.ProtocolSuccess, now, true, "");
        if (status is "failed" or "rejected" or "error")
            return (FinanzOnlineOutboxStatuses.ProtocolFailure, now, true, FinanzOnlineFailureCategories.PermanentBusiness);
        if (status is "pending" or "processing" or "awaitingprotocol")
            return (FinanzOnlineOutboxStatuses.AwaitingProtocol, now.AddMinutes(1), false, FinanzOnlineFailureCategories.AwaitingProtocol);

        return (FinanzOnlineOutboxStatuses.ManualReviewRequired, now, true, FinanzOnlineFailureCategories.ManualReview);
    }

    private static string BuildProtocolSummary(FinanzOnlineTransmissionStatusQueryResponse response)
    {
        if (response.Protocol.Count == 0)
            return $"status={response.Status ?? ""}; errorCode={response.ErrorCode ?? ""}";
        var last = response.Protocol[^1];
        return $"status={response.Status ?? ""}; lastLevel={last.Level}; lastMessage={last.Message}";
    }

    private static string? Truncate(string? text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
        return text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
    }

    private static async Task MarkPermanentFailureAsync(AppDbContext context, FinanzOnlineOutboxMessage item, string code, string message, CancellationToken cancellationToken)
    {
        item.Status = FinanzOnlineOutboxStatuses.PermanentFailure;
        item.LastErrorCode = code;
        item.LastErrorMessage = message;
        item.FailureCategory = FinanzOnlineFailureCategories.PermanentBusiness;
        item.ProcessingToken = null;
        item.ProcessingStartedAt = null;
        item.ProcessedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

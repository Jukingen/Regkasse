using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>
/// Polls Storecove for canary rows in <c>Sent</c>. Webhooks need a public URL; this canary does not.
/// <c>Peppol:AckPollInterval</c> 0 does not open HTTP. Only exact <c>TEST</c> may call Storecove.
/// A 2xx body is <c>Ack</c> only when <c>guid</c> matches and <c>state</c> is <c>DELIVERED</c>.
/// </summary>
public sealed class PeppolAckPollingService : BackgroundService
{
    public const int MinAgeMinutes = 5;
    public static readonly TimeSpan AckTimeout = TimeSpan.FromHours(24);
    public const string AckTimeoutReason = "peppol-ack-timeout";
    public const string AckErrorReason = "peppol-ack-error";
    public const string RetriesExhaustedReason = "peppol-ack-retries-exhausted";
    public const string DeliveredState = "DELIVERED";
    public const string ErrorState = "ERROR";
    public const string RejectedState = "REJECTED";
    public const string InvalidState = "INVALID";
    public static readonly string[] RetryableErrorCodes = ["STORE_INTERNAL", "TIMEOUT", "RATE_LIMIT"];
    public static readonly int[] DefaultRetryIntervalsSeconds = [300, 1800];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<PeppolOptions> _options;
    private readonly ILogger<PeppolAckPollingService> _logger;

    public PeppolAckPollingService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PeppolOptions> options,
        ILogger<PeppolAckPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = _options.CurrentValue.AckPollInterval;
            if (intervalMinutes > 0)
            {
                try
                {
                    await PollOnceAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Peppol ACK poll failed");
                }
            }

            var delay = intervalMinutes > 0
                ? TimeSpan.FromMinutes(intervalMinutes)
                : TimeSpan.FromMinutes(1);
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (options.AckPollInterval <= 0)
            return;

        if (!Guid.TryParse(options.ReservedExit.CanaryTenantId?.Trim(), out var canary)
            || !options.ReservedExit.Enabled
            || canary == Guid.Empty)
        {
            return;
        }

        if (!string.Equals(options.Provider?.Trim(), "storecove", StringComparison.OrdinalIgnoreCase))
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var minAge = now.AddMinutes(-MinAgeMinutes);
        var rows = await db.EinvoiceSubmissions
            .IgnoreQueryFilters()
            .Where(row => row.Status == EinvoiceSubmissionStatuses.Sent && row.TenantId == canary)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var storecoveReady = string.Equals(
            options.Storecove.Environment?.Trim(),
            "TEST",
            StringComparison.Ordinal);
        // Production and the ACK unit tests register the Storecove client. The canary smoke
        // test registers only IPeppolAccessPointClient, so this poll never opens a socket.
        IPeppolAccessPointClient? client = null;
        if (storecoveReady)
        {
            client = scope.ServiceProvider.GetService<StorecovePeppolAccessPointClient>()
                ?? scope.ServiceProvider.GetService<IPeppolAccessPointClient>();
        }

        var retryIntervals = RetryIntervals(options);
        foreach (var row in rows)
        {
            var origin = row.CreatedAtUtc;
            if (row.AttemptedAtUtc is DateTime attempted && attempted < origin)
                origin = attempted;
            if (origin <= now - AckTimeout)
            {
                row.Status = EinvoiceSubmissionStatuses.Failed;
                row.FailureReason = AckTimeoutReason;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Peppol ACK timed out. SubmissionId={SubmissionId} Reason={Reason}",
                    row.Id,
                    AckTimeoutReason);
                continue;
            }

            if (row.ProviderAttemptCount > 0)
            {
                if (!RetryIsDue(row, retryIntervals, now))
                    continue;
            }
            else
            {
                var stamp = row.AttemptedAtUtc ?? row.CreatedAtUtc;
                if (stamp > minAge)
                    continue;
            }

            if (client is null || string.IsNullOrWhiteSpace(row.ProviderMessageId))
                continue;

            PeppolTransportResult transport;
            try
            {
                transport = await client.GetStatusAsync(row.ProviderMessageId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (PeppolTransportException ex) when (ex.Code == StorecovePeppolAccessPointClient.LiveNotAllowedCode)
            {
                continue;
            }

            if (!string.Equals(transport.ProviderMessageId, row.ProviderMessageId, StringComparison.Ordinal))
                continue;

            var state = transport.Detail?.Trim();
            if (string.Equals(state, DeliveredState, StringComparison.Ordinal))
            {
                row.Status = EinvoiceSubmissionStatuses.Ack;
                row.AckedAtUtc = now;
                row.FailureReason = null;
                row.ProviderStatus = DeliveredState;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await RecordAckAsync(scope.ServiceProvider, row, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (IsTerminalProviderState(state) || string.Equals(state, ErrorState, StringComparison.Ordinal))
            {
                if (string.Equals(state, ErrorState, StringComparison.Ordinal)
                    && IsRetryableCode(transport.ProviderCode))
                {
                    row.ProviderAttemptCount++;
                    row.ProviderStatus = ErrorState;
                    if (row.ProviderAttemptCount <= retryIntervals.Length)
                    {
                        row.Status = EinvoiceSubmissionStatuses.Sent;
                        row.FailureReason = null;
                        row.AttemptedAtUtc = now;
                        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        await RecordRetryAsync(scope.ServiceProvider, row, cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation(
                            "Peppol ACK retry scheduled. SubmissionId={SubmissionId} Attempt={Attempt}",
                            row.Id,
                            row.ProviderAttemptCount);
                        continue;
                    }

                    row.Status = EinvoiceSubmissionStatuses.Failed;
                    row.FailureReason = RetriesExhaustedReason;
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "Peppol ACK retries exhausted. SubmissionId={SubmissionId} Reason={Reason}",
                        row.Id,
                        RetriesExhaustedReason);
                    continue;
                }

                row.Status = EinvoiceSubmissionStatuses.Failed;
                row.FailureReason = AckErrorReason;
                row.ProviderStatus = state;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Peppol ACK provider error. SubmissionId={SubmissionId} Reason={Reason}",
                    row.Id,
                    AckErrorReason);
            }
        }
    }

    internal static bool IsRetryableCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        foreach (var known in RetryableErrorCodes)
        {
            if (string.Equals(known, code.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsTerminalProviderState(string? state) =>
        string.Equals(state, RejectedState, StringComparison.Ordinal)
        || string.Equals(state, InvalidState, StringComparison.Ordinal);

    private static int[] RetryIntervals(PeppolOptions options) =>
        options.AckRetryIntervalsSeconds is { Length: > 0 } intervals
            ? intervals
            : DefaultRetryIntervalsSeconds;

    private static bool RetryIsDue(EinvoiceSubmission row, int[] intervals, DateTime now)
    {
        var index = row.ProviderAttemptCount - 1;
        if (index < 0 || index >= intervals.Length)
            return true;

        var wait = TimeSpan.FromSeconds(Math.Max(0, intervals[index]));
        var last = row.AttemptedAtUtc ?? row.CreatedAtUtc;
        return last <= now - wait;
    }

    private static async Task RecordAckAsync(
        IServiceProvider services,
        EinvoiceSubmission row,
        CancellationToken cancellationToken)
    {
        var audit = services.GetService<IAuditLogService>();
        if (audit is not null)
        {
            await audit.LogSystemOperationAsync(
                action: "EINVOICE_ACK_RECEIVED",
                entityType: "EinvoiceSubmission",
                userId: "system",
                userRole: "SuperAdmin",
                description: "Canary e-invoice acknowledged",
                actionType: AuditEventType.EinvoiceAckReceived,
                entityId: row.Id,
                tenantId: row.TenantId).ConfigureAwait(false);
        }

        var activity = services.GetService<IActivityEventPublisher>();
        if (activity is not null)
        {
            await activity.TryPublishAsync(
                row.TenantId,
                ActivityEventType.EinvoiceAckReceived,
                metadata: new
                {
                    submissionId = row.Id,
                    invoiceId = row.InvoiceId,
                    providerMessageId = row.ProviderMessageId,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RecordRetryAsync(
        IServiceProvider services,
        EinvoiceSubmission row,
        CancellationToken cancellationToken)
    {
        var audit = services.GetService<IAuditLogService>();
        if (audit is not null)
        {
            await audit.LogSystemOperationAsync(
                action: "EINVOICE_SUBMISSION_RETRY",
                entityType: "EinvoiceSubmission",
                userId: "system",
                userRole: "SuperAdmin",
                description: "Canary e-invoice status retry scheduled",
                actionType: AuditEventType.EinvoiceSubmissionRetry,
                entityId: row.Id,
                tenantId: row.TenantId).ConfigureAwait(false);
        }

        var activity = services.GetService<IActivityEventPublisher>();
        if (activity is not null)
        {
            await activity.TryPublishAsync(
                row.TenantId,
                ActivityEventType.EinvoiceSubmissionRetry,
                metadata: new
                {
                    submissionId = row.Id,
                    invoiceId = row.InvoiceId,
                    providerMessageId = row.ProviderMessageId,
                    attemptCount = row.ProviderAttemptCount,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}

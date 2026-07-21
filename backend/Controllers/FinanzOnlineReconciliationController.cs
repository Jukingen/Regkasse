using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: <b>legacy</b> payment-row FinanzOnline reconciliation (<b>derived</b> from <see cref="PaymentDetails"/> columns) — list and retry by payment id.
/// <b>Primary</b> submission lifecycle: <c>GET /api/admin/finanzonline-outbox</c> (<see cref="FinanzOnlineOutboxAdminController"/>).
/// This controller remains supported during phased deprecation; do not build new features on it.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/finanzonline-reconciliation")]
[Produces("application/json")]
public class FinanzOnlineReconciliationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IFinanzOnlineMetrics _metrics;
    private readonly ILogger<FinanzOnlineReconciliationController> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _sessionOptions;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _registrierkassenOptions;
    private readonly IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> _transmissionQueryOptions;
    private readonly IOptionsMonitor<FinanzOnlineSimulationDeveloperOptions> _simulationDeveloperOptions;
    private readonly IOptionsMonitor<FinanzOnlineSimulationOptions> _simulationScenarioOptions;

    public FinanzOnlineReconciliationController(
        AppDbContext context,
        IPaymentService paymentService,
        IFinanzOnlineMetrics metrics,
        ILogger<FinanzOnlineReconciliationController> logger,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<FinanzOnlineSessionOptions> sessionOptions,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> registrierkassenOptions,
        IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> transmissionQueryOptions,
        IOptionsMonitor<FinanzOnlineSimulationDeveloperOptions> simulationDeveloperOptions,
        IOptionsMonitor<FinanzOnlineSimulationOptions> simulationScenarioOptions)
    {
        _context = context;
        _paymentService = paymentService;
        _metrics = metrics;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _sessionOptions = sessionOptions;
        _registrierkassenOptions = registrierkassenOptions;
        _transmissionQueryOptions = transmissionQueryOptions;
        _simulationDeveloperOptions = simulationDeveloperOptions;
        _simulationScenarioOptions = simulationScenarioOptions;
    }

    /// <summary>
    /// GET: FinanzOnline submit metrics (finanzonline_submit_total, finanzonline_submit_failed_total by FailureKind). Counters reset on app restart.
    /// </summary>
    [HttpGet("metrics")]
    [Obsolete("Legacy reconciliation metrics surface. Prefer operational review via outbox admin API and logs; API may be narrowed in a future release.")]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    public ActionResult<FinanzOnlineMetricsResponse> GetMetrics()
    {
        var snap = _metrics.GetSnapshot();
        return Ok(new FinanzOnlineMetricsResponse
        {
            SubmitTotal = snap.SubmitTotal,
            SubmitFailedTotal = snap.SubmitFailedTotal,
            SubmitFailedTransient = snap.SubmitFailedTransient,
            SubmitFailedPermanent = snap.SubmitFailedPermanent,
            SubmitFailedUnknown = snap.SubmitFailedUnknown
        });
    }

    /// <summary>
    /// GET: List payments that need reconciliation (Pending, Failed, NeedsReconciliation). Optional filters.
    /// </summary>
    [HttpGet]
    [Obsolete("Legacy payment-row reconciliation list. Prefer GET /api/admin/finanzonline-outbox for SOAP pipeline state; this list remains for payment-centric triage until removal.")]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    public async Task<ActionResult<FinanzOnlineReconciliationListResponse>> GetReconciliationList(
        [FromQuery] string? status = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statuses = string.IsNullOrEmpty(status)
                ? new[] { "Pending", "Failed", "NeedsReconciliation" }
                : status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var query = _context.PaymentDetails
                .AsNoTracking()
                .Where(p => p.FinanzOnlineStatus != null && statuses.Contains(p.FinanzOnlineStatus));

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
                query = query.Where(p => p.CashRegisterId == cashRegisterId.Value);
            // Optional Austria calendar-day half-open filter on payment CreatedAt (query params are typically date-only).
            var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(fromUtc, toUtc);
            if (lo.HasValue)
                query = query.Where(p => p.CreatedAt >= lo.Value);
            if (hi.HasValue)
                query = query.Where(p => p.CreatedAt < hi.Value);

            var items = await query
                .OrderByDescending(p => p.FinanzOnlineLastAttemptAtUtc ?? p.CreatedAt)
                .Take(Math.Min(limit, 500))
                .Select(p => new FinanzOnlineReconciliationItemDto
                {
                    PaymentId = p.Id,
                    ReceiptNumber = p.ReceiptNumber,
                    CreatedAt = p.CreatedAt,
                    TotalAmount = p.TotalAmount,
                    CashRegisterId = p.CashRegisterId,
                    FinanzOnlineStatus = p.FinanzOnlineStatus,
                    FinanzOnlineError = p.FinanzOnlineError,
                    FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                    FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                    FinanzOnlineRetryCount = p.FinanzOnlineRetryCount
                })
                .ToListAsync(cancellationToken);

            await EnrichWithOutboxAsync(items, cancellationToken).ConfigureAwait(false);

            var transportSim = _sessionOptions.CurrentValue.UseSimulation
                || _registrierkassenOptions.CurrentValue.UseSimulation
                || _transmissionQueryOptions.CurrentValue.UseSimulation;
            var devProfile = FinanzOnlineSimulationDeveloperUi.ActiveProfileForAdminList(
                _hostEnvironment,
                _simulationScenarioOptions.CurrentValue,
                _simulationDeveloperOptions.CurrentValue);

            return Ok(new FinanzOnlineReconciliationListResponse
            {
                Total = items.Count,
                Items = items,
                FinanzOnlineTransportSimulationActive = transportSim,
                FinanzOnlineDeveloperSimulationProfile = devProfile
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FinanzOnline reconciliation list failed");
            return StatusCode(500, new { message = "Reconciliation list failed.", code = "RECONCILIATION_LIST_ERROR" });
        }
    }

    private async Task EnrichWithOutboxAsync(
        List<FinanzOnlineReconciliationItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return;

        var paymentIds = items.Select(i => i.PaymentId).ToList();
        var invoiceLinks = await _context.Invoices.AsNoTracking()
            .Where(i => i.SourcePaymentId != null && paymentIds.Contains(i.SourcePaymentId.Value))
            .Select(i => new { InvoiceId = i.Id, PaymentId = i.SourcePaymentId!.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (invoiceLinks.Count == 0)
            return;

        var invoiceIds = invoiceLinks.Select(x => x.InvoiceId).Distinct().ToList();
        var outboxRows = await _context.FinanzOnlineOutboxMessages.AsNoTracking()
            .Where(m => m.AggregateType == "Invoice" && invoiceIds.Contains(m.AggregateId))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latestByInvoice = new Dictionary<Guid, FinanzOnlineOutboxMessage>();
        foreach (var m in outboxRows)
        {
            if (!latestByInvoice.ContainsKey(m.AggregateId))
                latestByInvoice[m.AggregateId] = m;
        }

        var paymentToInvoice = invoiceLinks.GroupBy(x => x.PaymentId).ToDictionary(g => g.Key, g => g.First().InvoiceId);

        var anySim = _sessionOptions.CurrentValue.UseSimulation
            || _registrierkassenOptions.CurrentValue.UseSimulation
            || _transmissionQueryOptions.CurrentValue.UseSimulation;

        foreach (var item in items)
        {
            if (!paymentToInvoice.TryGetValue(item.PaymentId, out var invoiceId))
                continue;
            if (!latestByInvoice.TryGetValue(invoiceId, out var msg))
                continue;

            item.OutboxMessageId = msg.Id;
            item.OutboxStatus = msg.Status;
            item.OutboxLifecyclePhase = FinanzOnlineReconciliationOutboxMapper.MapLifecyclePhase(msg.Status);
            item.OutboxIsTerminal = FinanzOnlineReconciliationOutboxMapper.IsTerminalStatus(msg.Status);
            item.OutboxTransportPathKind = FinanzOnlineTransportPathKindResolver.Resolve(anySim, msg.Mode);
            item.OutboxTransmissionId = msg.TransmissionId;
            item.OutboxExternalReferenceId = msg.ExternalReferenceId;
            item.OutboxProtocolCode = msg.ProtocolCode;
            item.OutboxProtocolSummary = msg.ProtocolSummary;
            item.OutboxProcessedAtUtc = msg.ProcessedAt;
            item.OutboxLastResponsePreview = FinanzOnlineReconciliationOutboxMapper.TruncatePreview(msg.LastResponseJson);
        }
    }

    /// <summary>
    /// POST: Retry FinanzOnline submit for one payment. Idempotent if already Submitted.
    /// </summary>
    [HttpPost("retry/{paymentId:guid}")]
    [Obsolete("Legacy retry-by-payment. Re-queues via existing invoice/outbox path; keep until payment-row columns are retired or replaced.")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    public async Task<ActionResult<FinanzOnlineRetryResponse>> RetrySubmit(Guid paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _paymentService.RetryFinanzOnlineSubmitAsync(paymentId).ConfigureAwait(false);
            return Ok(new FinanzOnlineRetryResponse
            {
                Success = result.Success,
                Message = result.Success ? "Submitted" : (result.ErrorMessage ?? "Failed"),
                ReferenceId = result.ReferenceId,
                FailureKind = result.FailureKind.ToString(),
                SubmittedAt = result.SubmittedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FinanzOnline retry failed for PaymentId={PaymentId}", paymentId);
            return StatusCode(500, new { message = "Retry failed.", code = "RECONCILIATION_RETRY_ERROR" });
        }
    }
}

public class FinanzOnlineReconciliationListResponse
{
    public int Total { get; set; }
    public IReadOnlyList<FinanzOnlineReconciliationItemDto> Items { get; set; } = Array.Empty<FinanzOnlineReconciliationItemDto>();

    /// <summary>True when any FinanzOnline transport layer uses simulation (session, rkdb, or transmission query).</summary>
    public bool? FinanzOnlineTransportSimulationActive { get; set; }

    /// <summary>Non-production: active <c>FinanzOnline:Simulation:Developer</c> profile when not None (see <see cref="FinanzOnlineSimulationDeveloperUi"/>).</summary>
    public string? FinanzOnlineDeveloperSimulationProfile { get; set; }
}

public class FinanzOnlineReconciliationItemDto
{
    public Guid PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? FinanzOnlineStatus { get; set; }
    public string? FinanzOnlineError { get; set; }
    public string? FinanzOnlineReferenceId { get; set; }
    public DateTime? FinanzOnlineLastAttemptAtUtc { get; set; }
    public int FinanzOnlineRetryCount { get; set; }

    /// <summary>Authoritative outbox row for this payment's invoice (latest Invoice aggregate message).</summary>
    public Guid? OutboxMessageId { get; set; }
    public string? OutboxStatus { get; set; }
    public string? OutboxLifecyclePhase { get; set; }
    public bool? OutboxIsTerminal { get; set; }
    public string? OutboxTransmissionId { get; set; }
    public string? OutboxExternalReferenceId { get; set; }
    public string? OutboxProtocolCode { get; set; }
    public string? OutboxProtocolSummary { get; set; }
    public DateTime? OutboxProcessedAtUtc { get; set; }
    public string? OutboxLastResponsePreview { get; set; }

    /// <summary>Simulated | RealTest | RealProduction for the joined outbox row (live transport simulation flags + stored message mode).</summary>
    public string? OutboxTransportPathKind { get; set; }
}

public class FinanzOnlineRetryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string FailureKind { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class FinanzOnlineMetricsResponse
{
    public long SubmitTotal { get; set; }
    public long SubmitFailedTotal { get; set; }
    public long SubmitFailedTransient { get; set; }
    public long SubmitFailedPermanent { get; set; }
    public long SubmitFailedUnknown { get; set; }
}

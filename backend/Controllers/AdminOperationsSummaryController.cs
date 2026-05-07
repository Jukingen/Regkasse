using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin RKSV operations summary for dashboard first-glance signals.
/// Aggregates replay backlog/incident density/export risk without exposing raw metrics pipeline.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/operations")]
[Produces("application/json")]
public class AdminOperationsSummaryController : ControllerBase
{
    private static readonly string[] ReplayFailureActions =
    {
        "PAYLOAD_IMMUTABLE_MISMATCH",
        "MAX_RETRY_LIMIT_EXCEEDED",
        "OFFLINE_REPLAY_FAILED_FINAL",
        "OFFLINE_REPLAY_EXCEPTION_FINAL"
    };

    private readonly AppDbContext _context;
    private readonly IIntegrityCheckService _integrityCheckService;
    private readonly ILogger<AdminOperationsSummaryController> _logger;

    public AdminOperationsSummaryController(
        AppDbContext context,
        IIntegrityCheckService integrityCheckService,
        ILogger<AdminOperationsSummaryController> logger)
    {
        _context = context;
        _integrityCheckService = integrityCheckService;
        _logger = logger;
    }

    /// <summary>
    /// GET: Aggregated summary for RKSV operations dashboard (defaults to last 24h).
    /// </summary>
    [HttpGet("summary")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<AdminOperationsSummaryResponse>> GetSummary(
        [FromQuery] int windowHours = 24,
        CancellationToken cancellationToken = default)
    {
        windowHours = Math.Clamp(windowHours, 1, 168);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-windowHours);
        // All audit queries below: inclusive instant bounds on Timestamp (rolling UTC window, not calendar half-open).

        try
        {
            var replayPendingCount = await _context.OfflineTransactions
                .AsNoTracking()
                .CountAsync(
                    x => x.Status == OfflineTransactionStatus.Pending ||
                         x.Status == OfflineTransactionStatus.NonFiscalPending,
                    cancellationToken);

            var replayFailedCount = await _context.OfflineTransactions
                .AsNoTracking()
                .CountAsync(x => x.Status == OfflineTransactionStatus.Failed, cancellationToken);

            var replayBacklogCount = replayPendingCount + replayFailedCount;

            var replayFinalFailureAuditCount = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(
                    x => x.Timestamp >= fromUtc &&
                         x.Timestamp <= toUtc &&
                         ReplayFailureActions.Contains(x.Action),
                    cancellationToken);

            var replaySyncedAuditCount = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(
                    x => x.Timestamp >= fromUtc &&
                         x.Timestamp <= toUtc &&
                         x.Action == "OFFLINE_SYNCED",
                    cancellationToken);

            var incidentCorrelationCount = await _context.AuditLogs
                .AsNoTracking()
                .Where(
                    x => x.Timestamp >= fromUtc &&
                         x.Timestamp <= toUtc &&
                         !string.IsNullOrWhiteSpace(x.CorrelationId) &&
                         (x.Action == "OFFLINE_SYNCED" || ReplayFailureActions.Contains(x.Action)))
                .Select(x => x.CorrelationId!)
                .Distinct()
                .CountAsync(cancellationToken);

            var integrity = await _integrityCheckService.GetReportAsync(fromUtc, toUtc, includeDetails: false);
            var exportRiskSummary = new ExportRiskSummaryDto
            {
                SequenceDuplicateCount = integrity.SequenceIssues.DuplicateReceiptNumberCount,
                SequenceNonMonotonicCount = integrity.SequenceIssues.NonMonotonicSequenceCount,
                OrphanRefundCount = integrity.OrphanRefunds.OrphanRefundCount,
                PaymentWithoutInvoiceCount = integrity.PaymentWithoutInvoice.Count
            };
            exportRiskSummary.TotalRiskCount =
                exportRiskSummary.SequenceDuplicateCount +
                exportRiskSummary.SequenceNonMonotonicCount +
                exportRiskSummary.OrphanRefundCount +
                exportRiskSummary.PaymentWithoutInvoiceCount;
            exportRiskSummary.HasRisk = exportRiskSummary.TotalRiskCount > 0;

            return Ok(new AdminOperationsSummaryResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                WindowHours = windowHours,
                ReplayBacklogCount = replayBacklogCount,
                ReplayPendingCount = replayPendingCount,
                ReplayFailedCount = replayFailedCount,
                ReplayFinalFailureAuditCount = replayFinalFailureAuditCount,
                ReplaySyncedAuditCount = replaySyncedAuditCount,
                IncidentCorrelationCount = incidentCorrelationCount,
                ExportRisk = exportRiskSummary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin operations summary failed");
            return StatusCode(500, new { message = "Failed to build operations summary", code = "ADMIN_OPERATIONS_SUMMARY_ERROR" });
        }
    }
}

public class AdminOperationsSummaryResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int WindowHours { get; set; }
    public int ReplayBacklogCount { get; set; }
    public int ReplayPendingCount { get; set; }
    public int ReplayFailedCount { get; set; }
    public int ReplayFinalFailureAuditCount { get; set; }
    public int ReplaySyncedAuditCount { get; set; }
    public int IncidentCorrelationCount { get; set; }
    public ExportRiskSummaryDto ExportRisk { get; set; } = new();
}

public class ExportRiskSummaryDto
{
    public int SequenceDuplicateCount { get; set; }
    public int SequenceNonMonotonicCount { get; set; }
    public int OrphanRefundCount { get; set; }
    public int PaymentWithoutInvoiceCount { get; set; }
    public int TotalRiskCount { get; set; }
    public bool HasRisk { get; set; }
}

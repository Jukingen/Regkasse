using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: replay batch detail by correlation ID for incident debugging.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/replay-batch")]
[Produces("application/json")]
public class ReplayBatchController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReplayBatchController> _logger;

    public ReplayBatchController(AppDbContext context, ILogger<ReplayBatchController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET: Batch detail by correlation ID (Guid with or without dashes). Returns item counts, success/fail/duplicate summary, payments with receipt links, and audit correlation for log trace.
    /// </summary>
    [HttpGet("{correlationId}")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<ActionResult<ReplayBatchDetailResponse>> GetBatchDetail(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return BadRequest(new { message = "correlationId is required.", code = "REPLAY_BATCH_INVALID_ID" });

        if (!Guid.TryParse(correlationId.Replace("-", ""), out var batchId))
            return BadRequest(new { message = "correlationId must be a valid Guid.", code = "REPLAY_BATCH_INVALID_ID" });

        var response = await ReplayBatchDetailAssembler
            .BuildAsync(_context, batchId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Replay batch detail: CorrelationId={CorrelationId}, TotalItems={TotalItems}, SuccessCount={SuccessCount}, FailedOrDuplicate={FailedOrDuplicate}",
            batchId, response.TotalItems, response.SuccessCount, response.FailedOrDuplicateCount);

        return Ok(response);
    }
}

public sealed class ReplayBatchDetailResponse
{
    public Guid CorrelationId { get; set; }
    public int TotalItems { get; set; }
    public int SuccessCount { get; set; }
    public int FailedOrDuplicateCount { get; set; }
    /// <summary>Use for GET /api/AuditLog/correlation/{AuditCorrelationId} (log trace).</summary>
    public string AuditCorrelationId { get; set; } = string.Empty;
    public IReadOnlyList<ReplayBatchPaymentItemDto> Payments { get; set; } = Array.Empty<ReplayBatchPaymentItemDto>();
    /// <summary>Observability-only replay loop samples (device/sequence coverage). Supplementary to audit/fiscal counts.</summary>
    public int CoverageSampleCount { get; set; }
    /// <summary>Immutable audit events: fiscal sync completed for an offline intent in this batch.</summary>
    public int OfflineSyncedAuditCount { get; set; }
    /// <summary>Immutable audit events: terminal offline replay failure in this batch.</summary>
    public int OfflineFinalFailureAuditCount { get; set; }
}

public sealed class ReplayBatchPaymentItemDto
{
    public Guid? OfflineTransactionId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid? ReceiptId { get; set; }
    public string? ReceiptNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

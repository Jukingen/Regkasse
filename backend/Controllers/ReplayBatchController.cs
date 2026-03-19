using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;

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

        var totalItems = await _context.OfflineIntentCoverageSamples
            .AsNoTracking()
            .Where(s => s.ReplayBatchCorrelationId == batchId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentsInBatch = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p => p.OfflineReplayBatchCorrelationId == batchId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.OfflineTransactionId,
                p.ReceiptNumber,
                p.TotalAmount,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentIds = paymentsInBatch.Select(p => p.Id).ToList();
        var receiptByPayment = paymentIds.Count > 0
            ? await _context.Receipts
                .AsNoTracking()
                .Where(r => paymentIds.Contains(r.PaymentId))
                .Select(r => new { r.PaymentId, r.ReceiptId, r.ReceiptNumber })
                .ToDictionaryAsync(r => r.PaymentId, r => (r.ReceiptId, r.ReceiptNumber), cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, (Guid ReceiptId, string ReceiptNumber)>();

        var successCount = paymentsInBatch.Count;
        var failedOrDuplicateCount = totalItems > successCount ? totalItems - successCount : 0;

        var items = paymentsInBatch.Select(p =>
        {
            var hasReceipt = receiptByPayment.TryGetValue(p.Id, out var rec) && rec.Item1 != Guid.Empty;
            return new ReplayBatchPaymentItemDto
            {
                OfflineTransactionId = p.OfflineTransactionId,
                PaymentId = p.Id,
                ReceiptId = hasReceipt ? rec.Item1 : null,
                ReceiptNumber = hasReceipt ? (rec.Item2 ?? p.ReceiptNumber) : p.ReceiptNumber,
                TotalAmount = p.TotalAmount,
                CreatedAtUtc = p.CreatedAt
            };
        }).ToList();

        var response = new ReplayBatchDetailResponse
        {
            CorrelationId = batchId,
            TotalItems = totalItems,
            SuccessCount = successCount,
            FailedOrDuplicateCount = failedOrDuplicateCount,
            AuditCorrelationId = batchId.ToString("N"),
            Payments = items
        };

        _logger.LogInformation(
            "Replay batch detail: CorrelationId={CorrelationId}, TotalItems={TotalItems}, SuccessCount={SuccessCount}, FailedOrDuplicate={FailedOrDuplicate}",
            batchId, totalItems, successCount, failedOrDuplicateCount);

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

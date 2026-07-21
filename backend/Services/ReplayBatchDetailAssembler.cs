using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds replay batch detail from fiscal payment rows, immutable audit events, and optional coverage samples.
/// Coverage samples remain supplementary; totals prioritize audit + payments over coverage-only semantics.
/// </summary>
public static class ReplayBatchDetailAssembler
{
    private static readonly string[] OfflineFinalFailureAuditActions =
    {
        "PAYLOAD_IMMUTABLE_MISMATCH",
        "MAX_RETRY_LIMIT_EXCEEDED",
        "OFFLINE_REPLAY_FAILED_FINAL",
        "OFFLINE_REPLAY_EXCEPTION_FINAL"
    };

    public static async Task<ReplayBatchDetailResponse> BuildAsync(
        AppDbContext context,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var auditKey = batchId.ToString("N");

        var coverageSampleCount = await context.OfflineIntentCoverageSamples
            .AsNoTracking()
            .Where(s => s.ReplayBatchCorrelationId == batchId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var offlineSyncedAuditCount = await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.CorrelationId == auditKey && a.Action == "OFFLINE_SYNCED")
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var offlineFinalFailureAuditCount = await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.CorrelationId == auditKey && OfflineFinalFailureAuditActions.Contains(a.Action))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentsInBatch = await context.PaymentDetails
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
            ? await context.Receipts
                .AsNoTracking()
                .Where(r => paymentIds.Contains(r.PaymentId))
                .Select(r => new { r.PaymentId, r.ReceiptId, r.ReceiptNumber })
                .ToDictionaryAsync(r => r.PaymentId, r => (r.ReceiptId, r.ReceiptNumber), cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, (Guid ReceiptId, string ReceiptNumber)>();

        var fiscalizedPaymentCount = paymentsInBatch.Count;
        var auditActivityFloor = offlineSyncedAuditCount + offlineFinalFailureAuditCount;
        // Duplicates / no-op replays may increment coverage without a new OFFLINE_SYNCED row; floor with fiscalized count handles missing audits.
        var totalItems = Math.Max(
            coverageSampleCount,
            Math.Max(auditActivityFloor, fiscalizedPaymentCount));

        var failedOrDuplicateCount = Math.Max(0, totalItems - fiscalizedPaymentCount);

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

        return new ReplayBatchDetailResponse
        {
            CorrelationId = batchId,
            TotalItems = totalItems,
            SuccessCount = fiscalizedPaymentCount,
            FailedOrDuplicateCount = failedOrDuplicateCount,
            AuditCorrelationId = auditKey,
            Payments = items,
            CoverageSampleCount = coverageSampleCount,
            OfflineSyncedAuditCount = offlineSyncedAuditCount,
            OfflineFinalFailureAuditCount = offlineFinalFailureAuditCount
        };
    }
}

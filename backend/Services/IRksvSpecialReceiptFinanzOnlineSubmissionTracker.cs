using System;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Creates and attaches initial FinanzOnline submission tracking rows for eligible RKSV special receipts.
/// </summary>
public interface IRksvSpecialReceiptFinanzOnlineSubmissionTracker
{
    /// <summary>
    /// Builds a new row with status <see cref="RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending"/> (awaiting FinanzOnline outbox processing); caller adds to context and saves.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="kind"/> is not Startbeleg or Jahresbeleg.</exception>
    RksvSpecialReceiptFinanzOnlineSubmission CreateInitialNotRequiredRow(
        Guid paymentId,
        Guid receiptId,
        Guid cashRegisterId,
        string kind);

    /// <summary>Returns persisted tracking for a payment, if any.</summary>
    Task<RksvSpecialReceiptFinanzOnlineSubmission?> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);
}

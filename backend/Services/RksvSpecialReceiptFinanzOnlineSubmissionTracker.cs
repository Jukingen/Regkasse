using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class RksvSpecialReceiptFinanzOnlineSubmissionTracker : IRksvSpecialReceiptFinanzOnlineSubmissionTracker
{
    private readonly AppDbContext _db;

    public RksvSpecialReceiptFinanzOnlineSubmissionTracker(AppDbContext db)
    {
        _db = db;
    }

    public RksvSpecialReceiptFinanzOnlineSubmission CreateInitialPendingRow(
        Guid paymentId,
        Guid receiptId,
        Guid cashRegisterId,
        string kind)
    {
        if (kind != RksvSpecialReceiptKinds.Startbeleg && kind != RksvSpecialReceiptKinds.Jahresbeleg)
            throw new ArgumentException("Only Startbeleg and Jahresbeleg support FinanzOnline submission tracking.", nameof(kind));

        var now = DateTime.UtcNow;
        return new RksvSpecialReceiptFinanzOnlineSubmission
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            ReceiptId = receiptId,
            CashRegisterId = cashRegisterId,
            Kind = kind,
            Status = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending,
            AttemptCount = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public Task<RksvSpecialReceiptFinanzOnlineSubmission?> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default) =>
        _db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);
}

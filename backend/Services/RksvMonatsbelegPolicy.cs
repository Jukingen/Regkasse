using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class RksvMonatsbelegPolicy : IRksvMonatsbelegPolicy
{
    private readonly AppDbContext _db;
    private readonly TseOptions _tseOptions;

    public RksvMonatsbelegPolicy(AppDbContext db, IOptions<TseOptions> tseOptions)
    {
        _db = db;
        _tseOptions = tseOptions.Value;
    }

    /// <inheritdoc />
    public bool SessionGateApplies => !_tseOptions.IsOff && !_tseOptions.UseSoftTseWhenNoDevice;

    /// <inheritdoc />
    public Task<bool> HasMonatsbelegForRegisterMonthAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (month == 12)
        {
            // December: Jahresbeleg for the year satisfies the monthly gate (RKSV annual close).
            return _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == cashRegisterId &&
                         p.IsActive &&
                         (
                             (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg &&
                              p.RksvSpecialReceiptYear == year) ||
                             (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                              p.RksvSpecialReceiptYear == year &&
                              p.RksvSpecialReceiptMonth == 12)
                         ),
                    cancellationToken);
        }

        return _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == cashRegisterId &&
                     p.IsActive &&
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                     p.RksvSpecialReceiptYear == year &&
                     p.RksvSpecialReceiptMonth == month,
                cancellationToken);
    }
}

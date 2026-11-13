using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Resolves cashier/shift operational fields stamped on <see cref="DailyClosing"/>.</summary>
public static class DailyClosingOperationalResolver
{
    public static async Task StampOperationalFieldsAsync(
        AppDbContext db,
        DailyClosing closing,
        Guid cashRegisterId,
        string userId,
        Guid? activeShiftId = null,
        CancellationToken cancellationToken = default)
    {
        if (closing == null)
            throw new ArgumentNullException(nameof(closing));

        if (string.IsNullOrWhiteSpace(userId))
        {
            closing.CashierName = string.Empty;
            closing.ShiftNumber = 0;
            return;
        }

        var activeShift = (activeShiftId is Guid shiftId && shiftId != Guid.Empty)
            ? await db.CashierShifts.AsNoTracking()
                .Where(s => s.Id == shiftId)
                .Select(s => new { s.CashierName, s.Id })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : await db.CashierShifts.AsNoTracking()
                .Where(s =>
                    s.CashRegisterId == cashRegisterId
                    && s.CashierId == userId
                    && s.Status == CashierShiftStatuses.Active)
                .OrderByDescending(s => s.StartedAt)
                .Select(s => new { s.CashierName, s.Id })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        closing.CashierName = !string.IsNullOrWhiteSpace(activeShift?.CashierName)
            ? activeShift.CashierName
            : await ResolveCashierDisplayNameAsync(db, userId, cancellationToken).ConfigureAwait(false)
              ?? string.Empty;

        closing.ShiftNumber = activeShift != null
            ? await ResolveShiftSequenceNumberAsync(db, activeShift.Id, cancellationToken).ConfigureAwait(false)
            : 0;
    }
    public static async Task<int> ResolveShiftSequenceNumberAsync(
        AppDbContext db,
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        if (shiftId == Guid.Empty)
            return 0;

        var shift = await db.CashierShifts.AsNoTracking()
            .Where(s => s.Id == shiftId)
            .Select(s => new { s.CashRegisterId, s.StartedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (shift == null)
            return 0;

        return await ResolveShiftSequenceNumberAsync(
                db,
                shift.CashRegisterId,
                shift.StartedAt,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<int> ResolveShiftSequenceNumberAsync(
        AppDbContext db,
        Guid cashRegisterId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return 0;

        var startedUtc = startedAtUtc.Kind == DateTimeKind.Utc
            ? startedAtUtc
            : DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc);

        return await db.CashierShifts.AsNoTracking()
            .CountAsync(
                s => s.CashRegisterId == cashRegisterId && s.StartedAt <= startedUtc,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<string?> ResolveCashierDisplayNameAsync(
        AppDbContext db,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.UserName ?? u.Email)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

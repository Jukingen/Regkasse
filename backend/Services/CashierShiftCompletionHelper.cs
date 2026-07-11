using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Completes orphaned <see cref="CashierShift"/> rows when a register session ends outside <see cref="PosShiftService.EndShiftAsync"/>.
/// </summary>
internal static class CashierShiftCompletionHelper
{
    public static async Task<int> CompleteActiveShiftsForRegisterAsync(
        AppDbContext context,
        Guid tenantId,
        Guid cashRegisterId,
        string actorUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var activeShifts = await context.CashierShifts
            .Where(s => s.TenantId == tenantId
                        && s.CashRegisterId == cashRegisterId
                        && s.Status == CashierShiftStatuses.Active
                        && s.IsActive)
            .ToListAsync(cancellationToken);

        if (activeShifts.Count == 0)
            return 0;

        var endedAt = DateTime.UtcNow;
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        foreach (var shift in activeShifts)
        {
            shift.EndedAt = endedAt;
            shift.Status = CashierShiftStatuses.Completed;
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes)
                ? trimmedNote
                : trimmedNote == null
                    ? shift.Notes
                    : $"{shift.Notes}; {trimmedNote}";
            shift.UpdatedAt = endedAt;
            shift.UpdatedBy = actorUserId;
        }

        return activeShifts.Count;
    }
}

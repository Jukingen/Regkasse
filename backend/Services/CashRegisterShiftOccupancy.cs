using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Single source for operational shift occupancy on a cash register (<see cref="CashRegister.CurrentUserId"/>).
/// </summary>
/// <remarks>
/// <para><strong>Shared model (four paths)</strong>: selectable POS list, sole open auto-assignment, ensure-ready open-register branch,
/// and <see cref="CashRegisterResolutionService.ValidatePaymentRegisterAsync"/> all treat &quot;non-empty CurrentUserId ≠ caller&quot; as a
/// hard conflict for operational use (payment / ready / picker row / auto-assign).</para>
/// <para><strong>Exception (fifth path)</strong>: <see cref="CashRegisterResolutionService.ValidateAssignmentChangeAsync"/> uses the same
/// predicates when the principal lacks <see cref="AppPermissions.CashRegisterView"/>; with that permission, assignment may still persist
/// a preference pointing at another user&apos;s open shift (picker and payment remain blocked until occupancy clears).</para>
/// </remarks>
public static class CashRegisterShiftOccupancy
{
    /// <summary>
    /// True when <paramref name="currentUserIdOnRegister"/> identifies another user&apos;s operational shift claim on this register.
    /// </summary>
    public static bool IsHeldByOtherUser(string userId, string? currentUserIdOnRegister) =>
        !string.IsNullOrEmpty(currentUserIdOnRegister) &&
        !string.Equals(currentUserIdOnRegister, userId, StringComparison.Ordinal);

    /// <summary>
    /// True when the register row is safe for this user under the same occupancy model as payment (unclaimed shift or self).
    /// Typically used for open registers in the POS picker shortlist.
    /// </summary>
    public static bool UserMayOperateOpenRegisterShift(string userId, string? currentUserIdOnRegister) =>
        string.IsNullOrEmpty(currentUserIdOnRegister) ||
        string.Equals(currentUserIdOnRegister, userId, StringComparison.Ordinal);

    /// <summary>
    /// Manual assignment without <see cref="AppPermissions.CashRegisterView"/>: same occupancy rules as picker filtering for multi-register
    /// (must hold the shift on that register); sole register allows unclaimed or self (matches sole auto-assign eligibility).
    /// </summary>
    public static bool MayAssignRegisterWithoutCashRegisterView(
        string userId,
        CashRegister register,
        int totalRegisterCount)
    {
        if (totalRegisterCount == 1)
            return UserMayOperateOpenRegisterShift(userId, register.CurrentUserId);

        return !string.IsNullOrEmpty(register.CurrentUserId) &&
               string.Equals(register.CurrentUserId, userId, StringComparison.Ordinal);
    }
}

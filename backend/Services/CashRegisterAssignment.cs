using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Single source for the admin-managed cashier assignment on a cash register (<see cref="CashRegister.AssignedUserId"/>).
/// </summary>
/// <remarks>
/// <para><strong>Visibility only.</strong> Assignment decides which registers a POS user may see and pick
/// (<see cref="CashRegisterResolutionService.ListSelectableRegistersAsync"/>) and store as their preference
/// (<see cref="CashRegisterResolutionService.ValidateAssignmentChangeAsync"/>). It is deliberately absent from
/// payment authorization: <see cref="CashRegisterResolutionService.ValidatePaymentRegisterAsync"/> keeps using
/// operational shift ownership (<see cref="CashRegisterShiftOccupancy"/>) as the only authority.</para>
/// <para><strong>Null means shared.</strong> A register without an assignment stays selectable by every POS user of the
/// tenant, so existing deployments keep working unchanged until an admin assigns someone.</para>
/// <para>Distinct from <c>UserSettings.CashRegisterId</c>, which is the user's own persisted preference rather than an
/// admin decision, and from <see cref="CashRegister.CurrentUserId"/>, which is the open shift.</para>
/// </remarks>
public static class CashRegisterAssignment
{
    /// <summary>True when no cashier is assigned, i.e. the register is shared across the tenant's POS users.</summary>
    public static bool IsUnassigned(string? assignedUserId) => string.IsNullOrEmpty(assignedUserId);

    /// <summary>True when the register is assigned to exactly this user.</summary>
    public static bool IsAssignedTo(string userId, string? assignedUserId) =>
        !string.IsNullOrEmpty(assignedUserId) &&
        string.Equals(assignedUserId, userId, StringComparison.Ordinal);

    /// <summary>True when the register is assigned to somebody other than this user.</summary>
    public static bool IsAssignedToOtherUser(string userId, string? assignedUserId) =>
        !string.IsNullOrEmpty(assignedUserId) &&
        !string.Equals(assignedUserId, userId, StringComparison.Ordinal);

    /// <summary>
    /// Picker / preference rule for a non–Super Admin principal: unassigned (shared) or assigned to the caller.
    /// </summary>
    public static bool IsVisibleTo(string userId, string? assignedUserId) =>
        IsUnassigned(assignedUserId) || IsAssignedTo(userId, assignedUserId);
}

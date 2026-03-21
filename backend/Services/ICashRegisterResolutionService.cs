using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Payment-time and settings-time cash-register authorization (assignment changes and <see cref="ValidatePaymentRegisterAsync"/>).
/// POS session narrative (<c>nextAction</c>, optional auto-open, effective register preview) is produced only by
/// <see cref="IPosCashRegisterReadinessService.EnsureReadyForPosAsync"/>; <see cref="PaymentService.CreatePaymentAsync"/> does not call it and
/// authorizes the body register solely through <see cref="ValidatePaymentRegisterAsync"/> (aligned shift/assignment rules, separate code path).
/// Policy (see <see cref="CashRegisterShiftOccupancy"/> and implementation comments):
/// - UserSettings.CashRegisterId = persisted payment preference / assignment for the user.
/// - CashRegister.CurrentUserId = operational shift ownership (who opened the register).
/// - Operational occupancy (&quot;held by another user&quot;) is defined once in <see cref="CashRegisterShiftOccupancy"/> and shared by selectable list,
///   sole auto-assignment, ensure-ready, and payment validation.
/// - <see cref="AppPermissions.CashRegisterView"/>: intentionally allows <see cref="ValidateAssignmentChangeAsync"/> to accept assigning
///   an <see cref="RegisterStatus.Open"/> register even when <see cref="CashRegister.CurrentUserId"/> is another user (multi-register or sole);
///   the POS picker still omits those rows, and payment / ensure-ready still enforce shift ownership first (assignment never overrides occupancy).
/// - Payment on register R is allowed when R exists, is Open, R is not shift-claimed by another user, and one of:
///   (a) R.CurrentUserId == user, (b) exactly one register exists in DB and R is that register, (c) settings assignment matches R
///   (assignment does not override another user&apos;s shift).
/// </summary>
public interface ICashRegisterResolutionService
{
    /// <summary>
    /// When the user has no assignment and the database has exactly one cash-register row and that register is <see cref="RegisterStatus.Open"/>,
    /// and it is not on another user&apos;s shift, persist its id on settings.
    /// Does not run when the sole register is Closed (POS ensure-ready may open first, then persist assignment via readiness flow).
    /// </summary>
    Task ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(
        UserSettings userSettings,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates updating UserSettings.CashRegisterId. Null/empty clears assignment. Non-empty must reference an existing open register;
    /// principals with <see cref="AppPermissions.CashRegisterView"/> may assign any open register including one on another user&apos;s shift
    /// (preference only). Others may assign only registers they effectively &quot;own&quot; for assignment (sole unclaimed/own shift, or their shift row).
    /// </summary>
    Task<CashRegisterResolutionValidationResult> ValidateAssignmentChangeAsync(
        string userId,
        string? cashRegisterIdRaw,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Payment-time check for the body <c>CashRegisterId</c>: register exists, is open, shift not owned by another user,
    /// and assignment / sole-register rules pass. Independent of <see cref="IPosCashRegisterReadinessService.EnsureReadyForPosAsync"/> / <c>nextAction</c>.
    /// <see cref="AppPermissions.CashRegisterView"/> does not bypass another user&apos;s operational shift; <paramref name="principal"/> is not used to weaken that rule.
    /// </summary>
    Task<CashRegisterResolutionValidationResult> ValidatePaymentRegisterAsync(
        string userId,
        Guid requestedRegisterId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Domain projection: open registers the current user may select for POS assignment (self-service picker), after
    /// <see cref="AppPermissions.CashRegisterView"/> / sole-register / shift-ownership rules. Closed and maintenance/disabled rows are excluded.
    /// Registers open on another user&apos;s shift are excluded for all principals (including <see cref="AppPermissions.CashRegisterView"/>)
    /// so the list never shows payment-dead conflict rows; full inventory remains on separate admin endpoints.
    /// </summary>
    /// <remarks>
    /// Admin or reporting UIs that need every row (any status) must use inventory APIs (e.g. <c>GET /api/CashRegister</c>), not this method.
    /// </remarks>
    Task<IReadOnlyList<CashRegisterSelectableRow>> ListSelectableRegistersAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POS HTTP facade: same rows as <see cref="ListSelectableRegistersAsync"/> plus <see cref="PosSelectableListResult.EmptyReason"/> when the list is empty
    /// (<c>no_registers</c>, <c>none_open</c>, <c>none_selectable_for_user</c>). Exposed at <c>GET /api/pos/cash-register/selectable</c>.
    /// </summary>
    Task<PosSelectableListResult> ListSelectableForPosPickerAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Minimal row for POS picker (no sensitive fields).
/// </summary>
public sealed class CashRegisterSelectableRow
{
    public Guid Id { get; init; }
    public string RegisterNumber { get; init; } = string.Empty;
    public string? Location { get; init; }
}

/// <summary>
/// POS GET selectable response body: registers plus optional empty reason when count is zero.
/// </summary>
public sealed class PosSelectableListResult
{
    public IReadOnlyList<CashRegisterSelectableRow> Registers { get; init; } = Array.Empty<CashRegisterSelectableRow>();

    /// <summary>
    /// Set only when <see cref="Registers"/> is empty: <c>no_registers</c>, <c>none_open</c>, or <c>none_selectable_for_user</c>.
    /// </summary>
    public string? EmptyReason { get; init; }
}

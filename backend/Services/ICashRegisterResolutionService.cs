using System.Security.Claims;
using System.Text.Json.Serialization;
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
    /// and it is not on another user&apos;s shift nor assigned to another cashier (<see cref="CashRegister.AssignedUserId"/>), persist its id on settings.
    /// Does not run when the sole register is Closed (POS ensure-ready may open first, then persist assignment via readiness flow).
    /// </summary>
    Task ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(
        UserSettings userSettings,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates updating UserSettings.CashRegisterId. Null/empty clears assignment. Non-empty must reference an existing operational register —
    /// <see cref="RegisterStatus.Closed"/> is accepted so a cashier can store and then open their own register, while decommissioned,
    /// maintenance, disabled, and inactive rows are rejected. Beyond that, Super Admin may target any register and everyone else only
    /// registers that are unassigned or assigned to them (<see cref="CashRegister.AssignedUserId"/>).
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
    /// Payment commit gate: re-evaluates the same invariants as <see cref="ValidatePaymentRegisterAsync"/> on a fresh read of the register row
    /// after acquiring <c>SELECT … FOR UPDATE</c> on PostgreSQL (via <see cref="CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync"/>).
    /// Must be called only while an EF Core database transaction is active on the same <c>AppDbContext</c> instance as this service.
    /// </summary>
    Task<CashRegisterResolutionValidationResult> ValidatePaymentRegisterForCommitAsync(
        string userId,
        Guid requestedRegisterId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Domain projection: operational registers (active, <see cref="RegisterStatus.Open"/> or <see cref="RegisterStatus.Closed"/>) the current
    /// user may select in the POS picker. Closed rows are included so a cashier can pick and open their own register; maintenance / disabled /
    /// decommissioned / inactive rows are excluded. Non–Super Admin principals additionally see only registers that are unassigned or assigned
    /// to them (<see cref="CashRegisterAssignment"/>). Registers held on another user&apos;s shift are excluded for every principal so the list
    /// never shows payment-dead conflict rows; full inventory remains on separate admin endpoints.
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
    /// (<c>no_registers</c>, <c>none_assigned</c>, <c>none_selectable_for_user</c>). Exposed at <c>GET /api/pos/cash-register/selectable</c>.
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

    /// <summary>
    /// <see cref="RegisterStatus.Open"/> or <see cref="RegisterStatus.Closed"/>. A closed row is still selectable: POS opens it
    /// through <c>POST /api/pos/shift/auto-open</c> right after the user picks it, so the client can label it accordingly.
    /// </summary>
    /// <remarks>
    /// Serialized by name (<c>"Open"</c> / <c>"Closed"</c>) rather than the default ordinal, because the POS picker matches on the
    /// status name. The API has no global string-enum converter, so the converter has to sit on this property.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RegisterStatus Status { get; init; }

    /// <summary>
    /// Admin-managed cashier assignment. Null means the register is shared. POS uses this only for
    /// picker labeling / client-side visibility; payment authorization does not read it.
    /// </summary>
    public string? AssignedUserId { get; init; }
}

/// <summary>
/// POS GET selectable response body: registers plus optional empty reason when count is zero.
/// </summary>
public sealed class PosSelectableListResult
{
    public IReadOnlyList<CashRegisterSelectableRow> Registers { get; init; } = Array.Empty<CashRegisterSelectableRow>();

    /// <summary>
    /// Set only when <see cref="Registers"/> is empty: <c>no_registers</c>, <c>none_assigned</c>, or <c>none_selectable_for_user</c>.
    /// <c>none_open</c> is no longer produced (the picker lists closed registers too) but stays a known value for older clients.
    /// </summary>
    public string? EmptyReason { get; init; }
}

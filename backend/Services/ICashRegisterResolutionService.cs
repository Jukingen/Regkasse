using System.Security.Claims;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Single source of truth for POS cash-register resolution, assignment validation, and payment authorization.
/// POS session readiness (effective register, optional auto-open, nextAction/messageCode) is orchestrated by
/// <see cref="IPosCashRegisterReadinessService"/> / <see cref="PosCashRegisterReadinessService"/>; this interface
/// remains focused on assignment validation, payment authorization, and selectable-register listing.
/// Policy (see implementation comments):
/// - UserSettings.CashRegisterId = persisted payment preference / assignment for the user.
/// - CashRegister.CurrentUserId = operational shift ownership (who opened the register).
/// - Payment on register R is allowed when R exists, is Open, and one of:
///   (a) settings assignment matches R, (b) R.CurrentUserId == user, (c) exactly one register exists in DB and R is that register.
/// </summary>
public interface ICashRegisterResolutionService
{
    /// <summary>
    /// When the user has no assignment and the database has exactly one cash-register row and that register is <see cref="RegisterStatus.Open"/>,
    /// persist its id on settings. Does not run when the sole register is Closed (POS ensure-ready may open first, then persist assignment via readiness flow).
    /// </summary>
    Task ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(
        UserSettings userSettings,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates updating UserSettings.CashRegisterId. Null/empty clears assignment. Non-empty must be usable and selectable for the user.
    /// </summary>
    Task<CashRegisterResolutionValidationResult> ValidateAssignmentChangeAsync(
        string userId,
        string? cashRegisterIdRaw,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the authenticated user may post a fiscal payment on the requested register.
    /// </summary>
    Task<CashRegisterResolutionValidationResult> ValidatePaymentRegisterAsync(
        string userId,
        Guid requestedRegisterId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the user may pick for self-assignment (Open only). Uses CashRegisterView, sole-register, or shift ownership.
    /// </summary>
    Task<IReadOnlyList<CashRegisterSelectableRow>> ListSelectableRegistersAsync(
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

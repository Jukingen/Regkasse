using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Cash-register model (explicit):
/// - <see cref="UserSettings.CashRegisterId"/> = persisted POS payment preference / assignment for the user.
/// - <see cref="CashRegister.CurrentUserId"/> = operational shift ownership (who opened the register).
/// Payment is allowed when the register exists, <see cref="RegisterStatus.Open"/>, and one of:
/// settings assignment matches requested id, or shift owner matches user, or exactly one register row exists in the DB and the payment targets that register (sole-register payment fallback).
/// </summary>
/// <remarks>
/// <see cref="ApplySoleOpenRegisterAutoAssignmentIfNeededAsync"/> is separate: it persists settings only when there is
/// exactly one <see cref="CashRegister"/> row <em>and</em> that row is already <see cref="RegisterStatus.Open"/>.
/// It does not use “count of open registers”; a closed sole register is not auto-assigned here (POS ensure-ready may open it and persist elsewhere).
/// </remarks>
public sealed class CashRegisterResolutionService : ICashRegisterResolutionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CashRegisterResolutionService> _logger;

    public CashRegisterResolutionService(
        AppDbContext context,
        ILogger<CashRegisterResolutionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(
        UserSettings userSettings,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsMissingOrEmptyGuid(userSettings.CashRegisterId))
            return;

        var registers = await _context.CashRegisters
            .AsNoTracking()
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        if (registers.Count != 1)
            return;

        var only = registers[0];
        if (only.Status != RegisterStatus.Open)
        {
            _logger.LogInformation(
                "Sole cash register {RegisterId} is not Open (status {Status}); skipping auto-assignment for user {UserId}",
                only.Id,
                only.Status,
                userId);
            return;
        }

        userSettings.CashRegisterId = only.Id.ToString();
        userSettings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Auto-assigned sole open cash register {RegisterId} in user settings for user {UserId}",
            only.Id,
            userId);
    }

    /// <inheritdoc />
    public async Task<CashRegisterResolutionValidationResult> ValidateAssignmentChangeAsync(
        string userId,
        string? cashRegisterIdRaw,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterIdRaw == null)
            return CashRegisterResolutionValidationResult.Success(Guid.Empty, string.Empty);

        var trimmed = cashRegisterIdRaw.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return CashRegisterResolutionValidationResult.Success(Guid.Empty, string.Empty);

        if (!Guid.TryParse(trimmed, out var registerId) || registerId == Guid.Empty)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Invalid,
                "CashRegisterId must be a valid non-empty GUID.");
        }

        var register = await _context.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registerId, cancellationToken);

        if (register == null)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.NotFound,
                "Cash register not found.");
        }

        if (register.Status != RegisterStatus.Open)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Closed,
                "Cash register is not open and cannot be assigned for payment.");
        }

        if (!await CanUserSelectRegisterForAssignmentAsync(userId, register, principal, cancellationToken))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Forbidden,
                "You are not allowed to assign this cash register.");
        }

        return CashRegisterResolutionValidationResult.Success(register.Id, register.RegisterNumber);
    }

    /// <inheritdoc />
    public async Task<CashRegisterResolutionValidationResult> ValidatePaymentRegisterAsync(
        string userId,
        Guid requestedRegisterId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (requestedRegisterId == Guid.Empty)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Required,
                "CashRegisterId is required.");
        }

        var register = await _context.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestedRegisterId, cancellationToken);

        if (register == null)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.NotFound,
                "Cash register not found.");
        }

        if (register.Status != RegisterStatus.Open)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Closed,
                "Cash register is closed or not usable for payment.");
        }

        var settings = await _context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var assignedRaw = settings?.CashRegisterId?.Trim();
        var assignedMatches =
            !string.IsNullOrEmpty(assignedRaw) &&
            Guid.TryParse(assignedRaw, out var assignedGuid) &&
            assignedGuid != Guid.Empty &&
            assignedGuid == requestedRegisterId;

        var shiftMatches = !string.IsNullOrEmpty(register.CurrentUserId) &&
                           string.Equals(register.CurrentUserId, userId, StringComparison.Ordinal);

        var totalRegisters = await _context.CashRegisters.AsNoTracking().CountAsync(cancellationToken);
        var soleRegisterMatches = totalRegisters == 1 && register.Id == requestedRegisterId;

        if (assignedMatches || shiftMatches || soleRegisterMatches)
        {
            return CashRegisterResolutionValidationResult.Success(register.Id, register.RegisterNumber);
        }

        if (totalRegisters > 1 && IsMissingOrEmptyGuid(settings?.CashRegisterId))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.SelectionRequired,
                "Multiple cash registers exist; assign one in settings or use your shift register.");
        }

        return CashRegisterResolutionValidationResult.Failure(
            CashRegisterResolutionCodes.Forbidden,
            "Cash register is not authorized for this user.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CashRegisterSelectableRow>> ListSelectableRegistersAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var open = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.Status == RegisterStatus.Open)
            .OrderBy(r => r.RegisterNumber)
            .ToListAsync(cancellationToken);

        if (PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.CashRegisterView))
        {
            return open
                .Select(r => new CashRegisterSelectableRow
                {
                    Id = r.Id,
                    RegisterNumber = r.RegisterNumber,
                    Location = r.Location
                })
                .ToList();
        }

        var total = await _context.CashRegisters.AsNoTracking().CountAsync(cancellationToken);
        if (total == 1 && open.Count == 1)
        {
            var r = open[0];
            return new List<CashRegisterSelectableRow>
            {
                new()
                {
                    Id = r.Id,
                    RegisterNumber = r.RegisterNumber,
                    Location = r.Location
                }
            };
        }

        return open
            .Where(r => !string.IsNullOrEmpty(r.CurrentUserId) &&
                        string.Equals(r.CurrentUserId, userId, StringComparison.Ordinal))
            .Select(r => new CashRegisterSelectableRow
            {
                Id = r.Id,
                RegisterNumber = r.RegisterNumber,
                Location = r.Location
            })
            .ToList();
    }

    private async Task<bool> CanUserSelectRegisterForAssignmentAsync(
        string userId,
        CashRegister register,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.CashRegisterView))
            return true;

        var total = await _context.CashRegisters.AsNoTracking().CountAsync(cancellationToken);
        if (total == 1)
            return true;

        return !string.IsNullOrEmpty(register.CurrentUserId) &&
               string.Equals(register.CurrentUserId, userId, StringComparison.Ordinal);
    }

    private static bool IsMissingOrEmptyGuid(string? cashRegisterId)
    {
        if (string.IsNullOrWhiteSpace(cashRegisterId))
            return true;
        return Guid.TryParse(cashRegisterId.Trim(), out var g) && g == Guid.Empty;
    }
}

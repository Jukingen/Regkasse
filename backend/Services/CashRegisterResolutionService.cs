using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Cash-register model (explicit). Operational shift occupancy is centralized in <see cref="CashRegisterShiftOccupancy"/> and reused by
/// <see cref="ListSelectableRegistersAsync"/>, <see cref="ApplySoleOpenRegisterAutoAssignmentIfNeededAsync"/>, <see cref="ValidatePaymentRegisterAsync"/>, <see cref="ValidatePaymentRegisterForCommitAsync"/>,
/// and <see cref="PosCashRegisterReadinessService"/> (ensure-ready).
/// - <see cref="UserSettings.CashRegisterId"/> = persisted POS payment preference / assignment for the user.
/// - <see cref="CashRegister.CurrentUserId"/> = operational shift ownership (who opened the register).
/// - <see cref="CashRegister.AssignedUserId"/> = admin-managed cashier assignment, a visibility rule only (see <see cref="CashRegisterAssignment"/>).
/// It scopes <see cref="ListSelectableRegistersAsync"/> and <see cref="ValidateAssignmentChangeAsync"/> for non–Super Admins and is deliberately
/// absent from <see cref="ValidatePaymentRegisterAsync"/>: payment authority stays with the operational shift.
/// - <see cref="AppPermissions.CashRegisterView"/> widens <see cref="ValidateAssignmentChangeAsync"/> only: a register on another
/// user&apos;s shift may still be saved as a preference. <see cref="ListSelectableRegistersAsync"/> keeps an assigned register
/// that is open on someone else&apos;s shift in the picker as not selectable; <see cref="ValidatePaymentRegisterAsync"/> always
/// rejects payment on it for the non-owner.
/// Payment is allowed when the register exists, <see cref="RegisterStatus.Open"/>, and no other user holds the operational shift
/// (<see cref="CashRegister.CurrentUserId"/>). Settings assignment and sole-register rules apply only after that occupancy check:
/// they never override another user&apos;s shift (same conflict semantics as <see cref="PosCashRegisterReadinessService"/>, separate code path).
/// </summary>
/// <remarks>
/// <see cref="ApplySoleOpenRegisterAutoAssignmentIfNeededAsync"/> is separate: it persists settings when POS operational
/// cardinality is exactly one register (<see cref="CashRegisterPosOperationalCardinality"/>) <em>or</em> a tenant-default
/// operational register exists, and that candidate is already <see cref="RegisterStatus.Open"/>
/// and not on another user&apos;s shift (<see cref="CashRegister.CurrentUserId"/>).
/// A closed sole/default operational register is not auto-assigned here (POS ensure-ready may open it and persist elsewhere).
/// </remarks>
public sealed class CashRegisterResolutionService : ICashRegisterResolutionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CashRegisterResolutionService> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly IRksvStartbelegPolicy _rksvStartbelegPolicy;
    private readonly IRksvMonatsbelegPolicy _rksvMonatsbelegPolicy;

    public CashRegisterResolutionService(
        AppDbContext context,
        ILogger<CashRegisterResolutionService> logger,
        ISettingsTenantResolver settingsTenantResolver,
        IRksvStartbelegPolicy rksvStartbelegPolicy,
        IRksvMonatsbelegPolicy rksvMonatsbelegPolicy)
    {
        _context = context;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
        _rksvStartbelegPolicy = rksvStartbelegPolicy;
        _rksvMonatsbelegPolicy = rksvMonatsbelegPolicy;
    }

    /// <inheritdoc />
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(
        UserSettings userSettings,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsMissingOrEmptyGuid(userSettings.CashRegisterId))
            return;

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var registers = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var only = CashRegisterPosOperationalCardinality.GetSingleOperationalRegisterOrNull(registers);
        CashRegister? candidate = only;
        var assignReason = "sole";

        // When multiple operational registers exist, align with FA: prefer the tenant default if it is Open.
        if (candidate == null)
        {
            candidate = registers.FirstOrDefault(r =>
                r.IsDefaultForTenant &&
                CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(r));
            assignReason = "tenant_default";
        }

        if (candidate == null)
            return;

        if (CashRegisterAssignment.IsAssignedToOtherUser(userId, candidate.AssignedUserId))
        {
            _logger.LogInformation(
                "{Reason} cash register {RegisterId} is assigned to user {AssignedUserId}; skipping auto-assignment for user {UserId}",
                assignReason,
                candidate.Id,
                candidate.AssignedUserId,
                userId);
            return;
        }

        if (candidate.Status != RegisterStatus.Open)
        {
            _logger.LogInformation(
                "{Reason} cash register {RegisterId} is not Open (status {Status}); skipping auto-assignment for user {UserId}",
                assignReason,
                candidate.Id,
                candidate.Status,
                userId);
            return;
        }

        if (CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, candidate.CurrentUserId))
        {
            _logger.LogInformation(
                "{Reason} cash register {RegisterId} is on shift user {ShiftUserId}; skipping auto-assignment for user {UserId}",
                assignReason,
                candidate.Id,
                candidate.CurrentUserId,
                userId);
            return;
        }

        userSettings.CashRegisterId = candidate.Id.ToString();
        userSettings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Auto-assigned {Reason} open cash register {RegisterId} in user settings for user {UserId}",
            assignReason,
            candidate.Id,
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

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var register = await _context.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registerId && r.TenantId == tenantId, cancellationToken);

        if (register == null)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.NotFound,
                "Cash register not found.");
        }

        if (register.Status == RegisterStatus.Decommissioned)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Decommissioned,
                "Cash register is permanently decommissioned (RKSV Schlussbeleg) and cannot be assigned.");
        }

        // Closed is accepted on purpose: POS opens the picked register through shift auto-open right after this saves.
        if (!CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(register))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Closed,
                "Cash register is not operational (maintenance, disabled, or inactive) and cannot be assigned.");
        }

        if (!CanUserSelectRegisterForAssignment(userId, register, principal))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Forbidden,
                "You are not allowed to assign this cash register.");
        }

        return CashRegisterResolutionValidationResult.Success(register.Id, register.RegisterNumber);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="principal"/> is currently unused in the body; occupancy is evaluated before assignment/sole fallbacks so that
    /// <see cref="AppPermissions.CashRegisterView"/> (or any future claim) cannot authorize payment on another user&apos;s shift.
    /// </remarks>
    public async Task<CashRegisterResolutionValidationResult> ValidatePaymentRegisterAsync(
        string userId,
        Guid requestedRegisterId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _ = principal;
        if (requestedRegisterId == Guid.Empty)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Required,
                "CashRegisterId is required.");
        }

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var register = await _context.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestedRegisterId && r.TenantId == tenantId, cancellationToken);

        var settings = await _context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var operationalRegisterCount = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .WhereCountsTowardPosOperationalCardinality()
            .CountAsync(cancellationToken);

        return await EvaluatePaymentRegisterPolicyAsync(
            userId,
            requestedRegisterId,
            register,
            settings,
            operationalRegisterCount,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CashRegisterResolutionValidationResult> ValidatePaymentRegisterForCommitAsync(
        string userId,
        Guid requestedRegisterId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _ = principal;
        if (requestedRegisterId == Guid.Empty)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Required,
                "CashRegisterId is required.");
        }

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        await CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync(
            _context,
            requestedRegisterId,
            cancellationToken);

        var register = await _context.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestedRegisterId && r.TenantId == tenantId, cancellationToken);

        var settings = await _context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var operationalRegisterCount = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .WhereCountsTowardPosOperationalCardinality()
            .CountAsync(cancellationToken);

        return await EvaluatePaymentRegisterPolicyAsync(
            userId,
            requestedRegisterId,
            register,
            settings,
            operationalRegisterCount,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CashRegisterResolutionValidationResult> EvaluatePaymentRegisterPolicyAsync(
        string userId,
        Guid requestedRegisterId,
        CashRegister? register,
        UserSettings? settings,
        int operationalRegisterCount,
        CancellationToken cancellationToken)
    {
        var core = EvaluatePaymentRegisterCore(userId, requestedRegisterId, register, settings, operationalRegisterCount);
        if (!core.Ok)
            return core;

        if (_rksvStartbelegPolicy.SessionGateApplies &&
            !await _rksvStartbelegPolicy.HasStartbelegForRegisterAsync(requestedRegisterId, cancellationToken)
                .ConfigureAwait(false))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.StartbelegRequired,
                "RKSV Startbeleg must be created before sales on this cash register.");
        }

        var monatsbelegGate = await _rksvMonatsbelegPolicy
            .EvaluateSalesGateAsync(requestedRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (monatsbelegGate.BlocksSales)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.MonatsbelegRequired,
                "RKSV Monatsbeleg must be created for the previous completed calendar month before sales on this cash register.");
        }

        return core;
    }

    /// <summary>
    /// Core occupancy / assignment rules (without RKSV Startbeleg / Monatsbeleg gates).
    /// </summary>
    private static CashRegisterResolutionValidationResult EvaluatePaymentRegisterCore(
        string userId,
        Guid requestedRegisterId,
        CashRegister? register,
        UserSettings? settings,
        int operationalRegisterCount)
    {
        if (register == null)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.NotFound,
                "Cash register not found.");
        }

        if (register.Status == RegisterStatus.Decommissioned)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Decommissioned,
                "Cash register is permanently decommissioned (RKSV Schlussbeleg) and cannot accept payments.");
        }

        if (register.Status != RegisterStatus.Open)
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Closed,
                "Cash register is closed or not usable for payment.");
        }

        var shiftOccupantId = register.CurrentUserId;
        if (CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, shiftOccupantId))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Forbidden,
                "Cash register is in use by another user.");
        }

        var shiftHeldByCurrentUser = !string.IsNullOrEmpty(shiftOccupantId) &&
                                     string.Equals(shiftOccupantId, userId, StringComparison.Ordinal);

        var assignedRaw = settings?.CashRegisterId?.Trim();
        var assignedMatches =
            !string.IsNullOrEmpty(assignedRaw) &&
            Guid.TryParse(assignedRaw, out var assignedGuid) &&
            assignedGuid != Guid.Empty &&
            assignedGuid == requestedRegisterId;

        var soleRegisterMatches = operationalRegisterCount == 1 && register.Id == requestedRegisterId;

        if (shiftHeldByCurrentUser || soleRegisterMatches || assignedMatches)
        {
            return CashRegisterResolutionValidationResult.Success(register.Id, register.RegisterNumber);
        }

        if (operationalRegisterCount > 1 && IsMissingOrEmptyGuid(settings?.CashRegisterId))
        {
            return CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.SelectionRequired,
                "Multiple operational cash registers exist; assign one in settings or use your shift register.");
        }

        return CashRegisterResolutionValidationResult.Failure(
            CashRegisterResolutionCodes.Forbidden,
            "Cash register is not authorized for this user.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// POS clients consume this via <see cref="ListSelectableForPosPickerAsync"/> (<c>GET /api/pos/cash-register/selectable</c>).
    /// Two filters apply, in this order:
    /// <list type="number">
    /// <item>assignment — a non–Super Admin sees only registers whose <see cref="CashRegister.AssignedUserId"/> is
    /// their user id (<see cref="CashRegisterAssignment.IsAssignedTo"/>). Unassigned registers stay on the admin
    /// inventory; POS does not offer them and does not create one.</item>
    /// <item>occupancy — an <see cref="RegisterStatus.Open"/> register held on another user&apos;s shift
    /// (<see cref="CashRegister.CurrentUserId"/>) is omitted unless it is assigned to the caller. An assigned row stays
    /// visible with <see cref="CashRegisterSelectableRow.IsSelectable"/> false and a German
    /// <see cref="CashRegisterSelectableRow.StatusText"/>. A <see cref="RegisterStatus.Closed"/> row keeps its place
    /// even when <c>CurrentUserId</c> is stale, so the assigned cashier can open it.</item>
    /// </list>
    /// <see cref="RegisterStatus.Closed"/> rows are listed: picking one triggers <c>POST /api/pos/shift/auto-open</c>, which opens it.
    /// <see cref="AppPermissions.CashRegisterView"/> no longer widens this list — Cashier and Waiter both hold it, so it could never
    /// express the assignment rule. Full inventory listing stays on admin APIs. Super Admin still sees every operational register
    /// that is not held on someone else&apos;s shift.
    /// </remarks>
    public async Task<IReadOnlyList<CashRegisterSelectableRow>> ListSelectableRegistersAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var operational = await _context.CashRegisters
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .WhereCountsTowardPosOperationalCardinality()
            .OrderBy(r => r.RegisterNumber)
            .ToListAsync(cancellationToken);

        var seesEveryAssignment = PermissionClaimHelper.IsSuperAdminPrincipal(principal);

        var candidates = operational
            .Where(r => seesEveryAssignment || CashRegisterAssignment.IsAssignedTo(userId, r.AssignedUserId))
            .Select(r => new
            {
                Register = r,
                HeldByOther = r.Status == RegisterStatus.Open
                    && CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, r.CurrentUserId),
            })
            .Where(x => !x.HeldByOther || CashRegisterAssignment.IsAssignedTo(userId, x.Register.AssignedUserId))
            .ToList();

        var holderIds = candidates
            .Where(x => x.HeldByOther && !string.IsNullOrEmpty(x.Register.CurrentUserId))
            .Select(x => x.Register.CurrentUserId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var holderNames = await LoadUserDisplayNamesAsync(holderIds, cancellationToken).ConfigureAwait(false);

        return candidates
            .Select(x =>
            {
                string? statusText = null;
                if (x.HeldByOther)
                {
                    var holderId = x.Register.CurrentUserId ?? string.Empty;
                    var name = holderNames.TryGetValue(holderId, out var resolved) && !string.IsNullOrWhiteSpace(resolved)
                        ? resolved
                        : holderId;
                    statusText = $"In Benutzung von {name}";
                }

                return new CashRegisterSelectableRow
                {
                    Id = x.Register.Id,
                    RegisterNumber = x.Register.RegisterNumber,
                    Location = x.Register.Location,
                    Status = x.Register.Status,
                    AssignedUserId = x.Register.AssignedUserId,
                    IsSelectable = !x.HeldByOther,
                    StatusText = statusText,
                };
            })
            .ToList();
    }

    private async Task<Dictionary<string, string>> LoadUserDisplayNamesAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            names[user.Id] = !string.IsNullOrEmpty(fullName)
                ? fullName
                : (string.IsNullOrWhiteSpace(user.UserName) ? user.Id : user.UserName);
        }

        return names;
    }

    /// <inheritdoc />
    public async Task<PosSelectableListResult> ListSelectableForPosPickerAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var registers = await ListSelectableRegistersAsync(userId, principal, cancellationToken);
        var seesEveryAssignment = PermissionClaimHelper.IsSuperAdminPrincipal(principal);
        if (registers.Count > 0)
        {
            _logger.LogDebug(
                "PosSelectable resolved: UserId={UserId} returnedCount={Count} emptyReason=null isSuperAdmin={IsSuperAdmin}",
                userId,
                registers.Count,
                seesEveryAssignment);
            return new PosSelectableListResult { Registers = registers, EmptyReason = null };
        }

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var totalRows = await _context.CashRegisters.AsNoTracking().CountAsync(r => r.TenantId == tenantId, cancellationToken);
        var operationalRows = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .WhereCountsTowardPosOperationalCardinality()
            .Select(r => new { r.Status, r.CurrentUserId, r.AssignedUserId })
            .ToListAsync(cancellationToken);

        var operationalTotal = operationalRows.Count;
        var openRows = operationalRows.Count(r => r.Status == RegisterStatus.Open);
        var assignedToCaller = operationalRows
            .Count(r => CashRegisterAssignment.IsAssignedTo(userId, r.AssignedUserId));
        var visibleByAssignment = seesEveryAssignment ? operationalTotal : assignedToCaller;
        var assignedToOtherUsers = operationalRows
            .Count(r => CashRegisterAssignment.IsAssignedToOtherUser(userId, r.AssignedUserId));
        var heldByOtherShift = operationalRows
            .Count(r => r.Status == RegisterStatus.Open
                && CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, r.CurrentUserId));

        // Distinguishes an empty picker while registers exist. An assigned register that is open on someone else's
        // shift is returned as not selectable, so it does not produce none_selectable_for_user. That reason remains
        // when nothing assigned to the caller is visible (for example a Super Admin whose every operational register
        // is held on another shift).
        string emptyReason;
        if (operationalTotal == 0)
        {
            emptyReason = "no_registers";
        }
        else if (!seesEveryAssignment && assignedToCaller == 0)
        {
            emptyReason = "none_assigned";
        }
        else
        {
            emptyReason = "none_selectable_for_user";
        }

        _logger.LogInformation(
            "PosSelectable empty: UserId={UserId} totalRows={TotalRows} operationalRows={OperationalRows} openRows={OpenRows} visibleByAssignment={VisibleByAssignment} assignedToOtherUsers={AssignedToOtherUsers} heldByOtherShift={HeldByOtherShift} selectableReturned=0 emptyReason={EmptyReason} isSuperAdmin={IsSuperAdmin}. " +
            "Operational = active + (Open or Closed); the picker lists both and opens a closed pick on selection; non-operational = Maintenance/Disabled/Decommissioned or inactive.",
            userId,
            totalRows,
            operationalTotal,
            openRows,
            visibleByAssignment,
            assignedToOtherUsers,
            heldByOtherShift,
            emptyReason,
            seesEveryAssignment);

        if (totalRows > 0 && operationalTotal == 0)
        {
            var excluded = await _context.CashRegisters.AsNoTracking()
                .Where(r => r.TenantId == tenantId)
                .OrderBy(r => r.RegisterNumber)
                .Select(r => new { r.Id, r.RegisterNumber, StatusCode = (int)r.Status, r.IsActive })
                .Take(25)
                .ToListAsync(cancellationToken);
            _logger.LogInformation(
                "PosSelectable: all {TotalRows} cash_registers rows excluded from operational cardinality (sample): {@ExcludedSample}",
                totalRows,
                excluded);
        }

        return new PosSelectableListResult { Registers = registers, EmptyReason = emptyReason };
    }

    /// <summary>
    /// Assignment API gate for <see cref="ValidateAssignmentChangeAsync"/> (not payment, not picker): mirrors the picker's
    /// assignment rule so a user can only store a preference for a register they are actually allowed to see.
    /// Super Admin may target any register; everyone else needs it unassigned or assigned to themselves.
    /// </summary>
    /// <remarks>
    /// Deliberately does not consider <see cref="CashRegister.CurrentUserId"/>: persisting a preference for a register that is
    /// momentarily on another user&apos;s shift stays allowed (e.g. a waiter's default register), exactly as before. Occupancy is
    /// still enforced where it matters — the picker omits those rows and <see cref="ValidatePaymentRegisterAsync"/> rejects payment
    /// on them for the non-owner.
    /// </remarks>
    private static bool CanUserSelectRegisterForAssignment(
        string userId,
        CashRegister register,
        ClaimsPrincipal principal)
    {
        if (PermissionClaimHelper.IsSuperAdminPrincipal(principal))
            return true;

        return CashRegisterAssignment.IsVisibleTo(userId, register.AssignedUserId);
    }

    private static bool IsMissingOrEmptyGuid(string? cashRegisterId)
    {
        if (string.IsNullOrWhiteSpace(cashRegisterId))
            return true;
        return Guid.TryParse(cashRegisterId.Trim(), out var g) && g == Guid.Empty;
    }

}

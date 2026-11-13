using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>Outcome of a cash register permission check, mapped to an HTTP status by the caller.</summary>
public enum CashRegisterPermissionDecision
{
    /// <summary>Actor may perform the operation on this register.</summary>
    Allowed,

    /// <summary>Register is missing, or belongs to another tenant — both look identical to the caller (HTTP 404).</summary>
    NotFound,

    /// <summary>Register is reachable but the actor's role or assignment does not allow the operation (HTTP 403).</summary>
    Forbidden,

    /// <summary>Register and actor are fine, but the operation's target (e.g. the assignee) is invalid (HTTP 400).</summary>
    InvalidTarget,
}

/// <summary>Machine-readable reasons attached to a denied <see cref="CashRegisterPermissionResult"/>.</summary>
public static class CashRegisterPermissionCodes
{
    public const string RegisterNotFound = "REGISTER_NOT_FOUND";
    public const string ManagePermissionRequired = "CASH_REGISTER_MANAGE_REQUIRED";
    public const string OperationPermissionRequired = "CASH_REGISTER_OPERATION_NOT_PERMITTED";
    public const string RegisterNotAssignedToActor = "REGISTER_NOT_ASSIGNED_TO_ACTOR";
    /// <summary>Open shift is held by someone else — only that operator may close (HTTP 403).</summary>
    public const string RegisterHeldByOtherUser = "REGISTER_HELD_BY_OTHER";
    public const string ActorNotAuthenticated = "ACTOR_NOT_AUTHENTICATED";

    /// <summary>Kept identical to the code <c>CashRegisterManagementService</c> already surfaces for this case.</summary>
    public const string AssigneeNotInTenant = "ASSIGNEE_NOT_IN_TENANT";
}

/// <summary>Decision of a single cash register permission check.</summary>
public sealed class CashRegisterPermissionResult
{
    public CashRegisterPermissionDecision Decision { get; init; }

    /// <summary>Reason code for denials; null when allowed.</summary>
    public string? Code { get; init; }

    public bool IsAllowed => Decision == CashRegisterPermissionDecision.Allowed;

    public static CashRegisterPermissionResult Allow() =>
        new() { Decision = CashRegisterPermissionDecision.Allowed };

    public static CashRegisterPermissionResult NotFound() =>
        new() { Decision = CashRegisterPermissionDecision.NotFound, Code = CashRegisterPermissionCodes.RegisterNotFound };

    public static CashRegisterPermissionResult Forbidden(string code) =>
        new() { Decision = CashRegisterPermissionDecision.Forbidden, Code = code };

    public static CashRegisterPermissionResult InvalidTarget(string code) =>
        new() { Decision = CashRegisterPermissionDecision.InvalidTarget, Code = code };
}

/// <summary>
/// Answers "may this actor do X on this cash register?" for admin and RKSV Sonderbeleg routes.
/// </summary>
/// <remarks>
/// Layered below the <c>[HasPermission]</c> policy gate, which already decides whether the actor's role owns the
/// permission at all. This service adds the two per-register questions the attribute cannot answer: whether the
/// register is reachable for the actor's tenant, and whether an operational (POS-level) actor is allowed on this
/// particular register.
/// </remarks>
public interface ICashRegisterPermissionService
{
    /// <summary>
    /// Manager-level assignment of a cashier to a register. Pass <c>null</c> as <paramref name="targetUserId"/> to clear.
    /// </summary>
    Task<CashRegisterPermissionResult> CanAssignUserAsync(
        Guid registerId,
        string? targetUserId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);

    Task<CashRegisterPermissionResult> CanOpenAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);

    Task<CashRegisterPermissionResult> CanCloseAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);

    Task<CashRegisterPermissionResult> CanViewAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);

    Task<CashRegisterPermissionResult> CanCreateSonderbelegAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ICashRegisterPermissionService"/>
public sealed class CashRegisterPermissionService : ICashRegisterPermissionService
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<CashRegisterPermissionService> _logger;

    public CashRegisterPermissionService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        ILogger<CashRegisterPermissionService> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task<CashRegisterPermissionResult> CanAssignUserAsync(
        Guid registerId,
        string? targetUserId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        var (register, denial) = await ResolveReachableRegisterAsync(registerId, principal, "assign", cancellationToken)
            .ConfigureAwait(false);
        if (denial != null)
            return denial;

        // Assignment is a back-office decision: no operational fallback for Cashier / Waiter.
        if (!IsElevatedActor(principal))
            return Deny("assign", registerId, principal, CashRegisterPermissionCodes.ManagePermissionRequired);

        var normalizedTarget = string.IsNullOrWhiteSpace(targetUserId) ? null : targetUserId.Trim();
        if (normalizedTarget == null)
            return CashRegisterPermissionResult.Allow();

        var isActiveMember = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                m => m.UserId == normalizedTarget && m.TenantId == register!.TenantId && m.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        if (!isActiveMember)
        {
            _logger.LogWarning(
                "Cash register assign rejected: target user is not an active member RegisterId={RegisterId} TenantId={TenantId} TargetUserId={TargetUserId}",
                registerId,
                register!.TenantId,
                normalizedTarget);
            return CashRegisterPermissionResult.InvalidTarget(CashRegisterPermissionCodes.AssigneeNotInTenant);
        }

        return CashRegisterPermissionResult.Allow();
    }

    public Task<CashRegisterPermissionResult> CanOpenAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default) =>
        EvaluateOperationalAsync(registerId, principal, "open", AppPermissions.ShiftOpen, cancellationToken);

    public Task<CashRegisterPermissionResult> CanCloseAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default) =>
        EvaluateOperationalAsync(
            registerId,
            principal,
            "close",
            AppPermissions.ShiftClose,
            cancellationToken,
            allowCurrentShiftHolder: true);

    /// <summary>
    /// Read access is tenant reachability plus <see cref="AppPermissions.CashRegisterView"/>. Assignment deliberately
    /// plays no part here: it scopes the POS picker (<see cref="CashRegisterResolutionService"/>), while reporting roles
    /// such as Accountant must keep seeing every register of their tenant.
    /// </summary>
    public async Task<CashRegisterPermissionResult> CanViewAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        var (_, denial) = await ResolveReachableRegisterAsync(registerId, principal, "view", cancellationToken)
            .ConfigureAwait(false);
        if (denial != null)
            return denial;

        if (IsElevatedActor(principal)
            || PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.CashRegisterView))
        {
            return CashRegisterPermissionResult.Allow();
        }

        return Deny("view", registerId, principal, CashRegisterPermissionCodes.OperationPermissionRequired);
    }

    /// <summary>
    /// RKSV Sonderbeleg creation. Which receipt kinds an actor may create is already decided by the route's
    /// <c>rksv.*.create</c> policy; this check only adds tenant reachability and the operational assignment rule so a
    /// cashier cannot sign a Beleg onto a colleague's register.
    /// </summary>
    public Task<CashRegisterPermissionResult> CanCreateSonderbelegAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default) =>
        EvaluateOperationalAsync(registerId, principal, "sonderbeleg", AppPermissions.CashRegisterView, cancellationToken);

    /// <summary>
    /// Shared rule for register-bound operations: Super Admin anywhere, <see cref="AppPermissions.CashRegisterManage"/>
    /// holders anywhere in their own tenant, and operational actors only on registers that are visible to them
    /// (unassigned, i.e. shared, or assigned to themselves).
    /// Close additionally requires the actor to hold the open shift (<see cref="CashRegister.CurrentUserId"/>),
    /// including for elevated actors — recovery close lives on <c>TryForceCloseCashRegisterAsync</c>, not this path.
    /// A mid-shift reassignment still lets the current shift holder close so the till cannot get stuck Open.
    /// </summary>
    private async Task<CashRegisterPermissionResult> EvaluateOperationalAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        string operation,
        string operationalPermission,
        CancellationToken cancellationToken,
        bool allowCurrentShiftHolder = false)
    {
        var (register, denial) = await ResolveReachableRegisterAsync(registerId, principal, operation, cancellationToken)
            .ConfigureAwait(false);
        if (denial != null)
            return denial;

        var elevated = IsElevatedActor(principal);
        if (elevated && !allowCurrentShiftHolder)
            return CashRegisterPermissionResult.Allow();

        if (!elevated && !PermissionClaimHelper.PrincipalHasAppPermission(principal, operationalPermission))
            return Deny(operation, registerId, principal, CashRegisterPermissionCodes.OperationPermissionRequired);

        var actorUserId = principal.GetActorUserId();
        if (string.IsNullOrEmpty(actorUserId))
            return Deny(operation, registerId, principal, CashRegisterPermissionCodes.ActorNotAuthenticated);

        if (allowCurrentShiftHolder && register!.Status == RegisterStatus.Open)
        {
            if (!string.Equals(register.CurrentUserId, actorUserId, StringComparison.Ordinal))
                return Deny(operation, registerId, principal, CashRegisterPermissionCodes.RegisterHeldByOtherUser);

            return CashRegisterPermissionResult.Allow();
        }

        if (elevated)
            return CashRegisterPermissionResult.Allow();

        if (!CashRegisterAssignment.IsVisibleTo(actorUserId, register!.AssignedUserId)
            && !(allowCurrentShiftHolder
                 && string.Equals(register.CurrentUserId, actorUserId, StringComparison.Ordinal)))
        {
            return Deny(operation, registerId, principal, CashRegisterPermissionCodes.RegisterNotAssignedToActor);
        }

        return CashRegisterPermissionResult.Allow();
    }

    /// <summary>
    /// Loads the register and confirms the actor's tenant may reach it. A cross-tenant register is reported as
    /// <see cref="CashRegisterPermissionDecision.NotFound"/> so tenants cannot probe each other's inventory.
    /// </summary>
    private async Task<(CashRegister? Register, CashRegisterPermissionResult? Denial)> ResolveReachableRegisterAsync(
        Guid registerId,
        ClaimsPrincipal? principal,
        string operation,
        CancellationToken cancellationToken)
    {
        if (registerId == Guid.Empty)
            return (null, CashRegisterPermissionResult.NotFound());

        // Filters are bypassed so Super Admins reach any mandant and unbound ambient tenants cannot silently hide a
        // row; the tenant comparison below re-applies isolation for everyone else.
        var register = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registerId, cancellationToken)
            .ConfigureAwait(false);

        if (register == null)
            return (null, CashRegisterPermissionResult.NotFound());

        if (PermissionClaimHelper.IsSuperAdminPrincipal(principal))
            return (register, null);

        Guid effectiveTenantId;
        try
        {
            effectiveTenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cash register permission check failed to resolve tenant Operation={Operation} RegisterId={RegisterId}",
                operation,
                registerId);
            return (null, CashRegisterPermissionResult.NotFound());
        }

        if (effectiveTenantId != register.TenantId)
        {
            _logger.LogWarning(
                "Cash register permission denied across tenants Operation={Operation} RegisterId={RegisterId} RegisterTenantId={RegisterTenantId} ActorTenantId={ActorTenantId} Actor={Actor}",
                operation,
                registerId,
                register.TenantId,
                effectiveTenantId,
                principal.GetActorUserId());
            return (null, CashRegisterPermissionResult.NotFound());
        }

        return (register, null);
    }

    private static bool IsElevatedActor(ClaimsPrincipal? principal) =>
        PermissionClaimHelper.IsSuperAdminPrincipal(principal)
        || PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.CashRegisterManage);

    private CashRegisterPermissionResult Deny(
        string operation,
        Guid registerId,
        ClaimsPrincipal? principal,
        string code)
    {
        _logger.LogWarning(
            "Cash register permission denied Operation={Operation} RegisterId={RegisterId} Actor={Actor} Code={Code}",
            operation,
            registerId,
            principal.GetActorUserId(),
            code);
        return CashRegisterPermissionResult.Forbidden(code);
    }
}

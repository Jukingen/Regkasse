using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Per-register authorization for cash register operations. Sits below the <c>[HasPermission]</c> policy gate and
/// decides tenant reachability plus the operational assignment rule. Cross-tenant registers must be indistinguishable
/// from missing ones (HTTP 404), never 403.
/// </summary>
public sealed class CashRegisterPermissionServiceTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>Tenant A register with no assignment — shared across the tenant's POS users.</summary>
    private static readonly Guid SharedRegisterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");

    /// <summary>Tenant A register assigned to <see cref="CashierA"/>.</summary>
    private static readonly Guid AssignedRegisterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");

    /// <summary>Tenant B register — unreachable for tenant A actors.</summary>
    private static readonly Guid ForeignRegisterId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001");

    private const string CashierA = "cashier-a";
    private const string CashierA2 = "cashier-a2";
    private const string CashierB = "cashier-b";

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegisterPermissions_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<AppDbContext> SeededDbAsync()
    {
        var db = CreateDb();
        var now = DateTime.UtcNow;

        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Tenant A", Slug = "tenant-a", CreatedAt = now },
            new Tenant { Id = TenantBId, Name = "Tenant B", Slug = "tenant-b", CreatedAt = now });

        db.CashRegisters.AddRange(
            Register(SharedRegisterId, TenantAId, "A-1", assignedUserId: null),
            Register(AssignedRegisterId, TenantAId, "A-2", assignedUserId: CashierA),
            Register(ForeignRegisterId, TenantBId, "B-1", assignedUserId: null));

        db.UserTenantMemberships.AddRange(
            new UserTenantMembership { UserId = CashierA, TenantId = TenantAId, IsActive = true },
            new UserTenantMembership { UserId = CashierA2, TenantId = TenantAId, IsActive = true },
            new UserTenantMembership { UserId = CashierB, TenantId = TenantBId, IsActive = true });

        await db.SaveChangesAsync();
        return db;
    }

    private static CashRegister Register(Guid id, Guid tenantId, string number, string? assignedUserId) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            RegisterNumber = number,
            Location = number,
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            AssignedUserId = assignedUserId,
        };

    /// <summary>Service bound to tenant A, i.e. the tenant every non–Super Admin actor in these tests belongs to.</summary>
    private static CashRegisterPermissionService CreateService(AppDbContext db) =>
        new(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            NullLogger<CashRegisterPermissionService>.Instance);

    /// <summary>Principal whose permissions come from the role matrix, like a normal JWT without permission claims.</summary>
    private static ClaimsPrincipal Actor(string userId, string role) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)],
            "Test"));

    /// <summary>Principal carrying an explicit permission set, like a compact JWT.</summary>
    private static ClaimsPrincipal ActorWithPermissions(string userId, params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(permissions.Select(p => new Claim(PermissionCatalog.PermissionClaimType, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // ---- Super Admin: reaches every mandant -------------------------------------------------

    [Fact]
    public async Task SuperAdmin_MayOpenRegisterOfAnotherTenant()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanOpenAsync(ForeignRegisterId, Actor("root", Roles.SuperAdmin));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task SuperAdmin_MayAssignAcrossTenants_WhenTargetBelongsToRegisterTenant()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanAssignUserAsync(ForeignRegisterId, CashierB, Actor("root", Roles.SuperAdmin));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    /// <summary>Reaching the register does not make an outsider a valid assignee.</summary>
    [Fact]
    public async Task SuperAdmin_AssigningUserFromAnotherTenant_IsInvalidTarget()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanAssignUserAsync(ForeignRegisterId, CashierA, Actor("root", Roles.SuperAdmin));

        Assert.Equal(CashRegisterPermissionDecision.InvalidTarget, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.AssigneeNotInTenant, result.Code);
    }

    // ---- Manager: own tenant only -----------------------------------------------------------

    [Fact]
    public async Task Manager_MayAssignAndOpenWithinOwnTenant()
    {
        await using var db = await SeededDbAsync();
        var service = CreateService(db);
        var manager = Actor("manager-a", Roles.Manager);

        Assert.True((await service.CanAssignUserAsync(SharedRegisterId, CashierA, manager)).IsAllowed);
        Assert.True((await service.CanOpenAsync(SharedRegisterId, manager)).IsAllowed);
        Assert.True((await service.CanCloseAsync(SharedRegisterId, manager)).IsAllowed);
    }

    /// <summary>A manager is not bound by the cashier assignment inside their own tenant.</summary>
    [Fact]
    public async Task Manager_MayOpenRegisterAssignedToSomebodyElse()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanOpenAsync(AssignedRegisterId, Actor("manager-a", Roles.Manager));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task Manager_CrossTenantRegister_IsReportedAsNotFound()
    {
        await using var db = await SeededDbAsync();
        var service = CreateService(db);
        var manager = Actor("manager-a", Roles.Manager);

        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanOpenAsync(ForeignRegisterId, manager)).Decision);
        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanCloseAsync(ForeignRegisterId, manager)).Decision);
        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanViewAsync(ForeignRegisterId, manager)).Decision);
        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanAssignUserAsync(ForeignRegisterId, CashierB, manager)).Decision);
        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanCreateSonderbelegAsync(ForeignRegisterId, manager)).Decision);
    }

    [Fact]
    public async Task Manager_AssigningUserFromAnotherTenant_IsInvalidTarget()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanAssignUserAsync(SharedRegisterId, CashierB, Actor("manager-a", Roles.Manager));

        Assert.Equal(CashRegisterPermissionDecision.InvalidTarget, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.AssigneeNotInTenant, result.Code);
    }

    /// <summary>Clearing an assignment has no target to validate.</summary>
    [Fact]
    public async Task Manager_ClearingAssignment_IsAllowed()
    {
        await using var db = await SeededDbAsync();
        var service = CreateService(db);
        var manager = Actor("manager-a", Roles.Manager);

        Assert.True((await service.CanAssignUserAsync(AssignedRegisterId, null, manager)).IsAllowed);
        Assert.True((await service.CanAssignUserAsync(AssignedRegisterId, "   ", manager)).IsAllowed);
    }

    [Fact]
    public async Task Manager_InactiveMembership_IsInvalidTarget()
    {
        await using var db = await SeededDbAsync();
        var membership = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .FirstAsync(m => m.UserId == CashierA);
        membership.IsActive = false;
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .CanAssignUserAsync(SharedRegisterId, CashierA, Actor("manager-a", Roles.Manager));

        Assert.Equal(CashRegisterPermissionDecision.InvalidTarget, result.Decision);
    }

    // ---- Cashier: assignment scopes which registers are reachable ---------------------------

    [Fact]
    public async Task Cashier_MayOpenUnassignedRegister()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanOpenAsync(SharedRegisterId, Actor(CashierA2, Roles.Cashier));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task Cashier_MayOpenAndCloseRegisterAssignedToThemselves()
    {
        await using var db = await SeededDbAsync();
        var service = CreateService(db);
        var cashier = Actor(CashierA, Roles.Cashier);

        Assert.True((await service.CanOpenAsync(AssignedRegisterId, cashier)).IsAllowed);
        Assert.True((await service.CanCloseAsync(AssignedRegisterId, cashier)).IsAllowed);
    }

    [Fact]
    public async Task Cashier_MayNotOpenRegisterAssignedToColleague()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanOpenAsync(AssignedRegisterId, Actor(CashierA2, Roles.Cashier));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.RegisterNotAssignedToActor, result.Code);
    }

    [Fact]
    public async Task Cashier_MayNotAssignUsers()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanAssignUserAsync(SharedRegisterId, CashierA, Actor(CashierA, Roles.Cashier));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.ManagePermissionRequired, result.Code);
    }

    [Fact]
    public async Task Waiter_MayNotAssignOrOpen()
    {
        await using var db = await SeededDbAsync();
        var service = CreateService(db);
        var waiter = Actor("waiter-a", Roles.Waiter);

        Assert.Equal(
            CashRegisterPermissionDecision.Forbidden,
            (await service.CanAssignUserAsync(SharedRegisterId, CashierA, waiter)).Decision);
        Assert.Equal(
            CashRegisterPermissionDecision.Forbidden,
            (await service.CanOpenAsync(SharedRegisterId, waiter)).Decision);
        Assert.Equal(
            CashRegisterPermissionCodes.OperationPermissionRequired,
            (await service.CanOpenAsync(SharedRegisterId, waiter)).Code);
    }

    /// <summary>
    /// Close must still succeed for the cashier who holds the open shift after an admin reassigns the till,
    /// otherwise the register would be stuck Open.
    /// </summary>
    [Fact]
    public async Task Cashier_HoldingOpenShift_MayCloseAfterReassignment()
    {
        await using var db = await SeededDbAsync();
        var register = await db.CashRegisters.FindAsync(AssignedRegisterId);
        register!.AssignedUserId = CashierA2;
        register.CurrentUserId = CashierA;
        register.Status = RegisterStatus.Open;
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .CanCloseAsync(AssignedRegisterId, Actor(CashierA, Roles.Cashier));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task Manager_MayNotCloseRegisterHeldByCashier()
    {
        await using var db = await SeededDbAsync();
        var register = await db.CashRegisters.FindAsync(AssignedRegisterId);
        register!.Status = RegisterStatus.Open;
        register.CurrentUserId = CashierA;
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .CanCloseAsync(AssignedRegisterId, Actor("manager-a", Roles.Manager));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.RegisterHeldByOtherUser, result.Code);
    }

    [Fact]
    public async Task Manager_MayCloseRegisterTheyHold()
    {
        await using var db = await SeededDbAsync();
        var register = await db.CashRegisters.FindAsync(SharedRegisterId);
        register!.Status = RegisterStatus.Open;
        register.CurrentUserId = "manager-a";
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .CanCloseAsync(SharedRegisterId, Actor("manager-a", Roles.Manager));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task SuperAdmin_MayNotCloseRegisterHeldByCashier()
    {
        await using var db = await SeededDbAsync();
        var register = await db.CashRegisters.FindAsync(AssignedRegisterId);
        register!.Status = RegisterStatus.Open;
        register.CurrentUserId = CashierA;
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .CanCloseAsync(AssignedRegisterId, Actor("root", Roles.SuperAdmin));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.RegisterHeldByOtherUser, result.Code);
    }

    [Fact]
    public async Task Cashier_MayNotCloseColleagueRegister_WhenNotHoldingShift()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanCloseAsync(AssignedRegisterId, Actor(CashierA2, Roles.Cashier));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.RegisterNotAssignedToActor, result.Code);
    }

    [Fact]
    public async Task Cashier_MayNotCreateSonderbelegOnColleaguesRegister()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db)
            .CanCreateSonderbelegAsync(AssignedRegisterId, Actor(CashierA2, Roles.Cashier));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.RegisterNotAssignedToActor, result.Code);
    }

    /// <summary>Missing the operation's own permission is denied before assignment is even considered.</summary>
    [Fact]
    public async Task ActorWithoutShiftClosePermission_MayNotClose()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db).CanCloseAsync(
            SharedRegisterId,
            ActorWithPermissions(CashierA, AppPermissions.CashRegisterView, AppPermissions.ShiftOpen));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.OperationPermissionRequired, result.Code);
    }

    // ---- View: read access is not scoped by assignment ---------------------------------------

    /// <summary>
    /// Assignment scopes the POS picker, not reads. Reporting roles must keep seeing every register of their tenant.
    /// </summary>
    [Fact]
    public async Task ReadOnlyActor_MayViewRegisterAssignedToSomebodyElse()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db).CanViewAsync(
            AssignedRegisterId,
            ActorWithPermissions("accountant-a", AppPermissions.CashRegisterView));

        Assert.Equal(CashRegisterPermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task ActorWithoutViewPermission_MayNotView()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db).CanViewAsync(
            SharedRegisterId,
            ActorWithPermissions("kitchen-a", AppPermissions.KitchenView));

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
    }

    // ---- Missing / unauthenticated ------------------------------------------------------------

    [Fact]
    public async Task UnknownRegister_IsNotFound()
    {
        await using var db = await SeededDbAsync();
        var service = CreateService(db);

        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanOpenAsync(Guid.NewGuid(), Actor("manager-a", Roles.Manager))).Decision);
        Assert.Equal(
            CashRegisterPermissionDecision.NotFound,
            (await service.CanOpenAsync(Guid.Empty, Actor("root", Roles.SuperAdmin))).Decision);
    }

    [Fact]
    public async Task AnonymousPrincipal_IsForbidden()
    {
        await using var db = await SeededDbAsync();

        var result = await CreateService(db).CanOpenAsync(SharedRegisterId, principal: null);

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
    }

    /// <summary>A permission-bearing token without a subject cannot be matched against an assignment.</summary>
    [Fact]
    public async Task PrincipalWithoutUserId_IsForbidden()
    {
        await using var db = await SeededDbAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.ShiftOpen)],
            "Test"));

        var result = await CreateService(db).CanOpenAsync(SharedRegisterId, principal);

        Assert.Equal(CashRegisterPermissionDecision.Forbidden, result.Decision);
        Assert.Equal(CashRegisterPermissionCodes.ActorNotAuthenticated, result.Code);
    }
}

using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// <c>POST /api/admin/cash-registers/{id}/assign</c>: sets or clears the admin-managed cashier assignment that scopes the
/// POS picker. Covers tenant isolation (cross-tenant → 404), target membership validation, and audit.
/// </summary>
public sealed class AdminCashRegisterAssignTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid RegisterAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
    private static readonly Guid RegisterBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001");

    private const string CashierInTenantA = "cashier-a";
    private const string CashierInTenantB = "cashier-b";

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminCashRegAssign_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static CashRegisterManagementService CreateService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService? auditLog = null,
        ITenantLimitService? tenantLimits = null) =>
        new(
            db,
            tenantResolver,
            auditLog ?? Mock.Of<IAuditLogService>(),
            CashRegisterTestDoubles.NoOpListEnrichment(),
            new PaymentMethodDefinitionBootstrapService(db),
            TseProvisioningTestDoubles.Successful(),
            Mock.Of<KasseAPI_Final.Services.Trial.ITrialLimitGuard>(),
            tenantLimits ?? CashRegisterTestDoubles.PermissiveTenantLimits(),
            NullLogger<CashRegisterManagementService>.Instance);

    /// <summary>Wires the real permission service so the route's gate is exercised alongside the management service.</summary>
    private static AdminCashRegistersController CreateController(
        AppDbContext db,
        ICashRegisterManagementService management,
        string actorRole)
    {
        var controller = new AdminCashRegistersController(
            Mock.Of<ICashRegisterDecommissionService>(),
            management,
            CashRegisterTestDoubles.NoOpListEnrichment(),
            Mock.Of<ICashRegisterShiftService>(),
            new CashRegisterPermissionService(
                db,
                TenantTestDoubles.SettingsResolverReturning(TenantAId),
                NullLogger<CashRegisterPermissionService>.Instance),
            TenantTestDoubles.TenantAccessorReturning(TenantAId),
            NullLogger<AdminCashRegistersController>.Instance,
            LocalizationTestDoubles.ApiMessageLocalizer());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "actor-1"),
                        new Claim(ClaimTypes.Role, actorRole),
                    },
                    "Test")),
            },
        };

        return controller;
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Tenant A", Slug = "tenant-a", CreatedAt = now },
            new Tenant { Id = TenantBId, Name = "Tenant B", Slug = "tenant-b", CreatedAt = now });
        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = RegisterAId,
                TenantId = TenantAId,
                RegisterNumber = "A-1",
                Location = "A",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
            },
            new CashRegister
            {
                Id = RegisterBId,
                TenantId = TenantBId,
                RegisterNumber = "B-1",
                Location = "B",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
            });
        db.UserTenantMemberships.AddRange(
            new UserTenantMembership { UserId = CashierInTenantA, TenantId = TenantAId, IsActive = true },
            new UserTenantMembership { UserId = CashierInTenantB, TenantId = TenantBId, IsActive = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Assign_PersistsAssignedUserId_AndWritesAudit()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var audit = new Mock<IAuditLogService>();
        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId), audit.Object);

        var dto = await service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA },
            "actor-1",
            Roles.Manager,
            actorIsSuperAdmin: false);

        Assert.Equal(CashierInTenantA, dto.AssignedUserId);
        Assert.Equal(CashierInTenantA, (await db.CashRegisters.FindAsync(RegisterAId))!.AssignedUserId);
        VerifyAssignmentAudit(audit, AuditLogActions.CASH_REGISTER_ASSIGNED);
    }

    [Fact]
    public async Task Assign_WithNullUserId_ClearsAssignment_AndAuditsUnassigned()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        (await db.CashRegisters.FindAsync(RegisterAId))!.AssignedUserId = CashierInTenantA;
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId), audit.Object);

        var dto = await service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = null },
            "actor-1",
            Roles.Manager,
            actorIsSuperAdmin: false);

        Assert.Null(dto.AssignedUserId);
        Assert.Null((await db.CashRegisters.FindAsync(RegisterAId))!.AssignedUserId);
        VerifyAssignmentAudit(audit, AuditLogActions.CASH_REGISTER_UNASSIGNED);
    }

    private static void VerifyAssignmentAudit(Mock<IAuditLogService> audit, string expectedAction) =>
        audit.Verify(
            a => a.LogEntityChangeAsync(
                expectedAction,
                AuditLogEntityTypes.CASH_REGISTER,
                RegisterAId,
                "actor-1",
                Roles.Manager,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>()),
            Times.Once);

    [Fact]
    public async Task Assign_UserFromAnotherTenant_IsRejected()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantB },
            "actor-1",
            Roles.Manager,
            actorIsSuperAdmin: false));

        Assert.Contains("not an active member", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await db.CashRegisters.FindAsync(RegisterAId))!.AssignedUserId);
    }

    [Fact]
    public async Task Assign_InactiveMembership_IsRejected()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var membership = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .FirstAsync(m => m.UserId == CashierInTenantA);
        membership.IsActive = false;
        await db.SaveChangesAsync();

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA },
            "actor-1",
            Roles.Manager,
            actorIsSuperAdmin: false));
    }

    /// <summary>Cross-tenant target must look absent, not forbidden (multi-tenant 404 semantics).</summary>
    [Fact]
    public async Task Assign_CrossTenantRegister_ReturnsNotFound()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));
        var controller = CreateController(db, service, Roles.Manager);

        var result = await controller.AssignUser(
            RegisterBId,
            new AssignCashRegisterUserRequest { UserId = null },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Assign_SuperAdmin_MayTargetAnotherTenantsRegister()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));
        var controller = CreateController(db, service, Roles.SuperAdmin);

        var result = await controller.AssignUser(
            RegisterBId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantB },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CashRegisterDto>(ok.Value);
        Assert.Equal(CashierInTenantB, dto.AssignedUserId);
    }

    [Fact]
    public async Task Assign_Cashier_ReturnsForbidden()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));
        var controller = CreateController(db, service, Roles.Cashier);

        var result = await controller.AssignUser(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA },
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Assign_UnknownRegister_ReturnsNotFound()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));
        var controller = CreateController(db, service, Roles.Manager);

        var result = await controller.AssignUser(
            Guid.NewGuid(),
            new AssignCashRegisterUserRequest { UserId = null },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Assign_DecommissionedRegister_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        (await db.CashRegisters.FindAsync(RegisterAId))!.Status = RegisterStatus.Decommissioned;
        await db.SaveChangesAsync();

        var service = CreateService(db, TenantTestDoubles.SettingsResolverReturning(TenantAId));
        var controller = CreateController(db, service, Roles.Manager);

        var result = await controller.AssignUser(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Assign_WhenMaxActiveRegistersPerUserReached_ThrowsLimitExceeded()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var extraRegisterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
        db.CashRegisters.Add(new CashRegister
        {
            Id = extraRegisterId,
            TenantId = TenantAId,
            RegisterNumber = "A-2",
            Location = "A2",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            AssignedUserId = CashierInTenantA,
        });
        await db.SaveChangesAsync();

        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.GetLimitValueAsync(TenantAId, TenantLimitKeys.MaxActiveRegistersPerUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            tenantLimits: limits.Object);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(() => service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA },
            "actor-1",
            Roles.Manager,
            actorIsSuperAdmin: false));

        Assert.Equal(TenantLimitKeys.MaxActiveRegistersPerUser, ex.LimitKey);
        Assert.Null((await db.CashRegisters.FindAsync(RegisterAId))!.AssignedUserId);
    }

    [Fact]
    public async Task Assign_SuperAdminForce_BypassesMaxActiveRegistersPerUser()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var extraRegisterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
        db.CashRegisters.Add(new CashRegister
        {
            Id = extraRegisterId,
            TenantId = TenantAId,
            RegisterNumber = "A-2",
            Location = "A2",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            AssignedUserId = CashierInTenantA,
        });
        await db.SaveChangesAsync();

        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.GetLimitValueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            tenantLimits: limits.Object);

        var dto = await service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA, Force = true },
            "actor-1",
            Roles.SuperAdmin,
            actorIsSuperAdmin: true);

        Assert.Equal(CashierInTenantA, dto.AssignedUserId);
    }

    [Fact]
    public async Task Assign_ManagerForce_DoesNotBypassLimit()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var extraRegisterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
        db.CashRegisters.Add(new CashRegister
        {
            Id = extraRegisterId,
            TenantId = TenantAId,
            RegisterNumber = "A-2",
            Location = "A2",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            AssignedUserId = CashierInTenantA,
        });
        await db.SaveChangesAsync();

        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.GetLimitValueAsync(TenantAId, TenantLimitKeys.MaxActiveRegistersPerUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            tenantLimits: limits.Object);

        await Assert.ThrowsAsync<LimitExceededException>(() => service.AssignUserAsync(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA, Force = true },
            "actor-1",
            Roles.Manager,
            actorIsSuperAdmin: false));
    }

    [Fact]
    public async Task Assign_WhenLimitExceeded_ControllerReturns409()
    {
        await using var db = CreateDb();
        await SeedAsync(db);

        var extraRegisterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");
        db.CashRegisters.Add(new CashRegister
        {
            Id = extraRegisterId,
            TenantId = TenantAId,
            RegisterNumber = "A-2",
            Location = "A2",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            AssignedUserId = CashierInTenantA,
        });
        await db.SaveChangesAsync();

        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.GetLimitValueAsync(TenantAId, TenantLimitKeys.MaxActiveRegistersPerUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            tenantLimits: limits.Object);
        var controller = CreateController(db, service, Roles.Manager);

        var result = await controller.AssignUser(
            RegisterAId,
            new AssignCashRegisterUserRequest { UserId = CashierInTenantA },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }
}

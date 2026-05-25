using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for AdminUsersController: policy, validation, concurrency, audit, and safe DTOs.
/// </summary>
public class AdminUsersControllerTests
{
    private static AppDbContext CreateEphemeralContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminUsers_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static AdminUsersController CreateController(
        AppDbContext context,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUniquenessValidationService? uniquenessValidation = null,
        string? actorId = "admin-id",
        string actorRole = Roles.SuperAdmin)
    {
        var userManager = CreateUserManager(context);
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(context),
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);
        return CreateController(
            userManager,
            roleManager,
            auditLogService,
            sessionInvalidation,
            uniquenessValidation,
            actorId,
            actorRole,
            context);
    }

    private static (UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole> RoleManager) CreateMockUserAndRoleManagers(ApplicationUser? existingUser = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        store.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var options = Options.Create(new IdentityOptions());
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        var userManager = new UserManager<ApplicationUser>(
            store.Object, options, hasher.Object, userValidators, passwordValidators,
            keyNormalizer.Object, errors, services, logger);

        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        var roleManager = new RoleManager<IdentityRole>(
            roleStore.Object, null!, keyNormalizer.Object, errors, null!);

        return (userManager, roleManager);
    }

    private static IUserUniquenessValidationService CreateUniquenessValidationMock()
    {
        var m = new Mock<IUserUniquenessValidationService>();
        m.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        m.Setup(x => x.IsEmployeeNumberTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        m.Setup(x => x.IsTaxNumberTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        return m.Object;
    }

    private static AdminUsersController CreateController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUniquenessValidationService? uniquenessValidation = null,
        string? actorId = "admin-id",
        string actorRole = "SuperAdmin",
        AppDbContext? context = null)
    {
        var logger = new Mock<ILogger<AdminUsersController>>().Object;
        var tenantUserService = new Mock<ITenantUserService>().Object;
        var controller = new AdminUsersController(
            context ?? CreateEphemeralContext(),
            userManager,
            roleManager,
            auditLogService,
            sessionInvalidation,
            uniquenessValidation ?? CreateUniquenessValidationMock(),
            logger,
            TenantTestDoubles.NoOpProvisioner(),
            tenantUserService,
            NullCurrentTenantAccessor.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actorId ?? ""),
            new(ClaimTypes.Role, actorRole),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        return controller;
    }

    [Fact]
    public void ApiError_Validation_SetsStatus400AndType()
    {
        var err = ApiError.Validation("Validation failed", new Dictionary<string, string[]> { ["email"] = new[] { "Invalid." } });
        Assert.Equal(400, err.Status);
        Assert.Equal("ValidationError", err.Type);
        Assert.NotNull(err.Errors);
        Assert.Single(err.Errors["email"], "Invalid.");
    }

    [Fact]
    public void ApiError_NotFound_SetsStatus404()
    {
        var err = ApiError.NotFound("User not found");
        Assert.Equal(404, err.Status);
        Assert.Equal("NotFound", err.Type);
    }

    [Fact]
    public void ApiError_ConcurrencyConflict_SetsStatus412()
    {
        var err = ApiError.ConcurrencyConflict();
        Assert.Equal(412, err.Status);
        Assert.Equal("Conflict", err.Type);
    }

    [Fact]
    public async Task GetById_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.GetById("nonexistent-id");

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(notFound.Value);
        Assert.Equal(404, body.Status);
    }

    [Fact]
    public async Task GetById_WhenUserExists_ReturnsSafeDto_NoSecrets()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "jane",
            Email = "jane@test.com",
            FirstName = "Jane",
            LastName = "Doe",
            Role = "Cashier",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = "etag-1",
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(db, audit, session);

        var result = await controller.GetById("user-1");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminUsersController.AdminUserDto>(ok.Value);
        Assert.Equal("user-1", dto.Id);
        Assert.Equal("jane", dto.UserName);
        Assert.Equal("Jane", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);
        Assert.Equal("etag-1", dto.Etag);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task Deactivate_WhenReasonEmpty_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.Deactivate("u1", new AdminUsersController.AdminDeactivateRequest { Reason = "" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal(400, body.Status);
        Assert.NotNull(body.Errors);
        Assert.True(body.Errors.ContainsKey("reason"));
    }

    [Fact]
    public async Task Deactivate_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.Deactivate("nonexistent", new AdminUsersController.AdminDeactivateRequest { Reason = "Left company" });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Deactivate_WhenActorIsTarget_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session, actorId: "u1", actorRole: "SuperAdmin");

        var result = await controller.Deactivate("u1", new AdminUsersController.AdminDeactivateRequest { Reason = "Leaving" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal(400, body.Status);
        Assert.Equal("BusinessRule", body.Type);
        Assert.Contains("cannot deactivate your own", body.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_WhenUserAlreadyDeactivated_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = false };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.Deactivate("u1", new AdminUsersController.AdminDeactivateRequest { Reason = "Again" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal(400, body.Status);
        Assert.Equal("BusinessRule", body.Type);
    }

    [Fact]
    public async Task Patch_WhenIfMatchMismatch_Returns412()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "u",
            FirstName = "A",
            LastName = "B",
            ConcurrencyStamp = "old-etag",
            IsActive = true,
        };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var request = new AdminUsersController.AdminPatchUserRequest { FirstName = "Updated" };
        var result = await controller.Patch("u1", request, "wrong-etag");

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(412, status.StatusCode);
        var body = Assert.IsType<ApiError>(status.Value);
        Assert.Equal("Conflict", body.Type);
    }

    [Fact]
    public async Task Reactivate_WhenUserAlreadyActive_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.Reactivate("u1", null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal(400, body.Status);
        Assert.Equal("BusinessRule", body.Type);
    }

    [Fact]
    public async Task Reactivate_WhenUserWasLockedOut_ClearsLockoutEnd()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "u",
            FirstName = "A",
            LastName = "B",
            IsActive = false,
            LockoutEnd = DateTimeOffset.UtcNow.AddHours(2),
            UserTenantMemberships = new List<UserTenantMembership>(),
        };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.Reactivate("u1", null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AdminUsersController.AdminUserDto>(ok.Value);
        Assert.True(dto.IsActive);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task ForcePasswordReset_WhenPasswordTooShort_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.ForcePasswordReset("u1", new AdminUsersController.AdminForcePasswordResetRequest { NewPassword = "1234567" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal(400, body.Status);
        Assert.NotNull(body.Errors);
        Assert.True(body.Errors.ContainsKey("newPassword"));
    }

    [Fact]
    public async Task GenerateTemporaryPassword_WhenActorIsNotSuperAdmin_ReturnsForbidden()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "operator",
            Email = "operator@test.com",
            FirstName = "Op",
            LastName = "Erator",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            actorId: "manager-1",
            actorRole: Roles.Manager);

        var result = await controller.GenerateTemporaryPassword("user-1");

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var body = Assert.IsType<ApiError>(forbidden.Value);
        Assert.Equal("Forbidden", body.Type);
    }

    [Fact]
    public async Task GenerateTemporaryPassword_ReturnsGeneratedPassword_UpdatesFlag_AndInvalidatesSessions()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "operator",
            Email = "operator@test.com",
            FirstName = "Op",
            LastName = "Erator",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            MustChangePasswordOnNextLogin = false,
        };

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
        userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        userManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", It.IsAny<string>()))
            .Callback<ApplicationUser, string, string>((target, _token, password) =>
            {
                target.PasswordHash = $"hashed:{password}";
            })
            .ReturnsAsync(IdentityResult.Success);

        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        var roleManager = new RoleManager<IdentityRole>(
            roleStore.Object,
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogUserLifecycleAsync(
                AuditEventType.PasswordResetForced,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "user-1",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.FORCE_RESET_PASSWORD });
        audit.Setup(x => x.LogUserLifecycleAsync(
                AuditLogActions.SUPER_ADMIN_VIEWED_PASSWORD,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "user-1",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.SUPER_ADMIN_VIEWED_PASSWORD });
        var session = new Mock<IUserSessionInvalidation>();
        session.Setup(x => x.InvalidateSessionsForUserAsync("user-1")).Returns(Task.CompletedTask);

        var controller = CreateController(userManager.Object, roleManager, audit.Object, session.Object);

        var result = await controller.GenerateTemporaryPassword("user-1");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AdminUsersController.AdminTemporaryPasswordResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(body.GeneratedPassword));
        Assert.True(body.ForcePasswordChangeOnNextLogin);
        Assert.Equal(12, body.GeneratedPassword.Length);
        Assert.Matches(@"[A-Z]", body.GeneratedPassword);
        Assert.Matches(@"[a-z]", body.GeneratedPassword);
        Assert.Matches(@"\d", body.GeneratedPassword);
        Assert.Matches(@"[!@#$%^&*()]", body.GeneratedPassword);

        Assert.True(user.MustChangePasswordOnNextLogin);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));

        audit.Verify(
            x => x.LogUserLifecycleAsync(
                AuditEventType.PasswordResetForced,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "user-1",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()),
            Times.Once);
        audit.Verify(
            x => x.LogUserLifecycleAsync(
                AuditLogActions.SUPER_ADMIN_VIEWED_PASSWORD,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "user-1",
                "Support / Password reset",
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()),
            Times.Once);
        session.Verify(x => x.InvalidateSessionsForUserAsync("user-1"), Times.Once);
    }

    [Fact]
    public async Task List_IncludesTenantInfo_ForPlatformAndTenantUsers()
    {
        await using var db = CreateEphemeralContext();

        var cafeTenantId = Guid.NewGuid();
        var barTenantId = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = cafeTenantId,
                Name = "Cafe Alpha",
                Slug = "cafe-alpha",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = barTenantId,
                Name = "Bar Beta",
                Slug = "bar-beta",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "super-1",
                UserName = "super",
                Email = "super@test.com",
                FirstName = "Super",
                LastName = "Admin",
                Role = Roles.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new ApplicationUser
            {
                Id = "cashier-1",
                UserName = "cashier",
                Email = "cashier@test.com",
                FirstName = "Cafe",
                LastName = "Staff",
                Role = Roles.Cashier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new ApplicationUser
            {
                Id = "platform-1",
                UserName = "platform",
                Email = "platform@test.com",
                FirstName = "Platform",
                LastName = "Only",
                Role = Roles.Manager,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        db.UserTenantMemberships.AddRange(
            new UserTenantMembership
            {
                Id = Guid.NewGuid(),
                UserId = "cashier-1",
                TenantId = barTenantId,
                IsActive = true,
                IsOwner = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            },
            new UserTenantMembership
            {
                Id = Guid.NewGuid(),
                UserId = "cashier-1",
                TenantId = cafeTenantId,
                IsActive = true,
                IsOwner = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            });

        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(db, audit, session);

        var result = await controller.List();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<AdminUsersController.AdminUserDto>>(ok.Value)
            .ToDictionary(d => d.Id);

        Assert.Equal("Platform", dtos["super-1"].UserType);
        Assert.Null(dtos["super-1"].TenantName);
        Assert.Null(dtos["super-1"].TenantId);

        Assert.Equal("Tenant", dtos["cashier-1"].UserType);
        Assert.Equal(cafeTenantId.ToString(), dtos["cashier-1"].TenantId);
        Assert.Equal("Cafe Alpha", dtos["cashier-1"].TenantName);
        Assert.Equal("cafe-alpha", dtos["cashier-1"].TenantSlug);

        Assert.Equal("Platform", dtos["platform-1"].UserType);
        Assert.Null(dtos["platform-1"].TenantName);
    }

    [Fact]
    public async Task PutUserTenants_UpdatesActiveMemberships_AndReturnsNoContent()
    {
        await using var db = CreateEphemeralContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantA,
                Name = "Cafe A",
                Slug = "cafe-a",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = tenantB,
                Name = "Bar B",
                Slug = "bar-b",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        db.Users.Add(new ApplicationUser
        {
            Id = "cashier-1",
            UserName = "cashier",
            Email = "c@test.com",
            FirstName = "C",
            LastName = "User",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "cashier-1",
            TenantId = tenantA,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogUserLifecycleAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.USER_TENANT_MEMBERSHIP_CHANGED });

        var controller = CreateController(db, audit.Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.PutUserTenants(
            "cashier-1",
            new AdminUsersController.UpdateUserTenantsRequest { TenantIds = new List<Guid> { tenantB } });

        Assert.IsType<NoContentResult>(result);
        var memberships = await db.UserTenantMemberships.Where(m => m.UserId == "cashier-1").ToListAsync();
        Assert.False(memberships.Single(m => m.TenantId == tenantA).IsActive);
        Assert.True(memberships.Single(m => m.TenantId == tenantB).IsActive);
        audit.Verify(
            x => x.LogUserLifecycleAsync(
                AuditEventType.UserTenantMembershipChanged,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "cashier-1",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserTenants_ReturnsActiveBusinessMemberships()
    {
        await using var db = CreateEphemeralContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-x",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u",
            Email = "u@test.com",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Manager,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.GetUserTenants("u1");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IEnumerable<AdminUsersController.AdminUserTenantMembershipDto>>(ok.Value).ToList();
        Assert.Single(rows);
        Assert.Equal(tenantId, rows[0].TenantId);
        Assert.Equal("Cafe", rows[0].TenantName);
        Assert.True(rows[0].IsOwner);
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        foreach (var role in Roles.Canonical)
        {
            db.Roles.Add(new IdentityRole
            {
                Id = Guid.NewGuid().ToString("D"),
                Name = role,
                NormalizedName = role.ToUpperInvariant(),
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_WithEmailOnly_ReturnsGeneratedPassword_And_DoesNotRequireClientPassword()
    {
        await using var db = CreateEphemeralContext();
        await SeedRolesAsync(db);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogUserLifecycleAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        var controller = CreateController(
            db,
            audit.Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.Create(new AdminCreateUserRequest
        {
            Email = "new.platform@test.com",
            FirstName = "Pat",
            LastName = "Admin",
            Role = Roles.SuperAdmin,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<AdminCreateUserResponseDto>(created.Value);
        Assert.Equal("new.platform@test.com", body.Email);
        Assert.False(string.IsNullOrEmpty(body.GeneratedPassword));
        Assert.Equal(12, body.GeneratedPassword.Length);
        Assert.Matches(@"[A-Z]", body.GeneratedPassword);
        Assert.Matches(@"[a-z]", body.GeneratedPassword);
        Assert.Matches(@"\d", body.GeneratedPassword);
        Assert.Matches(@"[!@#$%^&*()]", body.GeneratedPassword);

        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "new.platform@test.com");
        Assert.True(persisted.MustChangePasswordOnNextLogin);
        Assert.True(PasswordGenerator.GenerateSecurePassword().Length >= 12);
    }

    [Fact]
    public async Task GetActivity_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.GetActivity("nonexistent");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}

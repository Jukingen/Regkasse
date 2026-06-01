using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Email;
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
        string actorRole = Roles.SuperAdmin,
        IUsernameChangeEmailService? usernameChangeEmail = null,
        IUserUsernameHistoryService? usernameHistory = null,
        ICurrentTenantAccessor? tenantAccessor = null,
        ITenantUserService? tenantUserService = null)
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
            context,
            usernameChangeEmail,
            usernameHistory,
            tenantAccessor,
            tenantUserService);
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
        m.Setup(x => x.IsUserNameTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        return m.Object;
    }

    private static IUserCreationService CreateUserCreationService(AppDbContext db, UserManager<ApplicationUser> userManager) =>
        new UserCreationService(db, userManager, CreateUniquenessValidationMock());

    private static IUserUsernameHistoryService CreateUsernameHistoryMock(Mock<IUserUsernameHistoryService>? mock = null)
    {
        mock ??= new Mock<IUserUsernameHistoryService>();
        mock.Setup(x => x.RecordChangeAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.ListForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserUsernameHistoryDto>());
        return mock.Object;
    }

    private static IUsernameChangeEmailService CreateUsernameChangeEmailMock(Mock<IUsernameChangeEmailService>? mock = null)
    {
        mock ??= new Mock<IUsernameChangeEmailService>();
        mock.Setup(x => x.IsConfigured).Returns(true);
        mock.Setup(x => x.TrySendUsernameChangedAsync(It.IsAny<UsernameChangedEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock.Object;
    }

    private static AdminUsersController CreateController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUniquenessValidationService? uniquenessValidation = null,
        string? actorId = "admin-id",
        string actorRole = "SuperAdmin",
        AppDbContext? context = null,
        IUsernameChangeEmailService? usernameChangeEmail = null,
        IUserUsernameHistoryService? usernameHistory = null,
        ICurrentTenantAccessor? tenantAccessor = null,
        ITenantUserService? tenantUserService = null)
    {
        var logger = new Mock<ILogger<AdminUsersController>>().Object;
        tenantUserService ??= new Mock<ITenantUserService>().Object;
        var db = context ?? CreateEphemeralContext();
        var controller = new AdminUsersController(
            db,
            userManager,
            roleManager,
            auditLogService,
            sessionInvalidation,
            uniquenessValidation ?? CreateUniquenessValidationMock(),
            CreateUserCreationService(db, userManager),
            logger,
            TenantTestDoubles.NoOpProvisioner(),
            tenantUserService,
            tenantAccessor ?? NullCurrentTenantAccessor.Instance,
            usernameChangeEmail ?? CreateUsernameChangeEmailMock(),
            usernameHistory ?? CreateUsernameHistoryMock(),
            ActivityEventTestSupport.CreateRecorder());
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
        var dto = Assert.IsType<AdminUserDto>(ok.Value);
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
        var dto = Assert.IsType<AdminUserDto>(ok.Value);
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
        await using var db = CreateEphemeralContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-audit",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

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
                tenantId,
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
                tenantId,
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

        var controller = CreateController(userManager.Object, roleManager, audit.Object, session.Object, context: db);

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
                tenantId,
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
                tenantId,
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
                Id = LegacyDefaultTenantIds.Primary,
                Name = "Default",
                Slug = LegacyDefaultTenantIds.PrimarySlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
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
            },
            new UserTenantMembership
            {
                Id = Guid.NewGuid(),
                UserId = "platform-1",
                TenantId = LegacyDefaultTenantIds.Primary,
                IsActive = true,
                IsOwner = false,
                CreatedAtUtc = DateTime.UtcNow,
            });

        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(db, audit, session);

        var result = await controller.List();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(ok.Value)
            .ToDictionary(d => d.Id);

        Assert.Equal("Platform", dtos["super-1"].UserType);
        Assert.Null(dtos["super-1"].TenantName);
        Assert.Null(dtos["super-1"].TenantId);

        Assert.Equal("Tenant", dtos["cashier-1"].UserType);
        Assert.Equal(cafeTenantId.ToString(), dtos["cashier-1"].TenantId);
        Assert.Equal("Cafe Alpha", dtos["cashier-1"].TenantName);
        Assert.Equal("cafe-alpha", dtos["cashier-1"].TenantSlug);

        Assert.Equal("Tenant", dtos["platform-1"].UserType);
        Assert.Equal(LegacyDefaultTenantIds.Primary.ToString(), dtos["platform-1"].TenantId);
        Assert.Equal("default", dtos["platform-1"].TenantSlug);

        Assert.Equal("cashier", dtos["cashier-1"].UserName);
        Assert.Equal("platform", dtos["platform-1"].UserName);
    }

    [Fact]
    public async Task List_ExcludesOrphanedTenantUsers_WhenActiveFilterTrue()
    {
        await using var db = CreateEphemeralContext();
        var deletedTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = deletedTenantId,
            Name = "Bar Test",
            Slug = "bar",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "bar-admin",
            UserName = "admin@bar.regkasse.at",
            Email = "admin@bar.regkasse.at",
            FirstName = "Admin",
            LastName = "Test Bar",
            Role = Roles.Manager,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "bar-admin",
            TenantId = deletedTenantId,
            IsActive = false,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var activeResult = await controller.List(isActive: true);
        var activeDtos = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(
            Assert.IsType<OkObjectResult>(activeResult).Value).ToList();
        Assert.DoesNotContain(activeDtos, d => d.Id == "bar-admin");

        var inactiveResult = await controller.List(isActive: false);
        var inactiveDtos = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(
            Assert.IsType<OkObjectResult>(inactiveResult).Value).ToList();
        Assert.Contains(inactiveDtos, d => d.Id == "bar-admin");
    }

    [Fact]
    public async Task List_TypePlatform_ExcludesOrphanedTenantUsers()
    {
        await using var db = CreateEphemeralContext();
        var deletedTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = deletedTenantId,
            Name = "Cafe Test",
            Slug = "cafe",
            Status = TenantStatuses.Deleted,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "cafe-admin",
            UserName = "admin@cafe.regkasse.at",
            Email = "admin@cafe.regkasse.at",
            FirstName = "Admin",
            LastName = "Test Cafe",
            Role = Roles.Manager,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "cafe-admin",
            TenantId = deletedTenantId,
            IsActive = false,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.List(type: "platform", isActive: true);
        var dtos = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(
            Assert.IsType<OkObjectResult>(result).Value).ToList();
        Assert.DoesNotContain(dtos, d => d.Id == "cafe-admin");
    }

    [Fact]
    public async Task List_TypeTenant_ReturnsUserName_OnEachRow()
    {
        await using var db = CreateEphemeralContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-list",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "u-tenant",
            UserName = "cashier42",
            Email = "cashier@cafe.test",
            FirstName = "Anna",
            LastName = "Kassier",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "u-tenant",
            TenantId = tenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.List(type: "tenant");

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<IEnumerable<AdminUsersController.AdminTenantUserRowDto>>(ok.Value)
            .Where(r => r.UserId == "u-tenant")
            .ToList();
        Assert.Single(rows);
        Assert.Equal("cashier42", rows[0].UserName);
        Assert.Equal("u-tenant", rows[0].UserId);
    }

    [Fact]
    public async Task List_Search_Matches_UserName_And_Email()
    {
        await using var db = CreateEphemeralContext();
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = "by-name",
                UserName = "mustafa",
                Email = "other@test.com",
                FirstName = "M",
                LastName = "User",
                Role = Roles.Manager,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new ApplicationUser
            {
                Id = "by-email",
                UserName = "cashier9",
                Email = "mustafa@cafe.test",
                FirstName = "E",
                LastName = "Mail",
                Role = Roles.Cashier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new ApplicationUser
            {
                Id = "no-match",
                UserName = "other",
                Email = "nobody@test.com",
                FirstName = "X",
                LastName = "Y",
                Role = Roles.Cashier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.List(search: "mustafa");

        var ok = Assert.IsType<OkObjectResult>(result);
        var ids = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(ok.Value)
            .Select(d => d.Id)
            .ToHashSet();
        Assert.Contains("by-name", ids);
        Assert.Contains("by-email", ids);
        Assert.DoesNotContain("no-match", ids);
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

        Assert.Matches(@"^admin\d+$", body.UserName);
        Assert.NotEqual(body.Email, body.UserName);

        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "new.platform@test.com");
        Assert.Equal(body.UserName, persisted.UserName);
        Assert.True(persisted.MustChangePasswordOnNextLogin);
        Assert.True(PasswordGenerator.GenerateSecurePassword().Length >= 12);
    }

    [Fact]
    public void AdminCreateUserRequest_Deserializes_TenantId_FromCamelCaseJson()
    {
        var tenantId = Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c");
        var json =
            $$"""{"email":"manager@test.com","role":"Manager","tenantId":"{{tenantId:D}}","isOwner":true}""";

        var request = JsonSerializer.Deserialize<AdminCreateUserRequest>(
            json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            });

        Assert.NotNull(request);
        Assert.Equal(tenantId, request.TenantId);
        Assert.True(request.IsOwner);
    }

    [Fact]
    public async Task Create_ManagerWithoutTenantId_ReturnsBadRequest_DoesNotProvisionDefaultTenant()
    {
        await using var db = CreateEphemeralContext();
        await SeedRolesAsync(db);
        var tenantUserService = new Mock<ITenantUserService>();
        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            tenantUserService: tenantUserService.Object);

        var result = await controller.Create(new AdminCreateUserRequest
        {
            Email = "manager@test.com",
            Role = Roles.Manager,
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Contains("tenantId", error.Errors!.Keys, StringComparer.OrdinalIgnoreCase);
        tenantUserService.Verify(
            x => x.CreateAsync(
                It.IsAny<Guid>(),
                It.IsAny<CreateTenantUserRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.False(await db.Users.AnyAsync(u => u.Email == "manager@test.com"));
    }

    [Fact]
    public async Task Create_ManagerWithTenantIdInBody_DelegatesToTenantUserService()
    {
        await using var db = CreateEphemeralContext();
        await SeedRolesAsync(db);
        var tenantId = Guid.NewGuid();
        var tenantUserService = new Mock<ITenantUserService>();
        tenantUserService
            .Setup(x => x.CreateAsync(
                tenantId,
                It.IsAny<CreateTenantUserRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new CreateTenantUserResultDto(
                    "user-tenant-1",
                    "manager@test.com",
                    "manager1",
                    "Temp#Pass12345",
                    true,
                    true,
                    TenantId: tenantId,
                    TenantSlug: "dev"),
                null));

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            tenantUserService: tenantUserService.Object);

        var result = await controller.Create(new AdminCreateUserRequest
        {
            Email = "manager@test.com",
            Role = Roles.Manager,
            TenantId = tenantId,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<CreateTenantUserResultDto>(created.Value);
        Assert.Equal(tenantId, body.TenantId);
        Assert.Equal("dev", body.TenantSlug);
        tenantUserService.Verify(
            x => x.CreateAsync(
                tenantId,
                It.Is<CreateTenantUserRequest>(r => r.Email == "manager@test.com" && r.Role == Roles.Manager),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateUsername_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

        var result = await controller.UpdateUsername(
            "nonexistent",
            new UpdateUsernameRequest { NewUsername = "newname" });

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.IsType<ApiError>(notFound.Value);
    }

    [Fact]
    public async Task UpdateUsername_WhenInvalidFormat_ReturnsBadRequest()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "valid1",
            Email = "u@test.com",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var tooShort = await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "ab" });
        Assert.IsType<BadRequestObjectResult>(tooShort.Result);

        var invalidChars = await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "bad name" });
        Assert.IsType<BadRequestObjectResult>(invalidChars.Result);
    }

    [Fact]
    public async Task UpdateUsername_WhenReserved_ReturnsBadRequest()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "cashier1",
            Email = "u@test.com",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "admin" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal(400, body.Status);
    }

    [Fact]
    public async Task UpdateUsername_WhenAccountTooNew_ReturnsBadRequest()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "oldname",
            NormalizedUserName = "OLDNAME",
            Email = "op@test.com",
            FirstName = "C",
            LastName = "D",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "newname" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Contains("24", body.Detail ?? string.Empty, StringComparison.Ordinal);

        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.Id == "user-1");
        Assert.Equal("oldname", persisted.UserName);
    }

    [Fact]
    public async Task UpdateUsername_WhenRateLimited_ReturnsBadRequest()
    {
        await using var db = CreateEphemeralContext();
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "oldname",
            NormalizedUserName = "OLDNAME",
            Email = "u@test.com",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        await userManager.AddClaimAsync(
            user,
            new System.Security.Claims.Claim(
                UsernameChangeRateLimit.LastChangeClaimType,
                DateTime.UtcNow.AddDays(-1).ToString("O")));

        var controller = CreateController(
            userManager,
            new RoleManager<IdentityRole>(
                new RoleStore<IdentityRole>(db),
                null!,
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!),
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            context: db);

        var result = await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "newname" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Contains("7 days", body.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.Id == "user-1");
        Assert.Equal("oldname", persisted.UserName);
    }

    [Fact]
    public async Task UpdateUsername_WhenUsernameTaken_ReturnsConflict()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "taken",
            Email = "taken@test.com",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "user-2",
            UserName = "operator",
            Email = "op@test.com",
            FirstName = "C",
            LastName = "D",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
        });
        await db.SaveChangesAsync();

        var uniqueness = new Mock<IUserUniquenessValidationService>();
        uniqueness.Setup(x => x.IsUserNameTakenByOtherUserAsync("taken", "user-2")).ReturnsAsync(true);
        uniqueness.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        uniqueness.Setup(x => x.IsEmployeeNumberTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);
        uniqueness.Setup(x => x.IsTaxNumberTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(false);

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            uniqueness.Object);

        var result = await controller.UpdateUsername(
            "user-2",
            new UpdateUsernameRequest { NewUsername = "taken" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateUsername_Success_UpdatesUser_LogsAudit_AndInvalidatesSessions()
    {
        await using var db = CreateEphemeralContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-upd",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "oldname",
            NormalizedUserName = "OLDNAME",
            Email = "op@test.com",
            FirstName = "C",
            LastName = "D",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            TenantId = tenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var before = await db.Users.AsNoTracking().SingleAsync(u => u.Id == "user-1");
        var stampBefore = before.SecurityStamp;

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogUserLifecycleAsync(
                AuditEventType.UserNameChanged,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "user-1",
                tenantId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid(), Action = AuditLogActions.USER_NAME_CHANGE });

        var session = new Mock<IUserSessionInvalidation>();
        session.Setup(x => x.InvalidateSessionsForUserAsync("user-1")).Returns(Task.CompletedTask);

        var controller = CreateController(db, audit.Object, session.Object);

        var result = await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "newname", Reason = "Support rename" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AdminUsersController.AdminUpdateUsernameResponse>(ok.Value);
        Assert.Equal("oldname", body.OldUsername);
        Assert.Equal("newname", body.NewUsername);

        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.Id == "user-1");
        Assert.Equal("newname", persisted.UserName);
        Assert.Equal("NEWNAME", persisted.NormalizedUserName);
        Assert.NotEqual(stampBefore, persisted.SecurityStamp);

        audit.Verify(
            x => x.LogUserLifecycleAsync(
                AuditEventType.UserNameChanged,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "user-1",
                tenantId,
                "Support rename",
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.Is<string?>(d => d != null && d.Contains("oldname") && d.Contains("newname")),
                It.IsAny<object?>(),
                It.IsAny<object?>()),
            Times.Once);
        session.Verify(x => x.InvalidateSessionsForUserAsync("user-1"), Times.Once);
    }

    [Fact]
    public async Task UpdateUsername_Success_Records_Username_History()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "oldname",
            NormalizedUserName = "OLDNAME",
            Email = "op@test.com",
            FirstName = "C",
            LastName = "D",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
        });
        await db.SaveChangesAsync();

        var historyMock = new Mock<IUserUsernameHistoryService>();
        historyMock.Setup(x => x.RecordChangeAsync(
                "user-1",
                "oldname",
                "newname",
                It.IsAny<string?>(),
                "Support",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            usernameHistory: historyMock.Object);

        await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "newname", Reason = "Support" });

        historyMock.Verify(
            x => x.RecordChangeAsync(
                "user-1",
                "oldname",
                "newname",
                It.IsAny<string?>(),
                "Support",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUsernameHistory_Returns_Rows_For_User()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "newname",
            Email = "op@test.com",
            Role = Roles.Cashier,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var historyMock = new Mock<IUserUsernameHistoryService>();
        historyMock.Setup(x => x.ListForUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserUsernameHistoryDto
                {
                    Id = Guid.NewGuid(),
                    OldUsername = "oldname",
                    NewUsername = "newname",
                    ChangedAtUtc = DateTime.UtcNow,
                },
            });

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            usernameHistory: historyMock.Object);

        var result = await controller.GetUsernameHistory("user-1");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<UserUsernameHistoryDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal("oldname", rows[0].OldUsername);
    }

    [Fact]
    public async Task UpdateUsername_Success_Sends_Email_Notification()
    {
        await using var db = CreateEphemeralContext();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "oldname",
            NormalizedUserName = "OLDNAME",
            Email = "op@test.com",
            FirstName = "C",
            LastName = "D",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "admin-id",
            UserName = "admin",
            Email = "admin@regkasse.test",
            Role = Roles.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddHours(-25),
        });
        await db.SaveChangesAsync();

        var emailMock = new Mock<IUsernameChangeEmailService>();
        emailMock.Setup(x => x.IsConfigured).Returns(true);
        emailMock.Setup(x => x.TrySendUsernameChangedAsync(It.IsAny<UsernameChangedEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            usernameChangeEmail: emailMock.Object);

        await controller.UpdateUsername(
            "user-1",
            new UpdateUsernameRequest { NewUsername = "newname" });

        emailMock.Verify(
            x => x.TrySendUsernameChangedAsync(
                It.Is<UsernameChangedEmailRequest>(r =>
                    r.ToEmail == "op@test.com"
                    && r.OldUsername == "oldname"
                    && r.NewUsername == "newname"
                    && r.ChangedByAdminEmail == "admin@regkasse.test"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUsernameSuggestions_WhenRoleMissing_ReturnsBadRequest()
    {
        await using var db = CreateEphemeralContext();
        await SeedRolesAsync(db);
        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.GetUsernameSuggestions(role: null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUsernameSuggestions_Returns_Next_Username_For_Role()
    {
        await using var db = CreateEphemeralContext();
        await SeedRolesAsync(db);
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "manager1",
            NormalizedUserName = "MANAGER1",
            Email = "m1@test.com",
            Role = Roles.Manager,
            IsActive = true,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "manager2",
            NormalizedUserName = "MANAGER2",
            Email = "m2@test.com",
            Role = Roles.Manager,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(
            db,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.GetUsernameSuggestions(Roles.Manager);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<UsernameSuggestionResponse>(ok.Value);
        Assert.Equal("manager3", body.SuggestedUsername);
        Assert.Equal(new[] { 3, 4, 5 }, body.AvailableNumbers);
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

    private static async Task<(AppDbContext Db, AdminUsersController Controller, ApplicationUser OtherTenantUser)> SeedCrossTenantMutationScenarioAsync(
        string actorRole = Roles.Manager)
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var db = CreateEphemeralContext();

        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantAId,
                Name = "Tenant A",
                Slug = "tenant-a",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = tenantBId,
                Name = "Tenant B",
                Slug = "tenant-b",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        var otherTenantUser = new ApplicationUser
        {
            Id = "user-tenant-b",
            UserName = "cashier-b",
            Email = "cashier-b@test.com",
            FirstName = "Other",
            LastName = "Tenant",
            Role = Roles.Cashier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = "etag-b",
        };
        db.Users.Add(otherTenantUser);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = otherTenantUser.Id,
            TenantId = tenantBId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var userManager = CreateUserManager(db);
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(db),
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);

        var controller = CreateController(
            userManager,
            roleManager,
            new Mock<IAuditLogService>().Object,
            new Mock<IUserSessionInvalidation>().Object,
            actorId: "manager-a",
            actorRole: actorRole,
            context: db,
            tenantAccessor: new CurrentTenantAccessor { TenantId = tenantAId });

        return (db, controller, otherTenantUser);
    }

    [Fact]
    public async Task Patch_WhenUserNotInAmbientTenant_ReturnsNotFound()
    {
        var (_, controller, user) = await SeedCrossTenantMutationScenarioAsync();

        var result = await controller.Patch(
            user.Id,
            new AdminUsersController.AdminPatchUserRequest { FirstName = "Hacked" },
            ifMatch: user.ConcurrencyStamp);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Deactivate_WhenUserNotInAmbientTenant_ReturnsNotFound()
    {
        var (_, controller, user) = await SeedCrossTenantMutationScenarioAsync();

        var result = await controller.Deactivate(
            user.Id,
            new AdminUsersController.AdminDeactivateRequest { Reason = "Cross-tenant attempt" });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Reactivate_WhenUserNotInAmbientTenant_ReturnsNotFound()
    {
        var (_, controller, user) = await SeedCrossTenantMutationScenarioAsync();
        user.IsActive = false;

        var result = await controller.Reactivate(user.Id, null);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateUsername_WhenUserNotInAmbientTenant_ReturnsNotFound()
    {
        var (_, controller, user) = await SeedCrossTenantMutationScenarioAsync();

        var result = await controller.UpdateUsername(
            user.Id,
            new UpdateUsernameRequest { NewUsername = "hacked-name", Reason = "test" });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ForcePasswordReset_WhenUserNotInAmbientTenant_ReturnsNotFound()
    {
        var (_, controller, user) = await SeedCrossTenantMutationScenarioAsync();

        var result = await controller.ForcePasswordReset(
            user.Id,
            new AdminUsersController.AdminForcePasswordResetRequest { NewPassword = "NewPass123!" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GenerateTemporaryPassword_WhenUserNotInAmbientTenant_ReturnsNotFound()
    {
        var (_, controller, user) = await SeedCrossTenantMutationScenarioAsync(actorRole: Roles.SuperAdmin);

        var result = await controller.GenerateTemporaryPassword(user.Id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}

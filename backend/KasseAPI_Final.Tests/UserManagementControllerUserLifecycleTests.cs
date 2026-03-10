using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for UserManagementController: deactivate (reason required), reactivate, audit and session invalidation.
/// </summary>
public class UserManagementControllerUserLifecycleTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"UserMgmt_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static (UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole> RoleManager) CreateMockUserAndRoleManagers(
        ApplicationUser? existingUser = null,
        bool updateSucceeds = true)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        if (updateSucceeds)
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
        // So controller role-exists validation passes when tests send Role = "SuperAdmin" (or any name)
        roleStore.Setup(x => x.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => new IdentityRole(name));
        var roleManager = new RoleManager<IdentityRole>(
            roleStore.Object, null!, keyNormalizer.Object, errors, null!);

        return (userManager, roleManager);
    }

    private static IUserUniquenessValidationService CreateUniquenessValidationMock(bool emailTaken = false, bool employeeNumberTaken = false, bool taxNumberTaken = false)
    {
        var m = new Mock<IUserUniquenessValidationService>();
        m.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(emailTaken);
        m.Setup(x => x.IsEmployeeNumberTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(employeeNumberTaken);
        m.Setup(x => x.IsTaxNumberTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(taxNumberTaken);
        m.Setup(x => x.ValidateUniquenessForUpdateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((false, (string?)null));
        return m.Object;
    }

    private static UserManagementController CreateController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUniquenessValidationService? uniquenessValidation = null,
        IRoleManagementService? roleManagementService = null,
        string? actorId = "admin-id",
        string actorRole = "SuperAdmin")
    {
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var roleMgmt = roleManagementService ?? new Mock<IRoleManagementService>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, auditLogService, sessionInvalidation, uniquenessValidation ?? CreateUniquenessValidationMock(), roleMgmt, logger);
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
    public async Task DeactivateUser_WhenReasonEmpty_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session);

        var result = await controller.DeactivateUser("u1", new DeactivateUserRequest { Reason = "" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(badRequest.Value ?? new object());
        Assert.Contains("reason", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeactivateUser_WhenReasonNull_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session);

        var result = await controller.DeactivateUser("u1", new DeactivateUserRequest { Reason = null! });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateUser_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session);

        var result = await controller.DeactivateUser("nonexistent", new DeactivateUserRequest { Reason = "Left company" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateUser_WhenActorIsTarget_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session, actorId: "u1", actorRole: "Manager");

        var result = await controller.DeactivateUser("u1", new DeactivateUserRequest { Reason = "Leaving" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(badRequest.Value ?? new object());
        Assert.Contains("own", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeactivateUser_WhenUserAlreadyDeactivated_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = false };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session);

        var result = await controller.DeactivateUser("u1", new DeactivateUserRequest { Reason = "Again" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateUser_WhenValid_CallsAuditAndSessionInvalidation()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        var sessionMock = new Mock<IUserSessionInvalidation>();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, sessionMock.Object);

        var result = await controller.DeactivateUser("u1", new DeactivateUserRequest { Reason = "Left company" });

        var ok = Assert.IsType<OkObjectResult>(result);
        auditMock.Verify(
            x => x.LogUserLifecycleAsync(
                AuditLogActions.USER_DEACTIVATE,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "u1",
                "Left company",
                null,
                AuditLogStatus.Success,
                It.IsAny<string>()),
            Times.Once);
        sessionMock.Verify(x => x.InvalidateSessionsForUserAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateUser_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session);

        var result = await controller.ReactivateUser("nonexistent", null);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReactivateUser_WhenUserAlreadyActive_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, audit, session);

        var result = await controller.ReactivateUser("u1", null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ReactivateUser_WhenValid_CallsAudit()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = false };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        var sessionMock = new Mock<IUserSessionInvalidation>();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, sessionMock.Object);

        var result = await controller.ReactivateUser("u1", null);

        var ok = Assert.IsType<OkObjectResult>(result);
        auditMock.Verify(
            x => x.LogUserLifecycleAsync(
                AuditLogActions.USER_REACTIVATE,
                It.IsAny<string>(),
                It.IsAny<string>(),
                "u1",
                null,
                null,
                AuditLogStatus.Success,
                It.IsAny<string>()),
            Times.Once);
    }

    /// <summary>DELETE is soft-delete only (RKSV/fiscal: no hard delete of users).</summary>
    [Fact]
    public async Task DeleteUser_SoftDeleteOnly_UserStillExistsWithIsActiveFalse()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        var sessionMock = new Mock<IUserSessionInvalidation>();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, sessionMock.Object);

        var result = await controller.DeleteUser("u1");

        Assert.IsType<OkObjectResult>(result);
        // Backend never removes the user; only IsActive is set to false (soft-delete)
        var stillExists = await userManager.FindByIdAsync("u1");
        Assert.NotNull(stillExists);
        Assert.False(stillExists.IsActive);
    }

    /// <summary>Force-reset endpoint must block self; user must use me/password.</summary>
    [Fact]
    public async Task ResetPassword_WhenTargetIsSelf_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "self-id", UserName = "self", FirstName = "S", LastName = "U", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object,
            actorId: "self-id", actorRole: "Manager");

        var result = await controller.ResetPassword("self-id", new ResetPasswordRequest { NewPassword = "NewPass123!" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    /// <summary>Admin cannot force-reset a SuperAdmin user; only SuperAdmin can.</summary>
    [Fact]
    public async Task ResetPassword_WhenAdminResetsSuperAdmin_Returns403()
    {
        var superAdminUser = new ApplicationUser { Id = "super-id", UserName = "super", FirstName = "S", LastName = "A", IsActive = true, Role = "SuperAdmin" };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: superAdminUser);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object,
            actorId: "admin-id", actorRole: "Manager");

        var result = await controller.ResetPassword("super-id", new ResetPasswordRequest { NewPassword = "NewPass123!" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
        Assert.NotNull(statusResult.Value);
    }

    /// <summary>When target user does not exist, returns 404.</summary>
    [Fact]
    public async Task ResetPassword_WhenUserNotFound_Returns404()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object,
            actorId: "admin-id", actorRole: "Manager");

        var result = await controller.ResetPassword("nonexistent-id", new ResetPasswordRequest { NewPassword = "NewPass123!" });

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    /// <summary>When target user is inactive, returns 404.</summary>
    [Fact]
    public async Task ResetPassword_WhenUserInactive_Returns404()
    {
        var user = new ApplicationUser { Id = "inactive-id", UserName = "inactive", FirstName = "I", LastName = "U", IsActive = false };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object,
            actorId: "admin-id", actorRole: "Manager");

        var result = await controller.ResetPassword("inactive-id", new ResetPasswordRequest { NewPassword = "NewPass123!" });

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    /// <summary>When new password is too short, returns 400.</summary>
    [Fact]
    public async Task ResetPassword_WhenPasswordTooShort_ReturnsBadRequest()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object,
            actorId: "admin-id", actorRole: "Manager");

        var result = await controller.ResetPassword("any-id", new ResetPasswordRequest { NewPassword = "1234567" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("8", json);
    }

    /// <summary>When request body is null, returns 400.</summary>
    [Fact]
    public async Task ResetPassword_WhenRequestNull_ReturnsBadRequest()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object,
            actorId: "admin-id", actorRole: "Manager");

        var result = await controller.ResetPassword("any-id", request: null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    /// <summary>Error isolation: when audit log write fails, primary operation (deactivate) still returns 200.</summary>
    [Fact]
    public async Task DeactivateUser_WhenAuditLogThrows_StillReturnsOk()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogUserLifecycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("audit_logs insert failed"));
        var sessionMock = new Mock<IUserSessionInvalidation>();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, sessionMock.Object);

        var result = await controller.DeactivateUser("u1", new DeactivateUserRequest { Reason = "Test" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var stillExists = await userManager.FindByIdAsync("u1");
        Assert.NotNull(stillExists);
        Assert.False(stillExists.IsActive);
    }

    // --- UpdateUser (PUT /api/UserManagement/{id}) ---

    [Fact]
    public async Task UpdateUser_WhenIdEmpty_ReturnsBadRequest()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUser("", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task UpdateUser_WhenEmployeeNumberEmpty_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "", Role = "SuperAdmin" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Employee number", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_ReturnsNotFound()
    {
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: null);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUser("nonexistent", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>Valid payload (same role to avoid GetRolesAsync); expects 200 and message.</summary>
    [Fact]
    public async Task UpdateUser_WhenValidPayload_SameRole_ReturnsOkWithMessage()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, new Mock<IUserSessionInvalidation>().Object);

        var request = new UpdateUserRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            EmployeeNumber = "E1",
            Role = "SuperAdmin",
            Notes = "Note"
        };
        var result = await controller.UpdateUser("u1", request);

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var value = ok.Value;
        Assert.NotNull(value);
        var message = value.GetType().GetProperty("message")?.GetValue(value) as string;
        Assert.Equal("User updated successfully", message);
        auditMock.Verify(
            x => x.LogUserLifecycleAsync(AuditLogActions.USER_UPDATE, It.IsAny<string>(), It.IsAny<string>(), "u1", null, null, AuditLogStatus.Success, It.IsAny<string>()),
            Times.Once);
    }

    /// <summary>When audit log throws, UpdateUser still returns 200 (error isolation).</summary>
    [Fact]
    public async Task UpdateUser_WhenAuditLogThrows_StillReturnsOk()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogUserLifecycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("audit_logs insert failed"));
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin" });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var value = ok.Value;
        Assert.NotNull(value);
        var message = value.GetType().GetProperty("message")?.GetValue(value) as string;
        Assert.Equal("User updated successfully", message);
    }

    /// <summary>Self-update: keeping the same employee number must succeed (exclude current user by user.Id).</summary>
    [Fact]
    public async Task UpdateUser_SelfUpdate_SameEmployeeNumber_ReturnsOk()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "234234", Role = "SuperAdmin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object);

        var request = new UpdateUserRequest
        {
            FirstName = "aaa",
            LastName = "bbbb",
            EmployeeNumber = "234234",
            Role = "SuperAdmin",
            Notes = "dfgsfg"
        };
        var result = await controller.UpdateUser("u1", request);

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var value = ok.Value;
        Assert.NotNull(value);
        var message = value.GetType().GetProperty("message")?.GetValue(value) as string;
        Assert.Equal("User updated successfully", message);
    }

    /// <summary>Update to an employee number already used by another user must return 400.</summary>
    [Fact]
    public async Task UpdateUser_WhenEmployeeNumberTakenByAnotherUser_ReturnsBadRequest()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "user1", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true },
            new ApplicationUser { Id = "u2", UserName = "user2", FirstName = "C", LastName = "D", EmployeeNumber = "E2", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E2", Role = "SuperAdmin" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Employee number already exists", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Update to a tax number already used by another user must return 400.</summary>
    [Fact]
    public async Task UpdateUser_WhenTaxNumberTakenByAnotherUser_ReturnsBadRequest()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "user1", FirstName = "A", LastName = "B", EmployeeNumber = "E1", TaxNumber = "T1", Role = "SuperAdmin", IsActive = true },
            new ApplicationUser { Id = "u2", UserName = "user2", FirstName = "C", LastName = "D", EmployeeNumber = "E2", TaxNumber = "T2", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var request = new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", TaxNumber = "T2", Role = "SuperAdmin" };
        var result = await controller.UpdateUser("u1", request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Tax number already exists", json, StringComparison.OrdinalIgnoreCase);
    }

    // --- Employee number & email uniqueness: required test matrix ---

    /// <summary>Self-update: PUT /api/UserManagement/{currentUserId} with same employeeNumber must succeed (current user excluded from uniqueness check).</summary>
    [Fact]
    public async Task UpdateUser_SelfUpdate_OwnEmployeeNumber_Returns200()
    {
        var currentUserId = "63760eeb-750a-494a-93d2-65a592e20bb3";
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = currentUserId, UserName = "me", FirstName = "A", LastName = "B", EmployeeNumber = "234234", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser(currentUserId, new UpdateUserRequest
        {
            FirstName = "aaa",
            LastName = "bbbb",
            Email = "aa@bb.cc",
            EmployeeNumber = "234234",
            Role = "SuperAdmin",
            TaxNumber = "123",
            Notes = "notes"
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.NotNull(ok.Value);
        var message = ok.Value.GetType().GetProperty("message")?.GetValue(ok.Value) as string;
        Assert.Equal("User updated successfully", message);
    }

    /// <summary>1. Update same user with same employeeNumber -> success.</summary>
    [Fact]
    public async Task UpdateUser_SameUser_SameEmployeeNumber_ReturnsSuccess()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", FirstName = "A", LastName = "B", EmployeeNumber = "EN-001", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A", LastName = "B", EmployeeNumber = "EN-001", Role = "SuperAdmin"
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    /// <summary>2. Update same user with new unique employeeNumber -> success.</summary>
    [Fact]
    public async Task UpdateUser_SameUser_NewUniqueEmployeeNumber_ReturnsSuccess()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", FirstName = "A", LastName = "B", EmployeeNumber = "EN-001", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A", LastName = "B", EmployeeNumber = "EN-999", Role = "SuperAdmin"
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    /// <summary>3. Update same user with employeeNumber used by another user -> 400.</summary>
    [Fact]
    public async Task UpdateUser_SameUser_EmployeeNumberUsedByOther_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", FirstName = "A", LastName = "B", EmployeeNumber = "EN-001", Role = "SuperAdmin", IsActive = true },
            new ApplicationUser { Id = "u2", UserName = "u2", FirstName = "C", LastName = "D", EmployeeNumber = "EN-002", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A", LastName = "B", EmployeeNumber = "EN-002", Role = "SuperAdmin"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Employee number already exists", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>4. Create new user with duplicate employeeNumber -> 400.</summary>
    [Fact]
    public async Task CreateUser_WhenEmployeeNumberDuplicate_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "existing", Email = "existing@example.com", FirstName = "A", LastName = "B", EmployeeNumber = "EN-001", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var request = new CreateUserRequest
        {
            UserName = "newuser",
            Password = "Pass123!@#",
            Email = "new@example.com",
            FirstName = "X",
            LastName = "Y",
            EmployeeNumber = "EN-001",
            Role = "SuperAdmin"
        };
        var result = await controller.CreateUser(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Employee number already exists", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>5a. Update same user with same email -> success.</summary>
    [Fact]
    public async Task UpdateUser_SameUser_SameEmail_ReturnsSuccess()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", Email = "same@test.com", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A", LastName = "B", Email = "same@test.com", EmployeeNumber = "E1", Role = "SuperAdmin"
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    /// <summary>5b. Update same user with new unique email -> success.</summary>
    [Fact]
    public async Task UpdateUser_SameUser_NewUniqueEmail_ReturnsSuccess()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", Email = "old@test.com", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A", LastName = "B", Email = "newunique@test.com", EmployeeNumber = "E1", Role = "SuperAdmin"
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    /// <summary>5c. Update same user with email used by another user -> 400.</summary>
    [Fact]
    public async Task UpdateUser_SameUser_EmailUsedByOther_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", Email = "one@test.com", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true },
            new ApplicationUser { Id = "u2", UserName = "u2", Email = "two@test.com", FirstName = "C", LastName = "D", EmployeeNumber = "E2", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A", LastName = "B", Email = "two@test.com", EmployeeNumber = "E1", Role = "SuperAdmin"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Email already exists", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Create user with empty role -> 400 ROLE_REQUIRED.</summary>
    [Fact]
    public async Task CreateUser_WhenRoleEmpty_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync();
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var request = new CreateUserRequest
        {
            UserName = "newuser",
            Password = "Pass123!@#",
            Email = "new@example.com",
            FirstName = "X",
            LastName = "Y",
            EmployeeNumber = "EN-999",
            Role = ""
        };
        var result = await controller.CreateUser(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("ROLE_REQUIRED", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Create user with non-existent role -> 400 ROLE_NOT_FOUND.</summary>
    [Fact]
    public async Task CreateUser_WhenRoleNotFound_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync();
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var request = new CreateUserRequest
        {
            UserName = "newuser",
            Password = "Pass123!@#",
            Email = "new@example.com",
            FirstName = "X",
            LastName = "Y",
            EmployeeNumber = "EN-999",
            Role = "NonExistentRole"
        };
        var result = await controller.CreateUser(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("ROLE_NOT_FOUND", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Update user with empty role -> 400 ROLE_REQUIRED.</summary>
    [Fact]
    public async Task UpdateUser_WhenRoleEmpty_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A",
            LastName = "B",
            EmployeeNumber = "E1",
            Role = ""
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("ROLE_REQUIRED", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Update user with non-existent role -> 400 ROLE_NOT_FOUND.</summary>
    [Fact]
    public async Task UpdateUser_WhenRoleNotFound_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "u1", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest
        {
            FirstName = "A",
            LastName = "B",
            EmployeeNumber = "E1",
            Role = "NonExistentRole"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("ROLE_NOT_FOUND", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>5d. Create new user with duplicate email -> 400.</summary>
    [Fact]
    public async Task CreateUser_WhenEmailDuplicate_Returns400()
    {
        var (context, userManager, roleManager, uniquenessValidation) = await CreateInMemoryUserManagerWithUsersAsync(
            new ApplicationUser { Id = "u1", UserName = "existing", Email = "taken@test.com", FirstName = "A", LastName = "B", EmployeeNumber = "EN-001", Role = "SuperAdmin", IsActive = true });
        var controller = CreateController(context, userManager, roleManager, new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object, uniquenessValidation);

        var request = new CreateUserRequest
        {
            UserName = "newuser",
            Password = "Pass123!@#",
            Email = "taken@test.com",
            FirstName = "X",
            LastName = "Y",
            EmployeeNumber = "EN-002",
            Role = "SuperAdmin"
        };
        var result = await controller.CreateUser(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Email already exists", json, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNormalizedFields(ApplicationUser user)
    {
        if (user.UserName != null && user.NormalizedUserName == null)
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
        if (user.Email != null && user.NormalizedEmail == null)
            user.NormalizedEmail = user.Email.ToUpperInvariant();
    }

    private static async Task<(AppDbContext Context, UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole> RoleManager, IUserUniquenessValidationService UniquenessValidation)> CreateInMemoryUserManagerWithUsersAsync(params ApplicationUser[] users)
    {
        var dbName = $"UserMgmt_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var context = new AppDbContext(options);
        foreach (var u in users)
        {
            EnsureNormalizedFields(u);
            context.Users.Add(u);
        }
        await context.SaveChangesAsync();

        var userStore = new UserStore<ApplicationUser, IdentityRole, AppDbContext>(context, null);
        var optionsIdentity = Options.Create(new IdentityOptions());
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>().Object;
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizerMock = new Mock<ILookupNormalizer>();
        keyNormalizerMock.Setup(x => x.NormalizeEmail(It.IsAny<string>())).Returns<string>(s => s?.ToUpperInvariant() ?? "");
        keyNormalizerMock.Setup(x => x.NormalizeName(It.IsAny<string>())).Returns<string>(s => s?.ToUpperInvariant() ?? "");
        var keyNormalizer = keyNormalizerMock.Object;
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;
        var userManager = new UserManager<ApplicationUser>(
            userStore, optionsIdentity, hasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger);

        var roleStore = new RoleStore<IdentityRole, AppDbContext>(context);
        var roleManager = new RoleManager<IdentityRole>(roleStore, null!, keyNormalizer, errors, null!);
        // Seed roles via RoleManager so NormalizedName is set and controller role-exists validation passes
        foreach (var roleName in new[] { "Cashier", "Manager", "SuperAdmin", "Waiter" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }
        var uniquenessValidation = new UserUniquenessValidationService(userManager);

        return (context, userManager, roleManager, uniquenessValidation);
    }
}

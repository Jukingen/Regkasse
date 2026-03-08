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
        var roleManager = new RoleManager<IdentityRole>(
            roleStore.Object, null!, keyNormalizer.Object, errors, null!);

        return (userManager, roleManager);
    }

    private static UserManagementController CreateController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        string? actorId = "admin-id",
        string actorRole = "Administrator")
    {
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, auditLogService, sessionInvalidation, logger);
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
        var controller = CreateController(context, userManager, roleManager, audit, session, actorId: "u1", actorRole: "Administrator");

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
            actorId: "self-id", actorRole: "Admin");

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
            actorId: "admin-id", actorRole: "Admin");

        var result = await controller.ResetPassword("super-id", new ResetPasswordRequest { NewPassword = "NewPass123!" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
        Assert.NotNull(statusResult.Value);
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

        var result = await controller.UpdateUser("", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "Admin" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task UpdateUser_WhenEmployeeNumberEmpty_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "Admin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager,
            new Mock<IAuditLogService>().Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "", Role = "Admin" });

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

        var result = await controller.UpdateUser("nonexistent", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "Admin" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    /// <summary>Valid payload (same role to avoid GetRolesAsync); expects 200 and message.</summary>
    [Fact]
    public async Task UpdateUser_WhenValidPayload_SameRole_ReturnsOkWithMessage()
    {
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "Admin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, new Mock<IUserSessionInvalidation>().Object);

        var request = new UpdateUserRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            EmployeeNumber = "E1",
            Role = "Admin",
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
        var user = new ApplicationUser { Id = "u1", UserName = "u", FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "Admin", IsActive = true };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogUserLifecycleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("audit_logs insert failed"));
        using var context = CreateContext();
        var controller = CreateController(context, userManager, roleManager, auditMock.Object, new Mock<IUserSessionInvalidation>().Object);

        var result = await controller.UpdateUser("u1", new UpdateUserRequest { FirstName = "A", LastName = "B", EmployeeNumber = "E1", Role = "Admin" });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var value = ok.Value;
        Assert.NotNull(value);
        var message = value.GetType().GetProperty("message")?.GetValue(value) as string;
        Assert.Equal("User updated successfully", message);
    }
}

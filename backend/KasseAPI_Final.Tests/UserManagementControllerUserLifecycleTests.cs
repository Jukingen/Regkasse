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

}

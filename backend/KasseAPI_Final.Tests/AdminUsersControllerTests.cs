using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private static (UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole> RoleManager) CreateMockUserAndRoleManagers(ApplicationUser? existingUser = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

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
        string actorRole = "Admin")
    {
        var logger = new Mock<ILogger<AdminUsersController>>().Object;
        var controller = new AdminUsersController(userManager, roleManager, auditLogService, sessionInvalidation, uniquenessValidation ?? CreateUniquenessValidationMock(), logger);
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
        var user = new ApplicationUser
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
        };
        var (userManager, roleManager) = CreateMockUserAndRoleManagers(existingUser: user);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var controller = CreateController(userManager, roleManager, audit, session);

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
        var controller = CreateController(userManager, roleManager, audit, session, actorId: "u1", actorRole: "Admin");

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

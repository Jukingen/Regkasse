using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AuthControllerTests
{
    #region Helpers

    private static UserManager<ApplicationUser> CreateMockUserManager(
        ApplicationUser? userByEmail,
        bool passwordValid = true,
        IList<string>? roles = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.As<IUserEmailStore<ApplicationUser>>()
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userByEmail);

        var options = Options.Create(new IdentityOptions());
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();
        hasher.Setup(h => h.VerifyHashedPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(passwordValid ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed);
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, options, hasher.Object, userValidators, passwordValidators,
            keyNormalizer.Object, errors, services, logger);

        mgr.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(userByEmail);

        mgr.Setup(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(passwordValid);

        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(roles ?? new List<string>());

        mgr.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        return mgr.Object;
    }

    private static IConfiguration CreateConfig(bool allowLegacy = true)
    {
        var data = new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "test-secret-key-at-least-32-characters-long!!",
            ["JwtSettings:Issuer"] = "Test",
            ["JwtSettings:Audience"] = "Test",
            ["Auth:AllowLegacyLoginWithoutClientApp"] = allowLegacy ? "true" : "false",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static Mock<ITokenClaimsService> CreateTokenClaimsMock()
    {
        var mock = new Mock<ITokenClaimsService>();
        mock.Setup(t => t.BuildClaimsAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<IList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "id") });
        return mock;
    }

    private static AuthController CreateController(
        ApplicationUser? userByEmail,
        bool passwordValid = true,
        IList<string>? roles = null,
        bool allowLegacy = true)
    {
        var userManager = CreateMockUserManager(userByEmail, passwordValid, roles);
        var config = CreateConfig(allowLegacy);
        var logger = new Mock<ILogger<AuthController>>().Object;
        var tokenClaims = CreateTokenClaimsMock();
        return new AuthController(userManager, config, logger, tokenClaims.Object);
    }

    private static ApplicationUser ActiveUser(string role = "Cashier") => new()
    {
        Id = "u1",
        UserName = "test@test.com",
        Email = "test@test.com",
        FirstName = "Test",
        LastName = "User",
        IsActive = true,
        Role = role,
    };

    #endregion

    // -------- Existing behaviour tests (updated for new signature) --------

    [Fact]
    public async Task Login_WhenUserDeactivated_ReturnsBadRequest()
    {
        var user = ActiveUser();
        user.IsActive = false;
        var controller = CreateController(user, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = user.Email!, Password = "any" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenUserNotFound_ReturnsBadRequest()
    {
        var controller = CreateController(null, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = "nobody@test.com", Password = "any" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // -------- clientApp field validation --------

    [Fact]
    public async Task Login_WithoutClientApp_WhenLegacyDisabled_ReturnsBadRequest()
    {
        var controller = CreateController(ActiveUser(), roles: new List<string> { "Cashier" }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("clientApp", bad.Value!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithoutClientApp_WhenLegacyEnabled_Succeeds()
    {
        var controller = CreateController(ActiveUser(), roles: new List<string> { "Cashier" }, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithUnknownClientApp_ReturnsBadRequest()
    {
        var controller = CreateController(ActiveUser(), roles: new List<string> { "Cashier" }, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "mobile" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Unknown clientApp", bad.Value!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    // -------- POS policy tests --------

    [Theory]
    [InlineData("Cashier")]
    [InlineData("SuperAdmin")]
    public async Task Login_Pos_AllowedRoles_Succeeds(string role)
    {
        var controller = CreateController(ActiveUser(role), roles: new List<string> { role }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "pos" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Accountant")]
    [InlineData("ReportViewer")]
    [InlineData("Waiter")]
    [InlineData("Kitchen")]
    public async Task Login_Pos_DeniedRoles_Returns403(string role)
    {
        var controller = CreateController(ActiveUser(role), roles: new List<string> { role }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "pos" });

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

    // -------- Admin policy tests --------

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Manager")]
    [InlineData("Accountant")]
    [InlineData("ReportViewer")]
    public async Task Login_Admin_AllowedRoles_Succeeds(string role)
    {
        var controller = CreateController(ActiveUser(role), roles: new List<string> { role }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "admin" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("Waiter")]
    [InlineData("Kitchen")]
    public async Task Login_Admin_DeniedRoles_Returns403(string role)
    {
        var controller = CreateController(ActiveUser(role), roles: new List<string> { role }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "admin" });

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

    // -------- Legacy Admin alias test --------

    [Fact]
    public async Task Login_Pos_LegacyAdminRole_MapsToSuperAdmin_Succeeds()
    {
        var controller = CreateController(ActiveUser("Admin"), roles: new List<string> { "Admin" }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "pos" });

        Assert.IsType<OkObjectResult>(result);
    }
}

// -------- ClientAppPolicy unit tests --------

public class ClientAppPolicyTests
{
    [Theory]
    [InlineData("pos")]
    [InlineData("admin")]
    [InlineData("POS")]
    [InlineData("Admin")]
    public void IsKnownApp_ValidValues_ReturnsTrue(string app)
    {
        Assert.True(ClientAppPolicy.IsKnownApp(app));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mobile")]
    [InlineData("unknown")]
    public void IsKnownApp_InvalidValues_ReturnsFalse(string? app)
    {
        Assert.False(ClientAppPolicy.IsKnownApp(app));
    }

    [Theory]
    [InlineData("pos", "Cashier", true)]
    [InlineData("pos", "SuperAdmin", true)]
    [InlineData("pos", "Admin", true)]       // Legacy alias → SuperAdmin
    [InlineData("pos", "Manager", false)]
    [InlineData("pos", "ReportViewer", false)]
    [InlineData("pos", "Accountant", false)]
    [InlineData("admin", "SuperAdmin", true)]
    [InlineData("admin", "Manager", true)]
    [InlineData("admin", "Accountant", true)]
    [InlineData("admin", "ReportViewer", true)]
    [InlineData("admin", "Cashier", false)]
    [InlineData("admin", "Waiter", false)]
    public void IsRoleAllowedForApp_ReturnsExpected(string app, string role, bool expected)
    {
        Assert.Equal(expected, ClientAppPolicy.IsRoleAllowedForApp(app, role));
    }

    [Fact]
    public void IsRoleAllowedForApp_UnknownApp_ReturnsFalse()
    {
        Assert.False(ClientAppPolicy.IsRoleAllowedForApp("mobile", "SuperAdmin"));
    }
}

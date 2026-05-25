using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

        if (userByEmail != null)
            mgr.Setup(m => m.FindByIdAsync(userByEmail.Id)).ReturnsAsync(userByEmail);

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

    private static Mock<IAuthTenantSnapshotProvider> CreateAuthTenantSnapshotMock()
    {
        var mock = new Mock<IAuthTenantSnapshotProvider>();
        var snapshot = new AuthTenantSnapshot(
            LegacyDefaultTenantIds.Primary.ToString("D"),
            "Default",
            LegacyDefaultTenantIds.PrimarySlug,
            null,
            null);
        mock.Setup(p => p.GetSnapshotAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        mock.Setup(p => p.ResolveForTokenIssuanceAsync(It.IsAny<string?>(), It.IsAny<System.Security.Claims.ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        return mock;
    }

    private static Mock<ILoginTenantResolver> CreateLoginTenantResolverMock(AuthTenantSnapshot? snapshot = null)
    {
        var snap = snapshot ?? new AuthTenantSnapshot(
            LegacyDefaultTenantIds.Primary.ToString("D"),
            "Default",
            LegacyDefaultTenantIds.PrimarySlug,
            null,
            null);
        var mock = new Mock<ILoginTenantResolver>();
        mock.Setup(p => p.ResolveSnapshotForLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snap);
        mock.Setup(p => p.HasActiveMembershipAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Mock<IUserTenantMembershipProvisioner> CreateMembershipProvisionerMock()
    {
        var m = new Mock<IUserTenantMembershipProvisioner>();
        m.Setup(x => x.ProvisionActiveMembershipAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static AuthController CreateController(
        ApplicationUser? userByEmail,
        bool passwordValid = true,
        IList<string>? roles = null,
        bool allowLegacy = true,
        Mock<IRolePermissionResolver>? rolePermissionResolverMock = null,
        Mock<IAuthTenantSnapshotProvider>? authTenantSnapshotMock = null,
        Mock<ILoginTenantResolver>? loginTenantResolverMock = null,
        bool requireTenantMembershipForLogin = false,
        Mock<IUserTenantMembershipProvisioner>? tenantMembershipProvisionerMock = null)
    {
        var userManager = CreateMockUserManager(userByEmail, passwordValid, roles);
        var config = CreateConfig(allowLegacy);
        var logger = new Mock<ILogger<AuthController>>().Object;
        var tokenClaims = CreateTokenClaimsMock();
        var rolePermissionResolver = rolePermissionResolverMock ?? CreateRolePermissionResolverMock();
        var authOptions = Options.Create(new AuthOptions
        {
            AllowLegacyLoginWithoutClientApp = allowLegacy,
            RequireTenantMembershipForLogin = requireTenantMembershipForLogin,
        });
        var refreshTokenService = new Mock<IRefreshTokenService>();
        refreshTokenService.Setup(x => x.IssueLoginTokensAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string, Guid, DateTime, string, string?, Task<string>>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (string uid, string app, Func<string, string, Guid, DateTime, string, string?, Task<string>> factory, Guid? _, CancellationToken _) =>
                {
                    return InvokeIssueLoginFactoryAsync(uid, app, factory);
                });
        var authTenant = authTenantSnapshotMock ?? CreateAuthTenantSnapshotMock();
        var loginTenant = loginTenantResolverMock ?? CreateLoginTenantResolverMock();
        var authService = CreateAuthServiceMock(loginTenant);
        var provisioner = tenantMembershipProvisionerMock ?? CreateMembershipProvisionerMock();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuthCtl_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var appDb = new AppDbContext(dbOptions);
        return new AuthController(
            appDb,
            userManager,
            config,
            logger,
            tokenClaims.Object,
            rolePermissionResolver.Object,
            authOptions,
            refreshTokenService.Object,
            authTenant.Object,
            loginTenant.Object,
            authService.Object,
            provisioner.Object);
    }

    private static Mock<IAuthService> CreateAuthServiceMock(Mock<ILoginTenantResolver> loginTenantResolver)
    {
        var mock = new Mock<IAuthService>();
        mock.Setup(s => s.ResolveLoginTenantAccessAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async (string userId, bool _, CancellationToken ct) =>
            {
                var snapshot = await loginTenantResolver.Object.ResolveSnapshotForLoginAsync(userId, ct);
                return LoginTenantAccessResult.Ok(snapshot);
            });
        return mock;
    }

    /// <summary>Default: same effective set as pre-alignment matrix-only JSON (system/custom via resolver in production).</summary>
    private static Mock<IRolePermissionResolver> CreateRolePermissionResolverMock()
    {
        var mock = new Mock<IRolePermissionResolver>();
        mock.Setup(r => r.GetPermissionsForRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> roleNames, CancellationToken _) =>
            {
                var set = RolePermissionMatrix.GetPermissionsForRoles(roleNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return Task.FromResult<IReadOnlySet<string>>(set);
            });
        return mock;
    }

    private static async Task<IssuedTokenPair> InvokeIssueLoginFactoryAsync(
        string userId,
        string clientApp,
        Func<string, string, Guid, DateTime, string, string?, Task<string>> factory)
    {
        _ = await factory(userId, "test-jti", Guid.NewGuid(), DateTime.UtcNow.AddMinutes(15), clientApp, null);
        return new IssuedTokenPair(
            "access",
            DateTime.UtcNow.AddMinutes(15),
            "refresh",
            DateTime.UtcNow.AddDays(14),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"));
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

    /// <summary>Admin is not a canonical role; POS login with role "Admin" returns 403. Canonical top admin is SuperAdmin only.</summary>
    [Fact]
    public async Task Login_Pos_AdminRole_NoLongerNormalized_Returns403()
    {
        var controller = CreateController(ActiveUser("Admin"), roles: new List<string> { "Admin" }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "pos" });

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, ((ObjectResult)result).StatusCode);
    }

    [Fact]
    public async Task Login_UserPayload_PermissionsComeFromRolePermissionResolver()
    {
        var resolverMock = new Mock<IRolePermissionResolver>();
        resolverMock.Setup(r => r.GetPermissionsForRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "resolver.contract.test" });

        var controller = CreateController(
            ActiveUser(),
            passwordValid: true,
            roles: new List<string> { "Cashier" },
            allowLegacy: true,
            rolePermissionResolverMock: resolverMock);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("resolver.contract.test", json, StringComparison.Ordinal);
        Assert.Contains(LegacyDefaultTenantIds.Primary.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"tenantDisplayName\":\"Default\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCurrentUser_Returns_Tenant_Fields_From_Snapshot()
    {
        var user = ActiveUser();
        var tenantMock = new Mock<IAuthTenantSnapshotProvider>();
        tenantMock.Setup(p => p.GetSnapshotAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(LegacyDefaultTenantIds.Primary.ToString("D"), "Acme", LegacyDefaultTenantIds.PrimarySlug, null, null));

        var controller = CreateController(user, roles: new List<string> { "Cashier" }, allowLegacy: true, authTenantSnapshotMock: tenantMock);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id) },
                "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("acme", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LegacyDefaultTenantIds.Primary.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentUser_Resolves_UserId_From_Sub_When_NameIdentifier_Absent()
    {
        var user = ActiveUser();
        var tenantMock = new Mock<IAuthTenantSnapshotProvider>();
        tenantMock.Setup(p => p.GetSnapshotAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(LegacyDefaultTenantIds.Primary.ToString("D"), "Acme", LegacyDefaultTenantIds.PrimarySlug, null, null));

        var controller = CreateController(user, roles: new List<string> { "Cashier" }, allowLegacy: true, authTenantSnapshotMock: tenantMock);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, user.Id) },
                "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains(user.Id, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCurrentUser_Resolves_UserId_From_UserId_Claim_Only()
    {
        var user = ActiveUser();
        var tenantMock = new Mock<IAuthTenantSnapshotProvider>();
        tenantMock.Setup(p => p.GetSnapshotAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(LegacyDefaultTenantIds.Primary.ToString("D"), "Acme", LegacyDefaultTenantIds.PrimarySlug, null, null));

        var controller = CreateController(user, roles: new List<string> { "Cashier" }, allowLegacy: true, authTenantSnapshotMock: tenantMock);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("userId", user.Id) },
                "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains(user.Id, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_RequireTenantMembershipForLogin_ReturnsBadRequest_When_No_Membership()
    {
        var loginMock = new Mock<ILoginTenantResolver>();
        loginMock.Setup(p => p.HasActiveMembershipAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateController(
            ActiveUser(),
            roles: new List<string> { "Cashier" },
            allowLegacy: true,
            loginTenantResolverMock: loginMock,
            requireTenantMembershipForLogin: true);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "pos" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(bad.Value);
        Assert.Contains("TENANT_MEMBERSHIP_REQUIRED", json, StringComparison.Ordinal);
        Assert.Contains("Kein Zugriff auf diesen Mandanten", json, StringComparison.Ordinal);
        loginMock.Verify(p => p.ResolveSnapshotForLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
    [InlineData("pos", "Admin", false)]
    [InlineData("pos", "Manager", false)]
    [InlineData("pos", "ReportViewer", false)]
    [InlineData("pos", "Accountant", false)]
    [InlineData("admin", "SuperAdmin", true)]
    [InlineData("admin", "Manager", true)]
    [InlineData("admin", "Accountant", true)]
    [InlineData("admin", "ReportViewer", true)]
    [InlineData("admin", "Admin", false)]
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

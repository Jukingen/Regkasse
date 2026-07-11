using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Services.Email;
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
        IList<string>? roles = null,
        ApplicationUser? userByName = null)
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
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, options, hasher.Object, userValidators, passwordValidators,
            keyNormalizer, errors, services, logger);

        mgr.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(userByEmail);

        mgr.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(userByName);

        var loginUsers = new List<ApplicationUser>();
        void TrackUser(ApplicationUser? u)
        {
            if (u == null) return;
            if (string.IsNullOrEmpty(u.NormalizedUserName) && !string.IsNullOrEmpty(u.UserName))
                u.NormalizedUserName = keyNormalizer.NormalizeName(u.UserName);
            if (string.IsNullOrEmpty(u.NormalizedEmail) && !string.IsNullOrEmpty(u.Email))
                u.NormalizedEmail = keyNormalizer.NormalizeEmail(u.Email);
            if (!loginUsers.Any(x => x.Id == u.Id))
                loginUsers.Add(u);
        }

        TrackUser(userByEmail);
        TrackUser(userByName);
        mgr.SetupGet(m => m.Users).Returns(loginUsers.AsQueryable());
        mgr.Setup(m => m.NormalizeName(It.IsAny<string>()))
            .Returns((string? name) => name == null ? null : keyNormalizer.NormalizeName(name));

        mgr.Setup(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(passwordValid);

        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(roles ?? new List<string>());

        mgr.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        mgr.Setup(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("reset-token");

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

    private static UserManager<ApplicationUser> CreateIdentityUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var userManager = new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        var dataProtection = DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(Path.GetTempPath(), "regkasse-auth-controller-tests")));
        userManager.RegisterTokenProvider(
            TokenOptions.DefaultProvider,
            new DataProtectorTokenProvider<ApplicationUser>(
                dataProtection,
                Options.Create(new DataProtectionTokenProviderOptions()),
                Mock.Of<ILogger<DataProtectorTokenProvider<ApplicationUser>>>()));

        return userManager;
    }

    private static async Task<AuthController> CreateControllerWithIdentityUserAsync(
        ApplicationUser user,
        string password = "pass",
        IList<string>? roles = null,
        bool allowLegacy = true)
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuthIdentity_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, NullCurrentTenantAccessor.Instance);
        var userManager = CreateIdentityUserManager(db);

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp ??= Guid.NewGuid().ToString("D");
        user.ConcurrencyStamp ??= Guid.NewGuid().ToString("D");
        if (!string.IsNullOrEmpty(user.UserName))
            user.NormalizedUserName ??= userManager.NormalizeName(user.UserName);
        if (!string.IsNullOrEmpty(user.Email))
            user.NormalizedEmail ??= userManager.NormalizeEmail(user.Email);

        db.Users.Add(user);
        foreach (var roleName in roles ?? new List<string> { user.Role ?? "Cashier" })
        {
            var normalizedRole = roleName.ToUpperInvariant();
            var role = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalizedRole);
            if (role == null)
            {
                role = new IdentityRole
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Name = roleName,
                    NormalizedName = normalizedRole,
                };
                db.Roles.Add(role);
            }

            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
        }

        await db.SaveChangesAsync();

        return CreateController(
            userByEmail: user,
            roles: roles ?? new List<string> { user.Role ?? "Cashier" },
            allowLegacy: allowLegacy,
            userManagerOverride: userManager,
            appDbOverride: db);
    }

    private static AuthController CreateController(
        ApplicationUser? userByEmail,
        bool passwordValid = true,
        IList<string>? roles = null,
        bool allowLegacy = true,
        Mock<IEffectivePermissionResolver>? effectivePermissionResolverMock = null,
        Mock<IAuthTenantSnapshotProvider>? authTenantSnapshotMock = null,
        Mock<ILoginTenantResolver>? loginTenantResolverMock = null,
        bool requireTenantMembershipForLogin = false,
        Mock<IUserTenantMembershipProvisioner>? tenantMembershipProvisionerMock = null,
        ApplicationUser? userByName = null,
        UserManager<ApplicationUser>? userManagerOverride = null,
        AppDbContext? appDbOverride = null,
        Mock<ISessionService>? sessionServiceMock = null)
    {
        var userManager = userManagerOverride
            ?? CreateMockUserManager(userByEmail, passwordValid, roles, userByName);
        var config = CreateConfig(allowLegacy);
        var logger = new Mock<ILogger<AuthController>>().Object;
        var tokenClaims = CreateTokenClaimsMock();
        var effectivePermissionResolver = effectivePermissionResolverMock ?? CreateEffectivePermissionResolverMock();
        var sessionPolicy = new Mock<ITenantSessionPolicyService>();
        sessionPolicy.Setup(s => s.GetPolicyAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSessionPolicyDto());
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
                It.IsAny<SessionClientMetadata?>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (string uid, string app, Func<string, string, Guid, DateTime, string, string?, Task<string>> factory, Guid? _, SessionClientMetadata? __, CancellationToken ___) =>
                {
                    return InvokeIssueLoginFactoryAsync(uid, app, factory);
                });
        var authTenant = authTenantSnapshotMock ?? CreateAuthTenantSnapshotMock();
        var loginTenant = loginTenantResolverMock ?? CreateLoginTenantResolverMock();
        var authService = CreateAuthServiceMock(loginTenant);
        var provisioner = tenantMembershipProvisionerMock ?? CreateMembershipProvisionerMock();
        var appDb = appDbOverride ?? new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"AuthCtl_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            NullCurrentTenantAccessor.Instance);
        var forgotUsernameEmail = CreateForgotUsernameEmailMock();
        var forgotPasswordEmail = CreateForgotPasswordEmailMock();

        var sessionService = sessionServiceMock ?? new Mock<ISessionService>();

        var controller = new AuthController(
            appDb,
            userManager,
            config,
            logger,
            tokenClaims.Object,
            effectivePermissionResolver.Object,
            authOptions,
            refreshTokenService.Object,
            authTenant.Object,
            loginTenant.Object,
            authService.Object,
            provisioner.Object,
            forgotUsernameEmail.Object,
            forgotPasswordEmail.Object,
            sessionPolicy.Object,
            sessionService.Object,
            LocalizationTestDoubles.ApiMessageLocalizer(),
            LocalizationTestDoubles.I18nErrorService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };
        return controller;
    }

    private static Mock<IForgotUsernameEmailService> CreateForgotUsernameEmailMock()
    {
        var mock = new Mock<IForgotUsernameEmailService>();
        mock.Setup(x => x.IsConfigured).Returns(true);
        mock.Setup(x => x.TrySendForgotUsernameAsync(
                It.IsAny<ForgotUsernameEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Mock<IForgotPasswordEmailService> CreateForgotPasswordEmailMock()
    {
        var mock = new Mock<IForgotPasswordEmailService>();
        mock.Setup(x => x.IsConfigured).Returns(true);
        mock.Setup(x => x.TrySendForgotPasswordAsync(
                It.IsAny<ForgotPasswordEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
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
    private static Mock<IEffectivePermissionResolver> CreateEffectivePermissionResolverMock()
    {
        var mock = new Mock<IEffectivePermissionResolver>();
        mock.Setup(r => r.GetEffectivePermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IEnumerable<string> roleNames, Guid? _, CancellationToken __) =>
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
    public async Task Login_WhenUserDeactivated_ReturnsUnauthorized_WithGenericCredentialsMessage()
    {
        var user = ActiveUser();
        user.IsActive = false;
        var controller = CreateController(user, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = user.Email!, Password = "any" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("INVALID_CREDENTIALS", body.Code);
        Assert.Equal("Ungültiger Benutzername oder Passwort", body.Message);
    }

    [Fact]
    public async Task Login_WhenUserNotFound_ReturnsUnauthorized_WithGenericCredentialsMessage()
    {
        var controller = CreateController(null, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = "nobody@test.com", Password = "any" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("INVALID_CREDENTIALS", body.Code);
        Assert.Equal("Ungültiger Benutzername oder Passwort", body.Message);
    }

    [Fact]
    public async Task Login_WhenPasswordInvalid_ReturnsUnauthorized_WithGenericCredentialsMessage()
    {
        var user = ActiveUser();
        var controller = CreateController(user, passwordValid: false, allowLegacy: true);

        var result = await controller.Login(new LoginModel { Email = user.Email, Password = "wrong" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("INVALID_CREDENTIALS", body.Code);
        Assert.Equal("Ungültiger Benutzername oder Passwort", body.Message);
    }

    [Fact]
    public async Task Login_CredentialFailures_ReturnSameCodeAndMessage_ToPreventEnumeration()
    {
        var inactiveUser = ActiveUser();
        inactiveUser.IsActive = false;
        var wrongPasswordUser = ActiveUser();

        var notFoundResult = await CreateController(null, allowLegacy: true)
            .Login(new LoginModel { Email = "nobody@test.com", Password = "any" });
        var inactiveResult = await CreateController(inactiveUser, allowLegacy: true)
            .Login(new LoginModel { Email = inactiveUser.Email!, Password = "any" });
        var wrongPasswordResult = await CreateController(wrongPasswordUser, passwordValid: false, allowLegacy: true)
            .Login(new LoginModel { Email = wrongPasswordUser.Email, Password = "wrong" });

        var notFound = Assert.IsType<ApiErrorResponse>(Assert.IsType<UnauthorizedObjectResult>(notFoundResult).Value);
        var inactive = Assert.IsType<ApiErrorResponse>(Assert.IsType<UnauthorizedObjectResult>(inactiveResult).Value);
        var wrongPassword = Assert.IsType<ApiErrorResponse>(Assert.IsType<UnauthorizedObjectResult>(wrongPasswordResult).Value);

        Assert.Equal(notFound.Code, inactive.Code);
        Assert.Equal(notFound.Message, inactive.Message);
        Assert.Equal(notFound.Code, wrongPassword.Code);
        Assert.Equal(notFound.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task Login_WithLoginIdentifierUsername_Succeeds()
    {
        var user = ActiveUser();
        user.UserName = "cashier1";
        user.Email = "cashier_a3f9k2@dev.regkasse.at";
        var controller = await CreateControllerWithIdentityUserAsync(user, allowLegacy: true);

        var result = await controller.Login(new LoginModel
        {
            LoginIdentifier = "cashier1",
            Password = "pass",
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithLoginIdentifierUsername_ClientAppPos_Succeeds()
    {
        var user = ActiveUser();
        user.UserName = "cashier1";
        user.Email = "cashier_a3f9k2@dev.regkasse.at";
        var controller = await CreateControllerWithIdentityUserAsync(
            user,
            allowLegacy: false,
            roles: new List<string> { "Cashier" });

        var result = await controller.Login(new LoginModel
        {
            LoginIdentifier = "cashier1",
            Password = "pass",
            ClientApp = "pos",
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithLoginIdentifierUsername_DifferentCasing_Succeeds()
    {
        var user = ActiveUser();
        user.UserName = "Mustafa";
        user.Email = "mustafa_a3f9k2@dev.regkasse.at";
        user.NormalizedUserName = "MUSTAFA";
        var controller = await CreateControllerWithIdentityUserAsync(
            user,
            allowLegacy: false,
            roles: new List<string> { "Cashier" });

        var result = await controller.Login(new LoginModel
        {
            LoginIdentifier = "mustafa",
            Password = "pass",
            ClientApp = "pos",
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithLegacyEmailField_StillSucceeds()
    {
        var user = ActiveUser();
        var controller = CreateController(user, allowLegacy: true);

        var result = await controller.Login(new LoginModel
        {
            Email = user.Email,
            Password = "pass",
        });

        Assert.IsType<OkObjectResult>(result);
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
    [InlineData("Cashier")]
    [InlineData("Accountant")]
    [InlineData("ReportViewer")]
    public async Task Login_Admin_AllowedRoles_Succeeds(string role)
    {
        var controller = CreateController(ActiveUser(role), roles: new List<string> { role }, allowLegacy: false);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass", ClientApp = "admin" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
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
        var resolverMock = new Mock<IEffectivePermissionResolver>();
        resolverMock.Setup(r => r.GetEffectivePermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "resolver.contract.test" });

        var controller = CreateController(
            ActiveUser(),
            passwordValid: true,
            roles: new List<string> { "Cashier" },
            allowLegacy: true,
            effectivePermissionResolverMock: resolverMock);

        var result = await controller.Login(new LoginModel { Email = "test@test.com", Password = "pass" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("resolver.contract.test", json, StringComparison.Ordinal);
        Assert.Contains(LegacyDefaultTenantIds.Primary.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"tenantDisplayName\":\"Default\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_Admin_Cashier_ReturnsViewOnlyPermissions()
    {
        var user = ActiveUser(Roles.Cashier);
        var controller = CreateController(user, roles: new List<string> { Roles.Cashier }, allowLegacy: false);

        var result = await controller.Login(new LoginModel
        {
            Email = user.Email!,
            Password = "pass",
            ClientApp = ClientAppPolicy.Admin,
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var permissions = ExtractLoginPermissions(ok.Value);

        Assert.Contains(AppPermissions.ProductView, permissions);
        Assert.Contains(AppPermissions.ReportView, permissions);
        Assert.DoesNotContain(AppPermissions.TableView, permissions);
        Assert.DoesNotContain(AppPermissions.SaleView, permissions);
        Assert.DoesNotContain(AppPermissions.TseSign, permissions);
        Assert.DoesNotContain(AppPermissions.PaymentTake, permissions);
    }

    [Fact]
    public async Task Login_Admin_Manager_StripsPosTerminalPermissions()
    {
        var user = ActiveUser(Roles.Manager);
        var controller = CreateController(user, roles: new List<string> { Roles.Manager }, allowLegacy: false);

        var result = await controller.Login(new LoginModel
        {
            Email = user.Email!,
            Password = "pass",
            ClientApp = ClientAppPolicy.Admin,
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var permissions = ExtractLoginPermissions(ok.Value);

        Assert.Contains(AppPermissions.UserView, permissions);
        Assert.Contains(AppPermissions.CashRegisterManage, permissions);
        Assert.Contains(AppPermissions.PaymentView, permissions);
        Assert.Contains(AppPermissions.SaleView, permissions);
        Assert.Contains(AppPermissions.ReportExport, permissions);
        Assert.Contains(AppPermissions.AuditView, permissions);
        Assert.DoesNotContain(AppPermissions.PaymentTake, permissions);
        Assert.DoesNotContain(AppPermissions.TseSign, permissions);
    }

    [Fact]
    public async Task Login_Pos_Cashier_ReturnsFullMatrixPermissions()
    {
        var user = ActiveUser(Roles.Cashier);
        var controller = CreateController(user, roles: new List<string> { Roles.Cashier }, allowLegacy: false);

        var result = await controller.Login(new LoginModel
        {
            Email = user.Email!,
            Password = "pass",
            ClientApp = ClientAppPolicy.Pos,
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var permissions = ExtractLoginPermissions(ok.Value);

        Assert.Contains(AppPermissions.TableView, permissions);
        Assert.Contains(AppPermissions.PaymentTake, permissions);
    }

    [Fact]
    public async Task GetCurrentUser_AdminContext_ReturnsFilteredPermissions()
    {
        var user = ActiveUser(Roles.Cashier);
        var controller = CreateController(user, roles: new List<string> { Roles.Cashier }, allowLegacy: false);

        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClientAppPolicy.AppContextClaimType, ClientAppPolicy.Admin),
                },
                "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.GetCurrentUser();

        var ok = Assert.IsType<OkObjectResult>(result);
        var permissions = ExtractMePermissions(ok.Value);

        Assert.Contains(AppPermissions.PaymentView, permissions);
        Assert.DoesNotContain(AppPermissions.ShiftView, permissions);
    }

    private static List<string> ExtractLoginPermissions(object payload)
    {
        var json = JsonSerializer.Serialize(payload, AdminPermissionJsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ReadPermissionStrings(doc.RootElement.GetProperty("user").GetProperty("permissions"));
    }

    private static List<string> ExtractMePermissions(object payload)
    {
        var json = JsonSerializer.Serialize(payload, AdminPermissionJsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ReadPermissionStrings(doc.RootElement.GetProperty("permissions"));
    }

    private static List<string> ReadPermissionStrings(JsonElement permissionsElement)
    {
        var list = new List<string>();
        foreach (var item in permissionsElement.EnumerateArray())
            list.Add(item.GetString()!);
        return list;
    }

    private static readonly JsonSerializerOptions AdminPermissionJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

    [Fact]
    public async Task ForgotUsername_WhenUserExists_Sends_Email_And_Returns_Generic_Message()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "user@test.com",
            UserName = "cashier1",
            IsActive = true,
        };
        var forgotEmail = new Mock<IForgotUsernameEmailService>();
        forgotEmail.Setup(x => x.IsConfigured).Returns(true);
        forgotEmail.Setup(x => x.TrySendForgotUsernameAsync(
                It.IsAny<ForgotUsernameEmailRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateForgotUsernameController(user, forgotEmail);

        var result = await controller.ForgotUsername(
            new ForgotUsernameRequest { Email = "user@test.com", ClientApp = "admin" });

        var ok = Assert.IsType<OkObjectResult>(result);
        forgotEmail.Verify(
            x => x.TrySendForgotUsernameAsync(
                It.Is<ForgotUsernameEmailRequest>(r =>
                    r.ToEmail == "user@test.com"
                    && r.Usernames.Count == 1
                    && r.Usernames[0] == "cashier1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task ForgotUsername_WhenUserMissing_Still_Returns_Ok()
    {
        var controller = CreateController(userByEmail: null);

        var result = await controller.ForgotUsername(
            new ForgotUsernameRequest { Email = "missing@test.com", ClientApp = "admin" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ForgotPassword_WhenUserExists_Sends_Email_And_Returns_Generic_Message()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "user@test.com",
            UserName = "user@test.com",
            IsActive = true,
        };
        var forgotPasswordEmail = CreateForgotPasswordEmailMock();
        var userManager = CreateMockUserManager(user);
        var controller = CreateForgotPasswordController(user, userManager, forgotPasswordEmail);

        var result = await controller.ForgotPassword(
            new ForgotPasswordRequest { Email = "user@test.com", ClientApp = "admin" });

        var ok = Assert.IsType<OkObjectResult>(result);
        forgotPasswordEmail.Verify(
            x => x.TrySendForgotPasswordAsync(
                It.Is<ForgotPasswordEmailRequest>(r =>
                    r.ToEmail == "user@test.com"
                    && r.ResetToken == "reset-token"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task ForgotPassword_WhenUserMissing_Still_Returns_Ok_With_Generic_Message()
    {
        var controller = CreateController(userByEmail: null);

        var result = await controller.ForgotPassword(
            new ForgotPasswordRequest { Email = "missing@test.com", ClientApp = "admin" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Wenn ein Konto existiert", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_Returns_Generic_RegistrationFailed_Message()
    {
        var user = ActiveUser();
        var controller = CreateController(user);

        var result = await controller.Register(new RegisterModel
        {
            Email = user.Email!,
            Password = "Password1!",
            FirstName = "Test",
            LastName = "User",
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(bad.Value);
        Assert.Contains("REGISTRATION_FAILED", json, StringComparison.Ordinal);
        Assert.Contains("Registrierung fehlgeschlagen", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Duplicate", json, StringComparison.OrdinalIgnoreCase);
    }

    private static AuthController CreateForgotPasswordController(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        Mock<IForgotPasswordEmailService>? forgotPasswordEmail = null)
    {
        forgotPasswordEmail ??= CreateForgotPasswordEmailMock();
        var sessionPolicy = new Mock<ITenantSessionPolicyService>();
        sessionPolicy.Setup(s => s.GetPolicyAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSessionPolicyDto());

        return new AuthController(
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase($"AuthForgotPwd_{Guid.NewGuid():N}")
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .Options,
                NullCurrentTenantAccessor.Instance),
            userManager,
            CreateConfig(),
            new Mock<ILogger<AuthController>>().Object,
            CreateTokenClaimsMock().Object,
            CreateEffectivePermissionResolverMock().Object,
            Options.Create(new AuthOptions()),
            new Mock<IRefreshTokenService>().Object,
            CreateAuthTenantSnapshotMock().Object,
            CreateLoginTenantResolverMock().Object,
            CreateAuthServiceMock(CreateLoginTenantResolverMock()).Object,
            CreateMembershipProvisionerMock().Object,
            CreateForgotUsernameEmailMock().Object,
            forgotPasswordEmail.Object,
            sessionPolicy.Object,
            new Mock<ISessionService>().Object,
            LocalizationTestDoubles.ApiMessageLocalizer(),
            LocalizationTestDoubles.I18nErrorService());
    }

    private static AuthController CreateForgotUsernameController(
        ApplicationUser user,
        Mock<IForgotUsernameEmailService>? forgotUsernameEmail = null)
    {
        forgotUsernameEmail ??= new Mock<IForgotUsernameEmailService>();

        var sessionPolicy = new Mock<ITenantSessionPolicyService>();
        sessionPolicy.Setup(s => s.GetPolicyAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSessionPolicyDto());

        return new AuthController(
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase($"AuthForgot_{Guid.NewGuid():N}")
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .Options,
                NullCurrentTenantAccessor.Instance),
            CreateMockUserManager(user),
            CreateConfig(),
            new Mock<ILogger<AuthController>>().Object,
            CreateTokenClaimsMock().Object,
            CreateEffectivePermissionResolverMock().Object,
            Options.Create(new AuthOptions()),
            new Mock<IRefreshTokenService>().Object,
            CreateAuthTenantSnapshotMock().Object,
            CreateLoginTenantResolverMock().Object,
            CreateAuthServiceMock(CreateLoginTenantResolverMock()).Object,
            CreateMembershipProvisionerMock().Object,
            forgotUsernameEmail.Object,
            CreateForgotPasswordEmailMock().Object,
            sessionPolicy.Object,
            new Mock<ISessionService>().Object,
            LocalizationTestDoubles.ApiMessageLocalizer(),
            LocalizationTestDoubles.I18nErrorService());
    }

    [Fact]
    public async Task RefreshSession_TouchesActivity_WhenSidClaimPresent()
    {
        var sessionId = Guid.NewGuid();
        var sessionService = new Mock<ISessionService>();
        sessionService
            .Setup(s => s.TouchSessionActivityAsync(sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(userByEmail: null, sessionServiceMock: sessionService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        new Claim("sid", sessionId.ToString()),
                    },
                    authenticationType: "Test")),
            },
        };

        var result = await controller.RefreshSession(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        sessionService.Verify(
            s => s.TouchSessionActivityAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
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
    [InlineData("admin", "Cashier", true)]
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

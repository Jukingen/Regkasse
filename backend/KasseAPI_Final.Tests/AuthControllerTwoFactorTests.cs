using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Token;
using KasseAPI_Final.Services.TwoFactor;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>SuperAdmin 2FA gate: Development bypass vs Production challenge.</summary>
public sealed class AuthControllerTwoFactorTests
{
    [Fact]
    public async Task Login_SuperAdmin_Development_BypassesTwoFactor_AndIssuesTokens()
    {
        var user = SuperAdminUser();
        var controller = CreateController(
            user,
            roles: new List<string> { Roles.SuperAdmin },
            requireTwoFactor: null,
            environmentName: Environments.Development);

        var result = await controller.Login(new LoginModel
        {
            LoginIdentifier = user.Email,
            Password = "Password1!",
            ClientApp = "admin",
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(doc.RootElement.TryGetProperty("token", out var token));
        Assert.False(string.IsNullOrWhiteSpace(token.GetString()));
        Assert.True(doc.RootElement.TryGetProperty("requires2FA", out var requires));
        Assert.False(requires.GetBoolean());
        Assert.True(doc.RootElement.GetProperty("isDevelopment").GetBoolean());
    }

    [Fact]
    public async Task Login_SuperAdmin_Production_ReturnsTwoFactorChallenge_WithoutTokens()
    {
        var user = SuperAdminUser();
        var controller = CreateController(
            user,
            roles: new List<string> { Roles.SuperAdmin },
            requireTwoFactor: null,
            environmentName: Environments.Production,
            twoFactorEnabled: false);

        var result = await controller.Login(new LoginModel
        {
            LoginIdentifier = user.Email,
            Password = "Password1!",
            ClientApp = "admin",
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var challenge = Assert.IsType<LoginTwoFactorChallengeDto>(ok.Value);
        Assert.True(challenge.Requires2FA);
        Assert.True(challenge.Requires2FASetup);
        Assert.False(string.IsNullOrWhiteSpace(challenge.TwoFactorToken));
        Assert.False(string.IsNullOrWhiteSpace(challenge.AuthenticatorKey));
        Assert.Contains("otpauth://totp/", challenge.AuthenticatorUri, StringComparison.Ordinal);
        Assert.False(challenge.IsDevelopment);
    }

    [Fact]
    public async Task Login_Manager_Production_DoesNotRequireTwoFactor()
    {
        var user = new ApplicationUser
        {
            Id = "mgr-1",
            UserName = "manager@test.com",
            Email = "manager@test.com",
            FirstName = "Mgr",
            LastName = "User",
            IsActive = true,
            Role = Roles.Manager,
        };
        var controller = CreateController(
            user,
            roles: new List<string> { Roles.Manager },
            requireTwoFactor: null,
            environmentName: Environments.Production);

        var result = await controller.Login(new LoginModel
        {
            LoginIdentifier = user.Email,
            Password = "Password1!",
            ClientApp = "admin",
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(doc.RootElement.TryGetProperty("token", out _));
        Assert.False(doc.RootElement.GetProperty("requires2FA").GetBoolean());
    }

    [Fact]
    public async Task VerifyTwoFactor_InvalidCode_ReturnsUnauthorized()
    {
        var user = SuperAdminUser();
        var challengeService = new TwoFactorChallengeService(new MemoryCache(new MemoryCacheOptions()));
        var controller = CreateController(
            user,
            roles: new List<string> { Roles.SuperAdmin },
            requireTwoFactor: true,
            environmentName: Environments.Production,
            twoFactorEnabled: true,
            challengeService: challengeService,
            verifyTwoFactorResult: false);

        var token = challengeService.CreateChallenge(new TwoFactorChallengePayload(
            user.Id,
            "admin",
            user.Email!,
            SetupRequired: false,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(5)));

        var result = await controller.VerifyTwoFactor(new VerifyTwoFactorModel
        {
            TwoFactorToken = token,
            Code = "000000",
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("TWO_FACTOR_INVALID", body.Code);
    }

    private static ApplicationUser SuperAdminUser() => new()
    {
        Id = "sa-1",
        UserName = "super@test.com",
        Email = "super@test.com",
        FirstName = "Super",
        LastName = "Admin",
        IsActive = true,
        Role = Roles.SuperAdmin,
    };

    private static AuthController CreateController(
        ApplicationUser user,
        IList<string> roles,
        bool? requireTwoFactor,
        string environmentName,
        bool twoFactorEnabled = false,
        ITwoFactorChallengeService? challengeService = null,
        bool verifyTwoFactorResult = true)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var options = Options.Create(new IdentityOptions());
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();
        hasher.Setup(h => h.VerifyHashedPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            options,
            hasher.Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        mgr.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        mgr.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        mgr.Setup(m => m.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        mgr.SetupGet(m => m.Users).Returns(new[] { user }.AsQueryable());
        mgr.Setup(m => m.NormalizeName(It.IsAny<string>()))
            .Returns((string? n) => n?.ToUpperInvariant());
        mgr.Setup(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(true);
        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(roles);
        mgr.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        mgr.Setup(m => m.GetTwoFactorEnabledAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(twoFactorEnabled);
        mgr.Setup(m => m.ResetAuthenticatorKeyAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        mgr.Setup(m => m.GetAuthenticatorKeyAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("JBSWY3DPEHPK3PXP");
        mgr.Setup(m => m.SetTwoFactorEnabledAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentityResult.Success);
        mgr.Setup(m => m.VerifyTwoFactorTokenAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(verifyTwoFactorResult);

        var usersDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"Auth2fa_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            NullCurrentTenantAccessor.Instance);
        usersDb.Users.Add(user);
        usersDb.SaveChanges();
        mgr.SetupGet(m => m.Users).Returns(usersDb.Users);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "test-secret-key-at-least-32-characters-long!!",
            ["JwtSettings:Issuer"] = "Test",
            ["JwtSettings:Audience"] = "Test",
        }).Build();

        var tokenClaims = new Mock<ITokenClaimsService>();
        tokenClaims.Setup(t => t.BuildClaimsAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<IList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<System.Security.Claims.Claim>());

        var effective = new Mock<IEffectivePermissionResolver>();
        effective.Setup(r => r.GetEffectivePermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var refresh = new Mock<IRefreshTokenService>();
        refresh.Setup(x => x.IssueLoginTokensAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string, Guid, DateTime, string, string?, Task<string>>>(),
                It.IsAny<Guid?>(),
                It.IsAny<SessionClientMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssuedTokenPair(
                "access-token",
                DateTime.UtcNow.AddMinutes(15),
                "refresh-token",
                DateTime.UtcNow.AddDays(14),
                Guid.NewGuid(),
                Guid.NewGuid().ToString("N")));

        var snapshot = new AuthTenantSnapshot(
            TenantId: LegacyDefaultTenantIds.Primary.ToString(),
            TenantDisplayName: "Dev",
            TenantSlug: "dev",
            BranchId: null,
            BranchDisplayName: null);
        var loginTenant = new Mock<ILoginTenantResolver>();
        loginTenant.Setup(p => p.HasActiveMembershipAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        loginTenant.Setup(p => p.ResolveSnapshotForLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var authService = new Mock<IAuthService>();
        authService.Setup(s => s.ResolveLoginTenantAccessAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LoginTenantAccessResult.Ok(snapshot));

        var authTenant = new Mock<IAuthTenantSnapshotProvider>();
        authTenant.Setup(p => p.ResolveForTokenIssuanceAsync(
                It.IsAny<string?>(),
                It.IsAny<System.Security.Claims.ClaimsPrincipal?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var sessionPolicy = new Mock<ITenantSessionPolicyService>();
        sessionPolicy.Setup(s => s.GetPolicyAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSessionPolicyDto());

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor.SetupGet(s => s.IsDevelopment).Returns(environmentName == Environments.Development);
        twoFactor.SetupGet(s => s.IsBypassActive)
            .Returns(environmentName == Environments.Development && requireTwoFactor != true);
        twoFactor.Setup(s => s.GenerateTwoFactorToken(It.IsAny<ApplicationUser>()))
            .Returns((ApplicationUser _) =>
                environmentName == Environments.Development
                    ? ITwoFactorService.DevelopmentBypassToken
                    : null);
        twoFactor.Setup(s => s.VerifyTwoFactorTokenAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyTwoFactorResult);

        var twoFactorOpts = Options.Create(new TwoFactorAuthOptions
        {
            Enabled = true,
            // When requireTwoFactor is true (tests), disable Dev bypass so challenge is issued.
            BypassInDevelopment = requireTwoFactor != true,
            TestToken = "123456",
        });

        var controller = new AuthController(
            usersDb,
            mgr.Object,
            config,
            new Mock<ILogger<AuthController>>().Object,
            tokenClaims.Object,
            effective.Object,
            Options.Create(new AuthOptions
            {
                AllowLegacyLoginWithoutClientApp = false,
                RequireSuperAdminTwoFactor = requireTwoFactor,
            }),
            refresh.Object,
            authTenant.Object,
            loginTenant.Object,
            authService.Object,
            new Mock<IUserTenantMembershipProvisioner>().Object,
            new Mock<IForgotUsernameEmailService>().Object,
            new Mock<IForgotPasswordEmailService>().Object,
            sessionPolicy.Object,
            new Mock<ISessionService>().Object,
            LocalizationTestDoubles.ApiMessageLocalizer(),
            LocalizationTestDoubles.I18nErrorService(),
            Mock.Of<IPosShiftService>(),
            new AccountLockoutService(
                new MemoryCache(new MemoryCacheOptions()),
                new OptionsMonitorStub(new AccountLockoutOptions { Enabled = false })),
            environment.Object,
            challengeService ?? new TwoFactorChallengeService(new MemoryCache(new MemoryCacheOptions())),
            twoFactor.Object,
            twoFactorOpts,
            Mock.Of<ITokenBlacklistService>(),
            Mock.Of<IAuditLogService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };
        return controller;
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<AccountLockoutOptions>
    {
        public OptionsMonitorStub(AccountLockoutOptions current) => CurrentValue = current;
        public AccountLockoutOptions CurrentValue { get; }
        public AccountLockoutOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AccountLockoutOptions, string?> listener) => null;
    }
}

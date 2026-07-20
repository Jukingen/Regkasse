using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.TwoFactor;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TwoFactorServiceTests
{
    [Fact]
    public void GenerateTwoFactorToken_Development_ReturnsBypass()
    {
        var service = CreateService(Environments.Development, verifyResult: false);
        var user = new ApplicationUser { Id = "u1", Email = "a@test.com" };

        Assert.Equal(ITwoFactorService.DevelopmentBypassToken, service.GenerateTwoFactorToken(user));
    }

    [Fact]
    public void GenerateTwoFactorToken_Production_ReturnsNull()
    {
        var service = CreateService(Environments.Production, verifyResult: false);
        var user = new ApplicationUser { Id = "u1", Email = "a@test.com" };

        Assert.Null(service.GenerateTwoFactorToken(user));
    }

    [Theory]
    [InlineData(ITwoFactorService.DevelopmentBypassToken)]
    [InlineData("123456")]
    public async Task VerifyTwoFactorToken_Development_AcceptsBypassCodes(string code)
    {
        var service = CreateService(Environments.Development, verifyResult: false);
        var user = new ApplicationUser { Id = "u1", Email = "a@test.com" };

        Assert.True(await service.VerifyTwoFactorTokenAsync(user, code));
    }

    [Fact]
    public async Task VerifyTwoFactorToken_Production_DoesNotAcceptBypassCodes()
    {
        var service = CreateService(Environments.Production, verifyResult: false);
        var user = new ApplicationUser { Id = "u1", Email = "a@test.com" };

        Assert.False(await service.VerifyTwoFactorTokenAsync(user, ITwoFactorService.DevelopmentBypassToken));
        Assert.False(await service.VerifyTwoFactorTokenAsync(user, "123456"));
    }

    [Fact]
    public async Task VerifyTwoFactorToken_Production_UsesIdentityTotp()
    {
        var service = CreateService(Environments.Production, verifyResult: true);
        var user = new ApplicationUser { Id = "u1", Email = "a@test.com" };

        Assert.True(await service.VerifyTwoFactorTokenAsync(user, "654321"));
    }

    [Fact]
    public void IsBypassActive_Development_WithBypassFlag()
    {
        var service = CreateService(Environments.Development, verifyResult: false, bypassInDevelopment: true);
        Assert.True(service.IsBypassActive);
    }

    [Fact]
    public void IsBypassActive_Production_AlwaysFalse()
    {
        var service = CreateService(Environments.Production, verifyResult: false, bypassInDevelopment: true);
        Assert.False(service.IsBypassActive);
    }

    private static TwoFactorService CreateService(
        string environmentName,
        bool verifyResult,
        bool bypassInDevelopment = true,
        string testToken = "123456")
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        userManager.Setup(m => m.VerifyTwoFactorTokenAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(verifyResult);

        var options = new OptionsMonitorStub(new TwoFactorAuthOptions
        {
            Enabled = true,
            BypassInDevelopment = bypassInDevelopment,
            TestToken = testToken,
        });

        return new TwoFactorService(
            environment.Object,
            userManager.Object,
            options,
            new Mock<ILogger<TwoFactorService>>().Object);
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<TwoFactorAuthOptions>
    {
        public OptionsMonitorStub(TwoFactorAuthOptions current) => CurrentValue = current;
        public TwoFactorAuthOptions CurrentValue { get; }
        public TwoFactorAuthOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<TwoFactorAuthOptions, string?> listener) => null;
    }
}

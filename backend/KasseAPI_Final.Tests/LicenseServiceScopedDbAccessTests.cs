using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseServiceScopedDbAccessTests
{
    [Fact]
    public async Task EvaluateOnStartup_And_GetCurrentStatusAsync_ReadActivationRows_WithoutDbContextFactory()
    {
        var machineHash = "machine-hash-123";
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"LicenseScopedDb_{Guid.NewGuid():N}")
                .Options,
            NullCurrentTenantAccessor.Instance);
        db.ActivatedLicenses.Add(new ActivatedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = "REGK-ABCDE-12345-FGHIJ",
            ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            MachineFingerprint = machineHash,
            ActivatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-1),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(AppDbContext)))
            .Returns(db);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var storage = new InMemoryLicenseStorage(machineHash);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var logger = new Mock<ILogger<LicenseService>>();
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(x => x.EnvironmentName).Returns(Environments.Production);
        var developmentOptions = new Mock<IOptionsMonitor<DevelopmentOptions>>();
        developmentOptions.SetupGet(x => x.CurrentValue).Returns(new DevelopmentOptions());
        var developmentMode = new Mock<IDevelopmentModeService>();
        developmentMode.Setup(x => x.ShouldBypassLicense()).Returns(false);
        developmentMode.Setup(x => x.GetValidDays()).Returns(0);
        developmentMode.Setup(x => x.GetFeatures()).Returns([]);

        var service = new LicenseService(
            Options.Create(new LicenseOptions()),
            httpClientFactory.Object,
            storage,
            scopeFactory.Object,
            Mock.Of<IActivityEventPublisher>(),
            logger.Object,
            hostEnvironment.Object,
            developmentOptions.Object,
            developmentMode.Object);

        service.EvaluateOnStartup();
        var startupSnapshot = service.GetStatus();
        var liveSnapshot = await service.GetCurrentStatusAsync();
        var persisted = storage.LoadLicenseFromFile();

        Assert.True(service.IsLicenseSnapshotInitialized);
        Assert.NotNull(persisted);
        Assert.Equal("REGK-ABCDE-12345-FGHIJ", persisted!.LicenseKey);
        Assert.Equal(machineHash, startupSnapshot.MachineHash);
        Assert.Equal(machineHash, liveSnapshot.MachineHash);
    }

    private sealed class InMemoryLicenseStorage(string machineHash) : ILicenseStorageService
    {
        private LicensePersistedState? _state;

        public void SaveLicenseToFile(LicensePersistedState state)
        {
            _state = new LicensePersistedState
            {
                FirstRunUtc = state.FirstRunUtc,
                LicenseKey = state.LicenseKey,
                OfflineJwt = state.OfflineJwt,
                KeyOnlyPaidValidUntilUtc = state.KeyOnlyPaidValidUntilUtc,
                FeaturesJson = state.FeaturesJson,
            };
        }

        public LicensePersistedState? LoadLicenseFromFile() => _state;

        public string LicenseFilePath => "memory://license.dat";

        public string MachineHashHex => machineHash;

        public string MachineFingerprintCanonical => machineHash;
    }
}

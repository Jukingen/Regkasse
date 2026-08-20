using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineRuntimeSettingsServiceTests
{
    [Fact]
    public void GetSettings_UsesConfigWhenNoOverride()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, sim: true, retryEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var settings = svc.GetSettings(canManage: true);

        Assert.True(settings.UseSimulation);
        Assert.True(settings.ConfigUseSimulation);
        Assert.False(settings.RetryJobEnabled);
        Assert.Equal("config", settings.Source);
        Assert.False(settings.IsProduction);
        Assert.True(settings.CanManage);
    }

    [Fact]
    public async Task Update_DevCanDisableSimulationWithoutChangingConfig()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, sim: true, retryEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var updated = await svc.UpdateAsync(
            new UpdateFinanzOnlineRuntimeRequest { UseSimulation = false },
            actorUserId: "sa-1",
            canManage: true);

        Assert.False(updated.UseSimulation);
        Assert.True(updated.ConfigUseSimulation);
        Assert.Equal("global_override", updated.Source);
        Assert.False(cache.GetOverlay()!.UseSimulation);
        Assert.True(cache.GetOverlay()!.IsComplete);

        await using var db = factory.CreateDbContext();
        var row = Assert.Single(db.TenantSettings.Where(s => s.Key == FinanzOnlineRuntimeOverlay.SettingsKey));
        Assert.Null(row.TenantId);
    }

    [Fact]
    public async Task Update_WritesExclusiveSnapshot_LaterConfigChangesDoNotApply()
    {
        var (factory, cache) = CreateStore();
        var initial = CreateService(factory, cache, sim: true, retryEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));
        await initial.UpdateAsync(new UpdateFinanzOnlineRuntimeRequest { UseSimulation = false, RetryJobEnabled = true }, "sa-1", true);

        var later = CreateService(
            factory,
            cache,
            new FinanzOnlineSessionOptions { UseSimulation = true },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = true, EnableRealTestSubmission = true },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = true, EnableRealTestQuery = true },
            new FinanzOnlineRetryJobOptions { Enabled = false, Interval = TimeSpan.FromSeconds(60) },
            TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var settings = later.GetSettings(true);
        Assert.False(settings.UseSimulation);
        Assert.True(settings.ConfigUseSimulation);
        Assert.False(settings.EnableRealTestSubmission);
        Assert.True(settings.ConfigEnableRealTestSubmission);
        Assert.True(settings.RetryJobEnabled);
        Assert.False(settings.ConfigRetryJobEnabled);
        Assert.Equal(120, settings.RetryIntervalSeconds.Effective);
        Assert.Equal(60, settings.RetryIntervalSeconds.Config);
        Assert.Equal("global_override", settings.Source);
    }

    [Fact]
    public async Task Production_CannotEnableSimulation()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, sim: false, retryEnabled: true, TenantTestDoubles.ProductionHostEnvironment);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(
                new UpdateFinanzOnlineRuntimeRequest { UseSimulation = true },
                "sa-1",
                true));

        Assert.Equal(FinanzOnlineRuntimeSettingsService.ProductionSimulationForbiddenCode, ex.Message);
        Assert.False(svc.GetSettings(true).UseSimulation);
    }

    [Fact]
    public async Task Production_CannotEnableRealTest()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, sim: false, retryEnabled: true, TenantTestDoubles.ProductionHostEnvironment);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(
                new UpdateFinanzOnlineRuntimeRequest { EnableRealTestSubmission = true, EnableRealTestQuery = true },
                "sa-1",
                true));

        Assert.Equal(FinanzOnlineRuntimeSettingsService.ProductionRealTestForbiddenCode, ex.Message);
    }

    [Fact]
    public async Task ClearOverride_FallsBackToConfig()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, sim: true, retryEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));
        await svc.UpdateAsync(new UpdateFinanzOnlineRuntimeRequest { UseSimulation = false }, "sa-1", true);

        var cleared = await svc.UpdateAsync(
            new UpdateFinanzOnlineRuntimeRequest { ClearOverride = true },
            "sa-1",
            true);

        Assert.True(cleared.UseSimulation);
        Assert.Equal("config", cleared.Source);
        await using var db = factory.CreateDbContext();
        Assert.Empty(db.TenantSettings.Where(s => s.Key == FinanzOnlineRuntimeOverlay.SettingsKey));
    }

    [Fact]
    public void ProductionAccessor_ForcesSimulationOff()
    {
        var (_, cache) = CreateStore();
        cache.SetOverlay(new FinanzOnlineRuntimeOverlay
        {
            UseSimulation = true,
            EnableRealTestSubmission = true,
            EnableRealTestQuery = true,
            RetryJobEnabled = true,
            RetryIntervalSeconds = 120,
            RetryMaxRetryCount = 5,
            RetryBaseDelaySeconds = 60,
            RetryBackoffCapSeconds = 3600,
            RetryBatchSize = 50,
        });

        var accessor = new FinanzOnlineRuntimeOptionsAccessor(
            Options.Create(new FinanzOnlineSessionOptions { UseSimulation = true }).ToMonitor(),
            Options.Create(new FinanzOnlineRegistrierkassenOptions { UseSimulation = true, EnableRealTestSubmission = true }).ToMonitor(),
            Options.Create(new FinanzOnlineTransmissionQueryOptions { UseSimulation = true, EnableRealTestQuery = true }).ToMonitor(),
            Options.Create(new FinanzOnlineRetryJobOptions { Enabled = true }).ToMonitor(),
            cache,
            TenantTestDoubles.ProductionHostEnvironment);

        Assert.False(accessor.Session.UseSimulation);
        Assert.False(accessor.Registrierkassen.UseSimulation);
        Assert.False(accessor.Registrierkassen.EnableRealTestSubmission);
        Assert.False(accessor.TransmissionQuery.UseSimulation);
        Assert.False(accessor.TransmissionQuery.EnableRealTestQuery);
        Assert.True(accessor.RetryJob.Enabled);
    }

    private static (IDbContextFactory<AppDbContext> Factory, FinanzOnlineRuntimeOverrideCache Cache) CreateStore()
    {
        var dbName = $"FoRuntimeSettings_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        var cache = new FinanzOnlineRuntimeOverrideCache(
            CreateScopeFactory(factory.Object),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinanzOnlineRuntimeOverrideCache>.Instance);
        return (factory.Object, cache);
    }

    private static IServiceScopeFactory CreateScopeFactory(IDbContextFactory<AppDbContext> factory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static FinanzOnlineRuntimeSettingsService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FinanzOnlineRuntimeOverrideCache cache,
        bool sim,
        bool retryEnabled,
        IHostEnvironment hostEnvironment) =>
        CreateService(
            factory,
            cache,
            new FinanzOnlineSessionOptions { UseSimulation = sim },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = sim, EnableRealTestSubmission = false },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = sim, EnableRealTestQuery = false },
            new FinanzOnlineRetryJobOptions { Enabled = retryEnabled, Interval = TimeSpan.FromSeconds(120) },
            hostEnvironment);

    private static FinanzOnlineRuntimeSettingsService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FinanzOnlineRuntimeOverrideCache cache,
        FinanzOnlineSessionOptions session,
        FinanzOnlineRegistrierkassenOptions registrierkassen,
        FinanzOnlineTransmissionQueryOptions transmission,
        FinanzOnlineRetryJobOptions retry,
        IHostEnvironment hostEnvironment)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new FinanzOnlineRuntimeSettingsService(
            Options.Create(session).ToMonitor(),
            Options.Create(registrierkassen).ToMonitor(),
            Options.Create(transmission).ToMonitor(),
            Options.Create(retry).ToMonitor(),
            cache,
            factory,
            audit.Object,
            hostEnvironment,
            NullLogger<FinanzOnlineRuntimeSettingsService>.Instance);
    }
}

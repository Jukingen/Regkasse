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

public sealed class FinanzOnlineOutboxSettingsServiceTests
{
    [Fact]
    public void GetSettings_UsesConfigWhenNoOverride()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var settings = svc.GetSettings(canManage: true);

        Assert.False(settings.Enabled);
        Assert.False(settings.ConfigEnabled);
        Assert.Null(settings.OverrideEnabled);
        Assert.Equal("config", settings.Source);
        Assert.False(settings.IsProduction);
        Assert.True(settings.CanManage);
    }

    [Fact]
    public async Task Update_DevOverlayEnablesWorkerWithoutChangingConfig()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var updated = await svc.UpdateAsync(
            new UpdateFinanzOnlineOutboxWorkerRequest { Enabled = true },
            actorUserId: "sa-1",
            canManage: true);

        Assert.True(updated.Enabled);
        Assert.False(updated.ConfigEnabled);
        Assert.True(updated.OverrideEnabled);
        Assert.Equal("global_override", updated.Source);
        Assert.True(cache.IsEnabled(configEnabled: false));

        await using var db = factory.CreateDbContext();
        var row = Assert.Single(db.TenantSettings.Where(s => s.Key == FinanzOnlineOutboxEnabledOverrideCache.SettingsKey));
        Assert.Null(row.TenantId);
        var parsed = FinanzOnlineOutboxEnabledOverrideCache.Parse(row.Value);
        Assert.NotNull(parsed);
        Assert.True(parsed!.IsComplete);
        Assert.True(parsed.Enabled);
        Assert.Equal(5, parsed.MaxAttempts);
    }

    [Fact]
    public async Task ClearOverride_FallsBackToConfig()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));
        await svc.UpdateAsync(new UpdateFinanzOnlineOutboxWorkerRequest { Enabled = true }, "sa-1", true);

        var cleared = await svc.UpdateAsync(
            new UpdateFinanzOnlineOutboxWorkerRequest { ClearOverride = true },
            "sa-1",
            true);

        Assert.False(cleared.Enabled);
        Assert.Equal("config", cleared.Source);
        Assert.Null(cleared.OverrideEnabled);
        await using var db = factory.CreateDbContext();
        Assert.Empty(db.TenantSettings.Where(s => s.Key == FinanzOnlineOutboxEnabledOverrideCache.SettingsKey));
    }

    [Fact]
    public async Task ProductionDisable_RequiresConfirm()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: true, TenantTestDoubles.ProductionHostEnvironment);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(
                new UpdateFinanzOnlineOutboxWorkerRequest { Enabled = false },
                "sa-1",
                true));

        Assert.Equal(FinanzOnlineOutboxSettingsService.ProductionDisableConfirmRequiredCode, ex.Message);
        Assert.True(svc.GetSettings(true).Enabled);
    }

    [Fact]
    public async Task ProductionDisable_SucceedsWithConfirm()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: true, TenantTestDoubles.ProductionHostEnvironment);

        var updated = await svc.UpdateAsync(
            new UpdateFinanzOnlineOutboxWorkerRequest { Enabled = false, ConfirmProductionDisable = true },
            "sa-1",
            true);

        Assert.False(updated.Enabled);
        Assert.True(updated.ConfigEnabled);
        Assert.False(updated.OverrideEnabled);
        Assert.True(updated.IsProduction);
    }

    [Fact]
    public async Task Update_NumericOverlay_AppliesWithoutChangingConfig()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var updated = await svc.UpdateAsync(
            new UpdateFinanzOnlineOutboxWorkerRequest
            {
                PollIntervalSeconds = 30,
                MaxAttempts = 3,
            },
            "sa-1",
            true);

        Assert.False(updated.Enabled);
        Assert.Equal(30, updated.PollIntervalSeconds.Effective);
        Assert.Equal(10, updated.PollIntervalSeconds.Config);
        Assert.Equal(30, updated.PollIntervalSeconds.Overlay);
        Assert.Equal(3, updated.MaxAttempts.Effective);
        Assert.Equal(5, updated.MaxAttempts.Config);
        Assert.Equal("global_override", updated.Source);
        Assert.Equal(30, (int)cache.GetOverlay()!.PollIntervalSeconds!);
        Assert.True(cache.GetOverlay()!.IsComplete);
        Assert.False(cache.GetOverlay()!.Enabled);
    }

    [Fact]
    public async Task Update_WritesExclusiveSnapshot_LaterConfigChangesDoNotApply()
    {
        var (factory, cache) = CreateStore();
        var initial = CreateService(factory, cache, configEnabled: false, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));
        await initial.UpdateAsync(new UpdateFinanzOnlineOutboxWorkerRequest { Enabled = true }, "sa-1", true);

        var later = CreateService(
            factory,
            cache,
            new FinanzOnlineOutboxOptions { Enabled = false, MaxAttempts = 2, PollInterval = TimeSpan.FromSeconds(60) },
            TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var settings = later.GetSettings(true);
        Assert.True(settings.Enabled);
        Assert.Equal(5, settings.MaxAttempts.Effective);
        Assert.Equal(2, settings.MaxAttempts.Config);
        Assert.Equal(10, settings.PollIntervalSeconds.Effective);
        Assert.Equal(60, settings.PollIntervalSeconds.Config);
        Assert.Equal("global_override", settings.Source);
    }

    [Fact]
    public async Task Update_NumericOutOfRange_Throws()
    {
        var (factory, cache) = CreateStore();
        var svc = CreateService(factory, cache, configEnabled: true, TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        var ex = await Assert.ThrowsAsync<FinanzOnlineOutboxWorkerValidationException>(() =>
            svc.UpdateAsync(
                new UpdateFinanzOnlineOutboxWorkerRequest { MaxAttempts = 9 },
                "sa-1",
                true));

        Assert.Equal("MaxAttempts", ex.Field);
        Assert.Equal(5, svc.GetSettings(true).MaxAttempts.Effective);
    }

    [Fact]
    public void Parse_LegacyBoolean_StillWorks()
    {
        var overlay = FinanzOnlineOutboxEnabledOverrideCache.Parse("true");
        Assert.NotNull(overlay);
        Assert.True(overlay!.Enabled);
        Assert.Null(overlay.MaxAttempts);
    }

    [Fact]
    public void WithEffectiveEnabled_AppliesOverlay()
    {
        var (_, cache) = CreateStore();
        cache.SetOverride(true);
        var opts = new FinanzOnlineOutboxOptions { Enabled = false, MaxAttempts = 3 };

        var effective = opts.WithEffectiveEnabled(cache);

        Assert.True(effective.Enabled);
        Assert.Equal(3, effective.MaxAttempts);
        Assert.False(opts.Enabled);
    }

    private static (IDbContextFactory<AppDbContext> Factory, FinanzOnlineOutboxEnabledOverrideCache Cache) CreateStore()
    {
        var dbName = $"FoOutboxSettings_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        var cache = new FinanzOnlineOutboxEnabledOverrideCache(
            CreateScopeFactory(factory.Object),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinanzOnlineOutboxEnabledOverrideCache>.Instance);
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

    private static FinanzOnlineOutboxSettingsService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FinanzOnlineOutboxEnabledOverrideCache cache,
        bool configEnabled,
        IHostEnvironment hostEnvironment) =>
        CreateService(factory, cache, new FinanzOnlineOutboxOptions { Enabled = configEnabled }, hostEnvironment);

    private static FinanzOnlineOutboxSettingsService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FinanzOnlineOutboxEnabledOverrideCache cache,
        FinanzOnlineOutboxOptions options,
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

        return new FinanzOnlineOutboxSettingsService(
            Options.Create(options).ToMonitor(),
            cache,
            factory,
            audit.Object,
            hostEnvironment,
            NullLogger<FinanzOnlineOutboxSettingsService>.Instance);
    }
}

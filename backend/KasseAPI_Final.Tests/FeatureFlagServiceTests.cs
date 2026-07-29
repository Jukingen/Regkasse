using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FeatureFlagServiceTests
{
    private static (IDbContextFactory<AppDbContext> Factory, string DbName) CreateFactory()
    {
        var dbName = $"FeatureFlags_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        return (factory.Object, dbName);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static FeatureFlagService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FeatureFlagsOptions? opts = null)
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

        return new FeatureFlagService(
            factory,
            Options.Create(opts ?? new FeatureFlagsOptions()).ToMonitor(),
            new MemoryCache(new MemoryCacheOptions()),
            audit.Object,
            NullLogger<FeatureFlagService>.Instance);
    }

    [Fact]
    public void IsEnabled_UsesConfigDefault()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableDepExportV2 = true });
        Assert.True(svc.IsEnabled("DepExportV2"));
        Assert.False(svc.IsEnabled("EnableNewPaymentFlow"));
    }

    [Fact]
    public async Task SetEnabled_TenantOverride_BeatsConfig()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableOnlineOrdersV2 = false });
        var tenantId = Guid.NewGuid();

        await svc.SetEnabledAsync(
            FeatureFlagNames.EnableOnlineOrdersV2,
            enabled: true,
            tenantId: tenantId.ToString("D"),
            actorUserId: "admin");

        Assert.True(svc.IsEnabled("EnableOnlineOrdersV2", tenantId.ToString("D")));
        Assert.False(svc.IsEnabled("EnableOnlineOrdersV2"));
    }

    [Fact]
    public async Task ClearOverride_RestoresConfig()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableAutoAusfall = true });

        await svc.SetEnabledAsync(FeatureFlagNames.EnableAutoAusfall, false, tenantId: null, actorUserId: "admin");
        Assert.False(svc.IsEnabled(FeatureFlagNames.EnableAutoAusfall));

        await svc.ClearOverrideAsync(FeatureFlagNames.EnableAutoAusfall, tenantId: null, actorUserId: "admin");
        Assert.True(svc.IsEnabled(FeatureFlagNames.EnableAutoAusfall));
    }

    [Fact]
    public void Normalize_AcceptsShortName()
    {
        Assert.Equal(FeatureFlagNames.EnableNewPaymentFlow, FeatureFlagNames.Normalize("NewPaymentFlow"));
        Assert.Equal(FeatureFlagNames.EnableAutoAusfall, FeatureFlagNames.Normalize("enableAutoAusfall"));
    }
}

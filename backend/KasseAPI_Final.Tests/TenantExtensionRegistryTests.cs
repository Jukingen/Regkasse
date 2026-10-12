using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantExtensionRegistryTests
{
    [Fact]
    public void GetForHook_ReturnsExtension_WhenEnabledForThatHook()
    {
        var tenantId = Guid.NewGuid();
        var extension = new RecordingExtension(applies: true);
        var registry = CreateRegistry(out var dbName, extension);
        SeedEnabled(dbName, tenantId, extension.FeatureName);

        var hooks = registry.GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign);

        Assert.Contains(extension, hooks);
    }

    [Fact]
    public void GetForHook_OmitsExtension_WhenDisabledInTenantSettings()
    {
        var tenantId = Guid.NewGuid();
        var extension = new RecordingExtension(applies: true);
        var registry = CreateRegistry(out var dbName, extension);
        SeedEnabled(dbName, tenantId, "some_other_extension");

        Assert.Empty(registry.GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign));
    }

    [Fact]
    public void GetForHook_IsolatesTenants()
    {
        var enabledTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var extension = new RecordingExtension(applies: true);
        var registry = CreateRegistry(out var dbName, extension);
        SeedEnabled(dbName, enabledTenant, extension.FeatureName);
        SeedEnabled(dbName, otherTenant, "[]");

        Assert.Contains(extension, registry.GetForHook(enabledTenant, TenantExtensionHooks.PaymentBeforeSign));
        Assert.Empty(registry.GetForHook(otherTenant, TenantExtensionHooks.PaymentBeforeSign));
    }

    [Fact]
    public void GetForHook_OmitsExtension_WhenHookOrAppliesToDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var extension = new RecordingExtension(applies: false);
        var registry = CreateRegistry(out var dbName, extension);
        SeedEnabled(dbName, tenantId, extension.FeatureName);

        Assert.Empty(registry.GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign));

        var matching = new RecordingExtension(applies: true);
        var hooked = CreateRegistry(out var otherDb, matching);
        SeedEnabled(otherDb, tenantId, matching.FeatureName);
        Assert.Empty(hooked.GetForHook(tenantId, TenantExtensionHooks.ReceiptBeforePrint));
    }

    [Fact]
    public async Task SetEnabled_TogglesAuditAndDropsTheExtensionFromTheHook()
    {
        var tenantId = Guid.NewGuid();
        var extension = new RecordingExtension(applies: true);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var registry = CreateRegistry(out _, extension, audit.Object);
        await registry.SetEnabledAsync(tenantId, extension.FeatureName, true, "admin", "SuperAdmin", CancellationToken.None);

        Assert.Contains(extension, registry.GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign));

        await registry.SetEnabledAsync(tenantId, extension.FeatureName, false, "admin", "SuperAdmin", CancellationToken.None);

        Assert.Empty(registry.GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign));
        audit.Verify(a => a.LogSystemOperationAsync(
                "TENANT_EXTENSION_TOGGLED",
                "TenantSetting",
                "admin",
                "SuperAdmin",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.TenantExtensionToggled,
                It.IsAny<Guid?>(),
                tenantId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Exactly(2));
    }

    private static TenantExtensionRegistry CreateRegistry(
        out string dbName,
        ITenantFeatureExtension extension,
        IAuditLogService? audit = null)
    {
        dbName = $"TenantExt_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var factory = TenantTestDoubles.DbContextFactoryForTests(
            options,
            TenantTestDoubles.TenantAccessorReturning(null));
        var auditMock = audit ?? Mock.Of<IAuditLogService>();
        return new TenantExtensionRegistry(
            [extension],
            factory,
            new MemoryCache(new MemoryCacheOptions()),
            new MemoryOnlyCache(),
            Options.Create(new CacheSettings()),
            auditMock,
            NullLogger<TenantExtensionRegistry>.Instance);
    }

    private static void SeedEnabled(string dbName, Guid tenantId, string featureName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
        var value = featureName.StartsWith('[')
            ? featureName
            : $"[\"{featureName}\"]";
        db.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = TenantExtensionSettings.EnabledKey,
            Value = value,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [TenantExtensionHook(TenantExtensionHooks.PaymentBeforeSign)]
    private sealed class RecordingExtension : ITenantFeatureExtension
    {
        private readonly bool _applies;

        public RecordingExtension(bool applies) => _applies = applies;

        public string FeatureName => "hair_salon_campaign";

        public bool AppliesTo(Guid tenantId) => _applies && tenantId != Guid.Empty;

        public Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct) =>
            Task.FromResult(new ExtensionResult(true));
    }

    private sealed class MemoryOnlyCache : ICacheService
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_values.TryGetValue(key, out var value) && value is T typed)
                return Task.FromResult<T?>(typed);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public async Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            var existing = await GetAsync<T>(key, cancellationToken);
            if (existing is not null)
                return existing;
            var created = await factory(cancellationToken);
            await SetAsync(key, created, expiry, cancellationToken);
            return created;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            foreach (var key in _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
                _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            _values.Clear();
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.ContainsKey(key));
    }
}

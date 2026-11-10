using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyTenantCredentialResolverTests
{
    [Fact]
    public async Task Resolve_UsesDatabaseRowAheadOfDevelopmentHostKeys()
    {
        var tenantId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, Environments.Development, hostKey: "host-key", hostSecret: "host-secret");
        var protectedKey = harness.Protector.Protect("tenant-key-abcd");
        var protectedSecret = harness.Protector.Protect("tenant-secret");
        harness.Db.TenantFiskalyCredentials.Add(new TenantFiskalyCredential
        {
            TenantId = tenantId,
            ApiKeyProtected = protectedKey,
            ApiSecretProtected = protectedSecret,
            ApiKeyHint = "abcd",
            ScuId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            Environment = "LIVE",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await harness.Db.SaveChangesAsync();

        var resolved = await harness.Resolver.ResolveAsync();

        Assert.NotNull(resolved);
        Assert.Equal(FiskalyCredentialSources.Tenant, resolved!.Source);
        Assert.Equal(tenantId, resolved.TenantId);
        Assert.Equal("tenant-key-abcd", resolved.ApiKey);
        Assert.Equal("tenant-secret", resolved.ApiSecret);
        Assert.Equal("LIVE", resolved.Environment);
    }

    [Fact]
    public async Task Resolve_WithoutRow_UsesHostKeysOnlyInDevelopment()
    {
        var tenantId = Guid.NewGuid();
        var development = CreateHarness(tenantId, Environments.Development, hostKey: "host-key", hostSecret: "host-secret");
        var resolved = await development.Resolver.ResolveAsync();
        Assert.NotNull(resolved);
        Assert.Equal(FiskalyCredentialSources.Host, resolved!.Source);
        Assert.Null(resolved.TenantId);
        Assert.Equal("host-key", resolved.ApiKey);
        Assert.Equal("host-secret", resolved.ApiSecret);

        var production = CreateHarness(tenantId, Environments.Production, hostKey: "host-key", hostSecret: "host-secret");
        var blocked = await production.Resolver.ResolveAsync();
        Assert.Null(blocked);
    }

    [Fact]
    public async Task Resolve_InactiveRow_DoesNotFallBackToHostKeys()
    {
        var tenantId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, Environments.Development, hostKey: "host-key", hostSecret: "host-secret");
        harness.Db.TenantFiskalyCredentials.Add(new TenantFiskalyCredential
        {
            TenantId = tenantId,
            ApiKeyProtected = harness.Protector.Protect("tenant-key"),
            ApiSecretProtected = harness.Protector.Protect("tenant-secret"),
            ApiKeyHint = "key",
            ScuId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
            Environment = "TEST",
            CreatedAt = DateTime.UtcNow,
            IsActive = false,
        });
        await harness.Db.SaveChangesAsync();

        var resolved = await harness.Resolver.ResolveAsync();
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Upsert_AuditsHintAndScuWithoutTheSecret()
    {
        var tenantId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, Environments.Development, hostKey: "host-key", hostSecret: "host-secret");
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var admin = new FiskalyCredentialAdminService(
            harness.Factory,
            harness.Tenant,
            harness.Protector,
            harness.Resolver,
            new FiskalyAccessTokenCache(),
            audit.Object);

        const string secret = "super-secret-value";
        const string scu = "e741519c-4367-4d72-abcf-1b1430d59ec7";
        var result = await admin.UpsertAsync(
            new UpsertFiskalyTenantCredentialRequest
            {
                ApiKey = "test_key_abcd",
                ApiSecret = secret,
                ScuId = scu,
                Environment = "test",
            },
            "manager-1");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("abcd", result.Body!.ApiKeyHint);
        Assert.Equal(scu, result.Body.ScuId);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(result.Body));

        var invocation = Assert.Single(audit.Invocations);
        Assert.Equal(AuditEventType.FiskalySettingsChanged, invocation.Arguments.Single(a => a is AuditEventType));
        var payload = string.Join('\n', invocation.Arguments.Select(a => a is null ? string.Empty : JsonSerializer.Serialize(a)));
        Assert.Contains("abcd", payload);
        Assert.Contains(scu, payload);
        Assert.DoesNotContain(secret, payload);

        var resolved = await harness.Resolver.ResolveAsync();
        Assert.Equal("test_key_abcd", resolved!.ApiKey);
        Assert.Equal(secret, resolved.ApiSecret);
        Assert.Equal(FiskalyCredentialSources.Tenant, resolved.Source);
    }

    private static Harness CreateHarness(Guid tenantId, string environment, string hostKey, string hostSecret)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_creds_{Guid.NewGuid():N}")
            .Options;
        var tenant = new TenantTestDoubles.MutableTenantAccessor(tenantId);
        var db = new AppDbContext(options, tenant);
        var protector = new FiskalyCredentialProtector(new EphemeralDataProtectionProvider());
        var fiskaly = Options.Create(new FiskalyOptions
        {
            Enabled = true,
            ApiKey = hostKey,
            ApiSecret = hostSecret,
            ScuId = "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
            Environment = "TEST",
        }).ToMonitor();
        var resolver = new FiskalyTenantCredentialResolver(
            TenantTestDoubles.DbContextFactoryForTests(options, tenant),
            tenant,
            protector,
            fiskaly,
            TenantTestDoubles.HostEnvironmentReturning(environment),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FiskalyTenantCredentialResolver>.Instance);
        return new Harness(db, TenantTestDoubles.DbContextFactoryForTests(options, tenant), tenant, protector, resolver);
    }

    private sealed record Harness(
        AppDbContext Db,
        IDbContextFactory<AppDbContext> Factory,
        TenantTestDoubles.MutableTenantAccessor Tenant,
        FiskalyCredentialProtector Protector,
        FiskalyTenantCredentialResolver Resolver);
}

public sealed class FiskalyAccessTokenCacheTests
{
    [Fact]
    public void TwoTenantsDoNotShareTokens()
    {
        var cache = new FiskalyAccessTokenCache();
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var expires = DateTimeOffset.UtcNow.AddHours(2);

        cache.Set(tenantA, "token-a", expires);
        cache.Set(tenantB, "token-b", expires);
        cache.Set(null, "token-host", expires);

        Assert.True(cache.TryGet(tenantA, out var tokenA, out _));
        Assert.Equal("token-a", tokenA);
        Assert.True(cache.TryGet(tenantB, out var tokenB, out _));
        Assert.Equal("token-b", tokenB);
        Assert.True(cache.TryGet(null, out var host, out _));
        Assert.Equal("token-host", host);

        cache.Clear(tenantA);
        Assert.False(cache.TryGet(tenantA, out _, out _));
        Assert.True(cache.TryGet(tenantB, out var stillB, out _));
        Assert.Equal("token-b", stillB);
        Assert.True(cache.TryGet(null, out _, out _));
    }
}

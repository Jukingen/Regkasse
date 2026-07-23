using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CashRegisterSettingsServiceTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetOrCreateForEffectiveTenantAsync_Returns_Existing_Row_When_Query_Filter_Hides_It()
    {
        await using var db = CreateContext(new FixedTenantAccessor(TenantAId));
        TenantTestDoubles.EnsureDefaultTenant(db);
        db.Tenants.Add(new Tenant
        {
            Id = TenantBId,
            Name = "Tenant B",
            Slug = "tenant-b",
        });
        db.CashRegisterSettings.Add(new CashRegisterSettings
        {
            TenantId = TenantBId,
            EffectiveDefaultOnPosEntry = false,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = TenantTestDoubles.SettingsResolverReturning(TenantBId);
        var service = new CashRegisterSettingsService(db, new FixedTenantAccessor(TenantAId), resolver);

        var row = await service.GetOrCreateForEffectiveTenantAsync();

        Assert.Equal(TenantBId, row.TenantId);
        Assert.False(row.EffectiveDefaultOnPosEntry);
        Assert.Single(await db.CashRegisterSettings.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task GetOrCreateForEffectiveTenantAsync_Creates_Default_Row_When_Missing()
    {
        await using var db = CreateContext(new FixedTenantAccessor(TenantAId));
        TenantTestDoubles.EnsureDefaultTenant(db);
        db.Tenants.Add(new Tenant
        {
            Id = TenantBId,
            Name = "Tenant B",
            Slug = "tenant-b",
        });
        await db.SaveChangesAsync();

        var resolver = TenantTestDoubles.SettingsResolverReturning(TenantBId);
        var service = new CashRegisterSettingsService(db, new FixedTenantAccessor(TenantAId), resolver);

        var row = await service.GetOrCreateForEffectiveTenantAsync();

        Assert.Equal(TenantBId, row.TenantId);
        Assert.True(row.EffectiveDefaultOnPosEntry);
        Assert.Single(await db.CashRegisterSettings.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task GetFeatureOptionsAsync_Returns_Default_When_No_Tenant_Context()
    {
        await using var db = CreateContext(NullCurrentTenantAccessor.Instance);
        var resolver = TenantTestDoubles.SettingsResolverReturning(TenantBId);
        var service = new CashRegisterSettingsService(db, NullCurrentTenantAccessor.Instance, resolver);

        var options = await service.GetFeatureOptionsAsync();

        Assert.Same(PosCashRegisterFeatureOptions.Default, options);
        Assert.Empty(await db.CashRegisterSettings.IgnoreQueryFilters().ToListAsync());
    }

    private static AppDbContext CreateContext(ICurrentTenantAccessor tenantAccessor)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegisterSettings_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, tenantAccessor);
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    public string? TenantSlug { get; set; }
    }
}

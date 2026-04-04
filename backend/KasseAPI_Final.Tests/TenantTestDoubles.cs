using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Shared test doubles for controllers that depend on tenant resolution / membership provisioning.</summary>
internal static class TenantTestDoubles
{
    /// <summary>Resolver fixed to <see cref="LegacyDefaultTenantIds.Primary"/> for legacy single-tenant test data.</summary>
    public static ISettingsTenantResolver PrimaryTenantResolver => SettingsResolverReturning(LegacyDefaultTenantIds.Primary);

    public static ISettingsTenantResolver SettingsResolverReturning(Guid tenantId)
    {
        var m = new Mock<ISettingsTenantResolver>();
        m.Setup(x => x.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenantId);
        return m.Object;
    }

    /// <summary>Inserts legacy primary tenant if missing (required for FK on tenant-scoped catalog rows in in-memory tests).</summary>
    public static void EnsureDefaultTenant(AppDbContext context)
    {
        if (!context.Tenants.AsNoTracking().Any(t => t.Id == LegacyDefaultTenantIds.Primary))
        {
            context.Tenants.Add(new Tenant
            {
                Id = LegacyDefaultTenantIds.Primary,
                Name = "Default",
                Slug = LegacyDefaultTenantIds.PrimarySlug
            });
        }
    }

    public static IUserTenantMembershipProvisioner NoOpProvisioner(Mock<IUserTenantMembershipProvisioner>? capture = null)
    {
        var m = capture ?? new Mock<IUserTenantMembershipProvisioner>();
        m.Setup(x => x.ProvisionActiveMembershipAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m.Object;
    }
}

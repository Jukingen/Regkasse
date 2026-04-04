using KasseAPI_Final.Tenancy;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Shared test doubles for controllers that depend on tenant resolution / membership provisioning.</summary>
internal static class TenantTestDoubles
{
    public static ISettingsTenantResolver SettingsResolverReturning(Guid tenantId)
    {
        var m = new Mock<ISettingsTenantResolver>();
        m.Setup(x => x.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenantId);
        return m.Object;
    }

    public static IUserTenantMembershipProvisioner NoOpProvisioner(Mock<IUserTenantMembershipProvisioner>? capture = null)
    {
        var m = capture ?? new Mock<IUserTenantMembershipProvisioner>();
        m.Setup(x => x.ProvisionActiveMembershipAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m.Object;
    }
}

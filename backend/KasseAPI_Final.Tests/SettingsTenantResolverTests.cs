using System.Security.Claims;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class SettingsTenantResolverTests
{
    private static SettingsTenantResolver CreateResolver(
        IHttpContextAccessor httpAccessor,
        IAuthTenantSnapshotProvider snapshotProvider,
        Guid? ambientTenantId = null)
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.SetupGet(a => a.TenantId).Returns(ambientTenantId);
        return new SettingsTenantResolver(httpAccessor, snapshotProvider, tenantAccessor.Object);
    }

    [Fact]
    public async Task ResolveEffectiveTenantIdAsync_Delegates_To_AuthTenantSnapshot()
    {
        var expected = LegacyDefaultTenantIds.Primary.ToString("D");
        var snapshotMock = new Mock<IAuthTenantSnapshotProvider>();
        snapshotMock
            .Setup(p => p.GetSnapshotAsync(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(expected, "X", LegacyDefaultTenantIds.PrimarySlug, null, null));

        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var resolver = CreateResolver(http.Object, snapshotMock.Object);
        var id = await resolver.ResolveEffectiveTenantIdAsync();

        Assert.Equal(LegacyDefaultTenantIds.Primary, id);
        snapshotMock.Verify(p => p.GetSnapshotAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveEffectiveTenantIdAsync_Passes_Current_User_To_Snapshot()
    {
        var other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var principal = new ClaimsPrincipal(new ClaimsIdentity("x"));
        var httpContext = new DefaultHttpContext { User = principal };

        var snapshotMock = new Mock<IAuthTenantSnapshotProvider>();
        snapshotMock
            .Setup(p => p.GetSnapshotAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(other.ToString("D"), "T", "other", null, null));

        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(httpContext);

        var resolver = CreateResolver(http.Object, snapshotMock.Object);
        var id = await resolver.ResolveEffectiveTenantIdAsync();

        Assert.Equal(other, id);
    }

    [Fact]
    public async Task ResolveEffectiveTenantIdAsync_Prefers_Ambient_Tenant_Over_Jwt()
    {
        var ambient = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var jwtTenant = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var snapshotMock = new Mock<IAuthTenantSnapshotProvider>();
        snapshotMock
            .Setup(p => p.GetSnapshotAsync(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(jwtTenant.ToString("D"), "Jwt", "jwt", null, null));

        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext());

        var resolver = CreateResolver(http.Object, snapshotMock.Object, ambient);
        var id = await resolver.ResolveEffectiveTenantIdAsync();

        Assert.Equal(ambient, id);
        snapshotMock.Verify(
            p => p.GetSnapshotAsync(It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

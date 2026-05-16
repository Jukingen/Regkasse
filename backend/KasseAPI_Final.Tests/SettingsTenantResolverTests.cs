using System.Security.Claims;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class SettingsTenantResolverTests
{
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

        var resolver = new SettingsTenantResolver(http.Object, snapshotMock.Object);
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

        var resolver = new SettingsTenantResolver(http.Object, snapshotMock.Object);
        var id = await resolver.ResolveEffectiveTenantIdAsync();

        Assert.Equal(other, id);
    }
}

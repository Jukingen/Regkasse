using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.CriticalActions;
using KasseAPI_Final.Services.TwoFactor;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CriticalActionApprovalServiceTests
{
    [Fact]
    public void MatchCriticalAction_DeactivateAll_ReturnsDeleteAllProducts()
    {
        var svc = CreateService();
        var matched = svc.MatchCriticalAction("POST", "/api/admin/products/deactivate-all");
        Assert.Equal(CriticalActionType.DeleteAllProducts, matched);
    }

    [Fact]
    public void MatchCriticalAction_TenantPermanentDelete_ReturnsTenantDeletion()
    {
        var svc = CreateService();
        var matched = svc.MatchCriticalAction(
            "DELETE",
            "/api/admin/tenants/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/permanent");
        Assert.Equal(CriticalActionType.TenantDeletion, matched);
    }

    [Fact]
    public void MatchCriticalAction_SafeMethod_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(svc.MatchCriticalAction("GET", "/api/admin/products/deactivate-all"));
    }

    [Fact]
    public void MatchCriticalAction_OwnApi_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(svc.MatchCriticalAction("POST", "/api/admin/critical-actions/approve-with-2fa"));
    }

    [Fact]
    public async Task IssueWithTwoFactor_ThenVerify_SucceedsOnce()
    {
        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor
            .Setup(t => t.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync("user-1"))
            .ReturnsAsync(new ApplicationUser { Id = "user-1" });

        var svc = CreateService(twoFactor: twoFactor.Object, userManager: userManager.Object);
        var issued = await svc.IssueWithTwoFactorAsync(
            "user-1",
            CriticalActionType.DeleteAllProducts,
            "/api/admin/products/deactivate-all",
            "123456");

        Assert.True(issued.Ok);
        Assert.False(string.IsNullOrWhiteSpace(issued.Token));

        var first = await svc.VerifyApprovalAsync(
            "user-1",
            issued.Token!,
            "/api/admin/products/deactivate-all");
        var second = await svc.VerifyApprovalAsync(
            "user-1",
            issued.Token!,
            "/api/admin/products/deactivate-all");

        Assert.True(first);
        Assert.False(second);
    }

    private static CriticalActionApprovalService CreateService(
        ITwoFactorService? twoFactor = null,
        UserManager<ApplicationUser>? userManager = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new CriticalActionOptions
        {
            Enabled = true,
            BypassInDevelopment = false,
            ApprovalTokenTtlMinutes = 5,
        });
        var monitor = Mock.Of<IOptionsMonitor<CriticalActionOptions>>(m => m.CurrentValue == options.Value);

        twoFactor ??= Mock.Of<ITwoFactorService>();
        if (userManager is null)
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            userManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!).Object;
        }

        return new CriticalActionApprovalService(
            cache,
            monitor,
            twoFactor,
            userManager,
            Mock.Of<IApprovalService>(),
            NullLogger<CriticalActionApprovalService>.Instance);
    }
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RolePermissionSimulateServiceTests
{
    [Fact]
    public async Task SimulateAsync_ReportsAddedRemovedAndUserImpact()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "cashier1",
            Role = Roles.Cashier,
        };

        var users = new List<ApplicationUser> { user };
        var userManager = CreateUserManagerMock(users, new Dictionary<string, IList<string>>
        {
            [user.Id] = new List<string> { Roles.Cashier },
        });

        var currentRolePerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.SaleView,
            AppPermissions.PaymentView,
        };
        var roleResolver = new Mock<IRolePermissionResolver>();
        roleResolver
            .Setup(r => r.GetPermissionsForRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentRolePerms);

        var effective = new Mock<IEffectivePermissionResolver>();
        effective
            .Setup(e => e.GetEffectivePermissionsAsync(
                user.Id,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentRolePerms);

        var proposed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.SaleView,
            AppPermissions.ReportView,
        };
        effective
            .Setup(e => e.GetEffectivePermissionsWithRoleOverrideAsync(
                user.Id,
                It.IsAny<IReadOnlySet<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposed);

        var svc = new RolePermissionSimulateService(userManager.Object, roleResolver.Object, effective.Object);
        var result = await svc.SimulateAsync(
            Roles.Cashier,
            new[] { AppPermissions.SaleView, AppPermissions.ReportView },
            page: 1,
            pageSize: 50);

        Assert.Equal(Roles.Cashier, result.RoleName);
        Assert.Contains(AppPermissions.ReportView, result.Added);
        Assert.Contains(AppPermissions.PaymentView, result.Removed);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.AffectedUserCount);
        Assert.Single(result.Users);
        Assert.Equal(1, result.Users[0].PermissionsGained);
        Assert.Equal(1, result.Users[0].PermissionsLost);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(
        IList<ApplicationUser> usersInRole,
        IReadOnlyDictionary<string, IList<string>> rolesByUser)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        mgr.Setup(m => m.GetUsersInRoleAsync(It.IsAny<string>()))
            .ReturnsAsync(usersInRole.ToList());
        mgr.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser u) =>
                rolesByUser.TryGetValue(u.Id, out var roles) ? roles : Array.Empty<string>());
        return mgr;
    }
}

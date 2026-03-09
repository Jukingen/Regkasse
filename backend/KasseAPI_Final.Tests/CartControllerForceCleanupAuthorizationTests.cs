using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// CartController force-cleanup (POST api/cart/force-cleanup) is protected by class-level [HasPermission(CartManage)].
/// Roles with CartManage: Cashier, Manager, Admin, SuperAdmin. Waiter has only CartView and must be denied.
/// </summary>
public class CartControllerForceCleanupAuthorizationTests
{
    [Fact]
    public void ForceCleanup_Requires_CartManage_Cashier_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CartManage));
    }

    [Fact]
    public void ForceCleanup_Requires_CartManage_Manager_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CartManage));
    }

    [Fact]
    public void ForceCleanup_Requires_CartManage_Waiter_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CartManage));
    }

    [Fact]
    public void ForceCleanup_Requires_CartManage_Admin_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.CartManage));
    }
}

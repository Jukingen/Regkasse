using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// CartController force-cleanup (POST api/cart/force-cleanup) is protected by class-level [HasPermission(CartManage)].
/// CartManage is a POS-terminal permission: Cashier and SuperAdmin have it.
/// Manager (Admin-only) has CartView but not CartManage. Waiter has only CartView.
/// </summary>
public class CartControllerForceCleanupAuthorizationTests
{
    [Fact]
    public void ForceCleanup_Requires_CartManage_Cashier_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CartManage));
    }

    [Fact]
    public void ForceCleanup_Requires_CartManage_Manager_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CartManage));
    }

    [Fact]
    public void ForceCleanup_Requires_CartManage_Waiter_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CartManage));
    }

    [Fact]
    public void ForceCleanup_Requires_CartManage_SuperAdmin_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CartManage));
    }
}

using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// GET api/pos/cash-register/selectable is protected by <see cref="AppPermissions.CartView"/> (POS entry).
/// </summary>
public class PosCashRegisterSelectableAuthorizationTests
{
    [Fact]
    public void SelectableList_Requires_CartView_Cashier_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CartView));
    }

    [Fact]
    public void SelectableList_Requires_CartView_Waiter_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CartView));
    }

    [Fact]
    public void SelectableList_Requires_CartView_Kitchen_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Kitchen, AppPermissions.CartView));
    }
}

using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>GET/POST/PUT api/admin/cash-registers permission matrix smoke checks.</summary>
public class AdminCashRegistersAuthorizationTests
{
    [Fact]
    public void List_Requires_CashRegisterView_Cashier_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterView));
    }

    [Fact]
    public void Create_Requires_CashRegisterManage_Cashier_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterManage));
    }

    [Fact]
    public void Update_Requires_CashRegisterManage_Manager_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterManage));
    }
}

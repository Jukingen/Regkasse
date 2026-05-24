using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PUT api/admin/cash-registers/{id}/decommission is protected by [HasPermission(CashRegisterDecommission)].
/// Domain-level Stilllegen permission; lower-level RKSV API remains on RksvSchlussbelegCreate.
/// </summary>
public class AdminCashRegistersDecommissionAuthorizationTests
{
    [Fact]
    public void Decommission_Requires_CashRegisterDecommission_SuperAdmin_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void Decommission_Requires_CashRegisterDecommission_Manager_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void Decommission_Requires_CashRegisterDecommission_Cashier_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void Decommission_Requires_CashRegisterDecommission_Waiter_DoesNotHave_Permission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CashRegisterDecommission));
    }
}

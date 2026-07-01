using System.Reflection;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Contract: <see cref="AdminCashRegistersController"/> read GETs use cash_register.view;
/// mutations use manage / decommission / system.critical.
/// </summary>
public class AdminCashRegistersAuthorizationTests
{
    private static MethodInfo? FindAction(string name) =>
        typeof(AdminCashRegistersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SingleOrDefault(m => m.Name == name);

    private static IEnumerable<string> GetHasPermissionValues(MethodInfo method) =>
        method.GetCustomAttributes<HasPermissionAttribute>()
            .Select(a => a.Permission);

    [Theory]
    [InlineData(nameof(AdminCashRegistersController.List))]
    [InlineData(nameof(AdminCashRegistersController.ListByTenant))]
    [InlineData(nameof(AdminCashRegistersController.GetCashRegisterCount))]
    [InlineData(nameof(AdminCashRegistersController.GetTseHealth))]
    [InlineData(nameof(AdminCashRegistersController.GetCapabilities))]
    public void ReadEndpoints_RequireCashRegisterView(string actionName)
    {
        var method = FindAction(actionName);
        Assert.NotNull(method);

        var permissions = GetHasPermissionValues(method).ToList();
        Assert.Contains(AppPermissions.CashRegisterView, permissions);
        Assert.DoesNotContain(AppPermissions.CashRegisterManage, permissions);
    }

    [Fact]
    public void List_Requires_CashRegisterView_Manager_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterView));
    }

    [Fact]
    public void List_Requires_CashRegisterView_Cashier_Has_Permission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterView));
    }

    [Fact]
    public void Create_Requires_CashRegisterManage_OnController()
    {
        var method = FindAction(nameof(AdminCashRegistersController.Create));
        Assert.NotNull(method);
        Assert.Contains(AppPermissions.CashRegisterManage, GetHasPermissionValues(method));
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

    [Fact]
    public void Decommission_Requires_CashRegisterDecommission()
    {
        var method = FindAction(nameof(AdminCashRegistersController.Decommission));
        Assert.NotNull(method);
        Assert.Contains(AppPermissions.CashRegisterDecommission, GetHasPermissionValues(method));
    }

    [Fact]
    public void HardDelete_Requires_SystemCritical()
    {
        var method = FindAction(nameof(AdminCashRegistersController.HardDelete));
        Assert.NotNull(method);
        Assert.Contains(AppPermissions.SystemCritical, GetHasPermissionValues(method));
    }
}

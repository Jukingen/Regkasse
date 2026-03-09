using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for RolePermissionMatrix: role-to-permission mapping and Administrator = Admin alias.
/// </summary>
public class RolePermissionMatrixTests
{
    // --- Positive: role has permission ---

    [Fact]
    public void RoleHasPermission_Cashier_Has_PaymentTake()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.PaymentTake));
    }

    [Fact]
    public void RoleHasPermission_Cashier_Has_RefundCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.RefundCreate));
    }

    [Fact]
    public void RoleHasPermission_Waiter_Has_OrderCancel()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.OrderCancel));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_AuditExport()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.AuditExport));
    }

    [Fact]
    public void RoleHasPermission_ReportViewer_Has_ReportView()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.ReportViewer, AppPermissions.ReportView));
    }

    [Fact]
    public void RoleHasPermission_Admin_Has_SettingsManage()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.SettingsManage));
    }

    // --- Negative: role must NOT have permission (authorization migration scenarios) ---

    [Fact]
    public void RoleHasPermission_Waiter_DoesNotHave_RefundCreate()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.RefundCreate));
    }

    [Fact]
    public void RoleHasPermission_Cashier_DoesNotHave_SettingsManage()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.SettingsManage));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_AuditCleanup()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.AuditCleanup));
    }

    [Fact]
    public void RoleHasPermission_ReportViewer_DoesNotHave_PaymentTake()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.ReportViewer, AppPermissions.PaymentTake));
    }

    // --- Administrator = Admin alias ---

    [Fact]
    public void RoleHasPermission_Administrator_Has_SettingsManage_SameAs_Admin()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Administrator, AppPermissions.SettingsManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.SettingsManage));
    }

    [Fact]
    public void RoleHasPermission_Administrator_Has_UserManage_SameAs_Admin()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Administrator, AppPermissions.UserManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.UserManage));
    }

    [Fact]
    public void GetPermissionsForRoles_Administrator_SetEquals_Admin()
    {
        var adminPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Admin });
        var administratorPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Administrator });
        Assert.Equal(adminPerms.Count, administratorPerms.Count);
        foreach (var p in adminPerms)
            Assert.True(administratorPerms.Contains(p));
    }

    // --- Edge cases ---

    [Fact]
    public void RoleHasPermission_UnknownRole_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission("UnknownRole", AppPermissions.PaymentTake));
    }

    [Fact]
    public void RoleHasPermission_NullOrEmptyRole_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(null!, AppPermissions.PaymentTake));
        Assert.False(RolePermissionMatrix.RoleHasPermission("", AppPermissions.PaymentTake));
    }

    [Fact]
    public void GetPermissionsForRoles_MultipleRoles_ReturnsUnion()
    {
        var waiterPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Waiter });
        var reportPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.ReportViewer });
        var combined = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Waiter, Roles.ReportViewer });
        Assert.True(combined.Count >= waiterPerms.Count);
        Assert.True(combined.Count >= reportPerms.Count);
        Assert.True(combined.Contains(AppPermissions.ReportView));
        Assert.True(combined.Contains(AppPermissions.OrderView));
    }
}

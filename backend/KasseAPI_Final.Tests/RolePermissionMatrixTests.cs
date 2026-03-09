using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for RolePermissionMatrix: role-to-permission mapping.
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

    [Fact]
    public void RoleHasPermission_Admin_Has_PaymentTake_So_PaymentMiddleware_Allows_Admin()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.PaymentTake));
    }

    // --- Negative: role must NOT have permission ---

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

    [Fact]
    public void RoleHasPermission_Waiter_DoesNotHave_CartManage()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CartManage));
    }

    [Fact]
    public void RoleHasPermission_Waiter_Has_CartView()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CartView));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_SystemCritical()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.SystemCritical));
    }

    [Fact]
    public void RoleHasPermission_Admin_DoesNotHave_SystemCritical()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.SystemCritical));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_UserView()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.UserView));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_UserManage()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.UserManage));
    }

    [Fact]
    public void RoleHasPermission_Admin_DoesNotHave_InventoryDelete()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.InventoryDelete));
    }

    [Fact]
    public void RoleHasPermission_Admin_DoesNotHave_TseDiagnostics()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.TseDiagnostics));
    }

    [Fact]
    public void RoleHasPermission_Admin_DoesNotHave_AuditCleanup()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Admin, AppPermissions.AuditCleanup));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_TseDiagnostics()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.TseDiagnostics));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_AuditCleanup()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.AuditCleanup));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_InventoryDelete()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.InventoryDelete));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_InventoryDelete()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.InventoryDelete));
    }

    // --- Edge cases ---

    [Fact]
    public void RoleHasPermission_UnknownRole_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission("UnknownRole", AppPermissions.PaymentTake));
    }

    /// <summary>Administrator is unsupported/absent; canonical admin role is Admin only.</summary>
    [Fact]
    public void RoleHasPermission_AdministratorRole_NotInMatrix_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission("Administrator", AppPermissions.UserManage));
        Assert.False(RolePermissionMatrix.RoleHasPermission("Administrator", AppPermissions.SettingsManage));
    }

    [Fact]
    public void GetPermissionsForRoles_Administrator_ReturnsEmpty()
    {
        var perms = RolePermissionMatrix.GetPermissionsForRoles(new[] { "Administrator" });
        Assert.NotNull(perms);
        Assert.Empty(perms);
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

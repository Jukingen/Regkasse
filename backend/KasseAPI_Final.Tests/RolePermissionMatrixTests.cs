using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for RolePermissionMatrix: role-to-permission mapping.
/// Admin role removed; SuperAdmin has full set, Manager has explicit set.
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
    public void RoleHasPermission_Manager_Has_VoucherPermissions()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.VoucherRead));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.VoucherCreate));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.VoucherCancel));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.VoucherAuditView));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.VoucherIssue));
    }

    [Fact]
    public void RoleHasPermission_Cashier_Has_VoucherIssue()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.VoucherIssue));
    }

    [Fact]
    public void RoleHasPermission_ReportViewer_Has_ReportView()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.ReportViewer, AppPermissions.ReportView));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_SettingsManage()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.SettingsManage));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_PaymentTake_So_PaymentMiddleware_Allows_SuperAdmin()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.PaymentTake));
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
    public void RoleHasPermission_Waiter_Has_CashRegisterView_ForPosSelection()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CashRegisterView));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_SystemCritical()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.SystemCritical));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_SystemCritical()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.SystemCritical));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_UserView()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.UserView));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_RksvNullbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.RksvNullbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Accountant_Has_RksvNullbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Accountant, AppPermissions.RksvNullbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Cashier_Has_RksvStartbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.RksvStartbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_RksvStartbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.RksvStartbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Cashier_Has_RksvMonatsbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.RksvMonatsbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_RksvMonatsbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.RksvMonatsbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Accountant_Has_RksvMonatsbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Accountant, AppPermissions.RksvMonatsbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Cashier_Has_RksvJahresbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.RksvJahresbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_RksvJahresbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.RksvJahresbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Accountant_Has_RksvJahresbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Accountant, AppPermissions.RksvJahresbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_RksvSchlussbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.RksvSchlussbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_RksvSchlussbelegCreate()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.RksvSchlussbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Cashier_DoesNotHave_RksvSchlussbelegCreate()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.RksvSchlussbelegCreate));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_CashRegisterDecommission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_CashRegisterDecommission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_Cashier_DoesNotHave_CashRegisterDecommission()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_SuperAdmin_Has_CashRegisterView_Manage_Decommission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterView));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_Manager_Has_CashRegisterView_Manage_Decommission()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterView));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_Cashier_Has_CashRegisterView_Only()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterView));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterManage));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_Accountant_Has_CashRegisterView_Only()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Accountant, AppPermissions.CashRegisterView));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Accountant, AppPermissions.CashRegisterManage));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Accountant, AppPermissions.CashRegisterDecommission));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_UserManage()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.UserManage));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_InventoryDelete()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.InventoryDelete));
    }

    [Fact]
    public void RoleHasPermission_Manager_DoesNotHave_TseDiagnostics()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.TseDiagnostics));
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

    // --- Edge cases ---

    [Fact]
    public void RoleHasPermission_UnknownRole_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission("UnknownRole", AppPermissions.PaymentTake));
    }

    /// <summary>Administrator legacy name not in matrix.</summary>
    [Fact]
    public void RoleHasPermission_AdministratorRole_NotInMatrix_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission("Administrator", AppPermissions.UserManage));
        Assert.False(RolePermissionMatrix.RoleHasPermission("Administrator", AppPermissions.SettingsManage));
    }

    /// <summary>Admin role removed from matrix; treated as unknown for matrix lookup.</summary>
    [Fact]
    public void RoleHasPermission_AdminRole_NotInMatrix_ReturnsFalse()
    {
        Assert.False(RolePermissionMatrix.RoleHasPermission("Admin", AppPermissions.UserManage));
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

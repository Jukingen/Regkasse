using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AdminAppPermissionProfileTests
{
    [Fact]
    public void Filter_PosContext_ReturnsUnchanged()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var result = AdminAppPermissionProfile.Filter(ClientAppPolicy.Pos, new[] { Roles.Cashier }, effective);
        Assert.Equal(effective.Count, result.Count);
    }

    [Fact]
    public void Filter_Admin_Cashier_OnlyViewOnlyAllowlist()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Cashier },
            effective);

        Assert.Contains(AppPermissions.ProductView, result);
        Assert.Contains(AppPermissions.PaymentView, result);
        Assert.Contains(AppPermissions.ReportView, result);
        Assert.DoesNotContain(AppPermissions.TableView, result);
        Assert.DoesNotContain(AppPermissions.SaleView, result);
        Assert.DoesNotContain(AppPermissions.ShiftView, result);
        Assert.DoesNotContain(AppPermissions.TseSign, result);
        Assert.DoesNotContain(AppPermissions.CashRegisterView, result);
        Assert.DoesNotContain(AppPermissions.PaymentTake, result);
    }

    [Fact]
    public void Filter_Admin_Manager_StripsPosTerminalOnly()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.Contains(AppPermissions.CashRegisterManage, result);
        Assert.Contains(AppPermissions.ReportExport, result);
        Assert.Contains(AppPermissions.UserView, result);
        Assert.Contains(AppPermissions.TableView, result);
        Assert.Contains(AppPermissions.SaleView, result);
        Assert.DoesNotContain(AppPermissions.PaymentTake, result);
        Assert.DoesNotContain(AppPermissions.CartManage, result);
        Assert.DoesNotContain(AppPermissions.TseSign, result);
        Assert.DoesNotContain(AppPermissions.VoucherIssue, result);
    }

    [Fact]
    public void Filter_Admin_SuperAdmin_Unchanged()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.SuperAdmin });
        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.SuperAdmin },
            effective);
        Assert.Equal(effective.Count, result.Count);
    }
}

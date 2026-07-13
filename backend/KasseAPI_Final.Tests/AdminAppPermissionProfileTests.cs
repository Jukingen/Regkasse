using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AdminAppPermissionProfileTests
{
    [Fact]
    public void Filter_PosContext_ReturnsFullCashierPosPermissions()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var result = AdminAppPermissionProfile.Filter(ClientAppPolicy.Pos, new[] { Roles.Cashier }, effective);

        foreach (var permission in AdminAppPermissionProfile.CashierPosPermissions)
            Assert.Contains(permission, result);
    }

    [Fact]
    public void Filter_PosContext_Cashier_MergesMatrixWhenEffectiveSubset()
    {
        var subset = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.ProductView };
        var result = AdminAppPermissionProfile.Filter(ClientAppPolicy.Pos, new[] { Roles.Cashier }, subset);

        Assert.Contains(AppPermissions.ProductView, result);
        Assert.Contains(AppPermissions.CartManage, result);
        Assert.Contains(AppPermissions.PaymentTake, result);
        Assert.Contains(AppPermissions.LicenseView, result);
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
    public void CashierPosPermissions_IncludesLicenseView_ForPosMandantGate()
    {
        Assert.Contains(AppPermissions.LicenseView, AdminAppPermissionProfile.CashierPosPermissions);
    }

    [Fact]
    public void Filter_Admin_Cashier_IncludesLicenseView()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Cashier },
            effective);

        Assert.Contains(AppPermissions.LicenseView, result);
    }

    [Fact]
    public void Filter_Admin_Manager_StripsPosOperationalWriteOnly()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.Contains(AppPermissions.CashRegisterView, result);
        Assert.Contains(AppPermissions.CashRegisterManage, result);
        Assert.Contains(AppPermissions.CashRegisterDecommission, result);
        Assert.Contains(AppPermissions.ReportExport, result);
        Assert.Contains(AppPermissions.UserView, result);
        Assert.Contains(AppPermissions.UserResetPassword, result);
        Assert.Contains(AppPermissions.TableView, result);
        Assert.Contains(AppPermissions.SaleView, result);
        Assert.DoesNotContain(AppPermissions.PaymentTake, result);
        Assert.DoesNotContain(AppPermissions.CartManage, result);
        Assert.DoesNotContain(AppPermissions.TseSign, result);
        Assert.DoesNotContain(AppPermissions.VoucherIssue, result);
        Assert.DoesNotContain(AppPermissions.SaleCreate, result);
        Assert.DoesNotContain(AppPermissions.OrderCreate, result);
    }

    [Theory]
    [MemberData(nameof(ManagerOversightViewPermissionCases))]
    public void Filter_Admin_Manager_PreservesOversightViewPermissions(string permission)
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        Assert.Contains(permission, effective);

        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.Contains(permission, result);
    }

    public static IEnumerable<object[]> ManagerOversightViewPermissionCases() =>
        AdminAppPermissionProfile.ManagerOversightViewPermissions.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(ManagerCashRegisterAdminPermissionCases))]
    public void Filter_Admin_Manager_PreservesCashRegisterAdminPermissions(string permission)
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        Assert.Contains(permission, effective);

        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.Contains(permission, result);
    }

    public static IEnumerable<object[]> ManagerCashRegisterAdminPermissionCases() =>
        AdminAppPermissionProfile.ManagerCashRegisterAdminPermissions.Select(p => new object[] { p });

    [Theory]
    [InlineData(AppPermissions.PaymentTake)]
    [InlineData(AppPermissions.SaleCreate)]
    [InlineData(AppPermissions.OrderUpdate)]
    [InlineData(AppPermissions.TseSign)]
    [InlineData(AppPermissions.RksvStartbelegCreate)]
    public void Filter_Admin_Manager_StripsOperationalWritePermissions(string writePermission)
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        if (!effective.Contains(writePermission))
            return;

        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.DoesNotContain(writePermission, result);
    }

    [Fact]
    public void Filter_Admin_Manager_PreservesPosReadPermissions_NotInWriteStrip()
    {
        var effective = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        var result = AdminAppPermissionProfile.Filter(
            ClientAppPolicy.Admin,
            new[] { Roles.Manager },
            effective);

        Assert.Contains(AppPermissions.CartView, result);
        Assert.Contains(AppPermissions.KitchenView, result);
        Assert.Contains(AppPermissions.PaymentView, result);
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

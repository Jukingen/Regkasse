using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// POS bootstrap / floor endpoints vs Cashier matrix — documents expected RBAC for Cashier 403 triage.
/// </summary>
public sealed class PosCashierEndpointPermissionContractTests
{
    public static IEnumerable<object[]> PosEndpointPermissionCases() =>
        new (string Route, string Permission)[]
        {
            ("GET /api/pos/catalog", AppPermissions.ProductView),
            ("GET /api/pos/all", AppPermissions.ProductView),
            ("GET /api/pos/cart/current", AppPermissions.CartManage),
            ("GET /api/pos/status/overview", AppPermissions.CartView),
            ("GET /api/pos/cash-register/selectable", AppPermissions.CartView),
            ("POST /api/pos/cash-register/ensure-ready", AppPermissions.CartView),
            ("GET /api/pos/company", AppPermissions.CartView),
            ("GET /api/pos/payment/methods", AppPermissions.PaymentView),
            ("POST /api/pos/payment", AppPermissions.PaymentTake),
            ("GET /api/pos/shift/current", AppPermissions.ShiftView),
            ("POST /api/pos/shift/start", AppPermissions.ShiftOpen),
            ("POST /api/pos/shift/end", AppPermissions.ShiftClose),
            ("GET /api/pos/customers", AppPermissions.CustomerView),
            ("POST /api/pos/vouchers/validate", AppPermissions.PaymentTake),
            ("POST /api/pos/storno", AppPermissions.PaymentCancel),
            ("GET /api/license/status", AppPermissions.LicenseView),
        }.Select(c => new object[] { c.Route, c.Permission });

    [Theory]
    [MemberData(nameof(PosEndpointPermissionCases))]
    public void Cashier_HasPermission_ForPosEndpoint(string route, string permission)
    {
        Assert.True(
            RolePermissionMatrix.RoleHasPermission(Roles.Cashier, permission),
            $"Cashier must have {permission} for {route}");
    }
}

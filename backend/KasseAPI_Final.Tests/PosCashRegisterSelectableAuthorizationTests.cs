using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
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

    /// <summary>
    /// Guards the reason the picker filter moved off <see cref="AppPermissions.CashRegisterView"/> and onto
    /// <c>cash_registers.assigned_user_id</c> plus a Super Admin bypass: both POS roles hold that permission, so it can
    /// never distinguish "sees every register" from "sees only mine". If this ever fails, revisit
    /// <c>CashRegisterResolutionService.ListSelectableRegistersAsync</c> before changing the assertion.
    /// </summary>
    [Fact]
    public void CashRegisterView_IsNotAnAssignmentBypass_BecauseCashierAndWaiterBothHaveIt()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.CashRegisterView));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.CashRegisterView));
    }

    /// <summary>
    /// The POS picker matches <c>status</c> by name to decide whether a row needs opening on pick. The API has no global
    /// string-enum converter, so without the property-level converter this would serialize as an ordinal and the client
    /// would silently stop recognizing closed rows.
    /// </summary>
    [Fact]
    public void SelectableRow_SerializesStatusByName()
    {
        var json = JsonSerializer.Serialize(
            new CashRegisterSelectableRow
            {
                Id = Guid.NewGuid(),
                RegisterNumber = "K-1",
                Status = RegisterStatus.Closed,
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status");

        Assert.Equal(JsonValueKind.String, status.ValueKind);
        Assert.Equal("Closed", status.GetString());
    }

    [Fact]
    public void SelectableRow_SerializesAssignedUserId()
    {
        var json = JsonSerializer.Serialize(
            new CashRegisterSelectableRow
            {
                Id = Guid.NewGuid(),
                RegisterNumber = "K-1",
                Status = RegisterStatus.Open,
                AssignedUserId = "cashier-1",
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("cashier-1", doc.RootElement.GetProperty("assignedUserId").GetString());
    }
}

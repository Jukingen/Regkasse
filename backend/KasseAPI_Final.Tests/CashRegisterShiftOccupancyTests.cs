using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Pin the shared occupancy predicates used by picker, sole auto-assign, ensure-ready, payment, and (when no CashRegisterView) manual assignment.
/// </summary>
public class CashRegisterShiftOccupancyTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("u1", false)]
    [InlineData("u2", true)]
    public void IsHeldByOtherUser_MatchesExpected(string? occupant, bool expectedConflict)
    {
        Assert.Equal(expectedConflict, CashRegisterShiftOccupancy.IsHeldByOtherUser("u1", occupant));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("u1", true)]
    [InlineData("u2", false)]
    public void UserMayOperateOpenRegisterShift_IsComplementOfConflictForOpenRows(string? occupant, bool mayUse)
    {
        Assert.Equal(mayUse, CashRegisterShiftOccupancy.UserMayOperateOpenRegisterShift("u1", occupant));
        Assert.NotEqual(mayUse, CashRegisterShiftOccupancy.IsHeldByOtherUser("u1", occupant));
    }

    [Fact]
    public void MayAssignRegisterWithoutCashRegisterView_Sole_AllowsUnclaimedOrSelf()
    {
        var unclaimed = new CashRegister { CurrentUserId = null };
        var self = new CashRegister { CurrentUserId = "u1" };
        var other = new CashRegister { CurrentUserId = "u2" };

        Assert.True(CashRegisterShiftOccupancy.MayAssignRegisterWithoutCashRegisterView("u1", unclaimed, operationalRegisterCountForPos: 1));
        Assert.True(CashRegisterShiftOccupancy.MayAssignRegisterWithoutCashRegisterView("u1", self, operationalRegisterCountForPos: 1));
        Assert.False(CashRegisterShiftOccupancy.MayAssignRegisterWithoutCashRegisterView("u1", other, operationalRegisterCountForPos: 1));
    }

    [Fact]
    public void MayAssignRegisterWithoutCashRegisterView_Multi_RequiresNonEmptySelfShift()
    {
        var unclaimed = new CashRegister { CurrentUserId = null };
        var self = new CashRegister { CurrentUserId = "u1" };

        Assert.False(CashRegisterShiftOccupancy.MayAssignRegisterWithoutCashRegisterView("u1", unclaimed, operationalRegisterCountForPos: 2));
        Assert.True(CashRegisterShiftOccupancy.MayAssignRegisterWithoutCashRegisterView("u1", self, operationalRegisterCountForPos: 2));
    }
}

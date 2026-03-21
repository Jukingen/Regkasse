using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class CashRegisterPosOperationalCardinalityTests
{
    private static CashRegister Reg(RegisterStatus status, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        RegisterNumber = "R",
        Location = "L",
        StartingBalance = 0,
        CurrentBalance = 0,
        LastBalanceUpdate = DateTime.UtcNow,
        Status = status,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void CountsToward_IncludesOpenAndClosedActive_ExcludesMaintenanceDisabledInactive()
    {
        Assert.True(CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(Reg(RegisterStatus.Open)));
        Assert.True(CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(Reg(RegisterStatus.Closed)));
        Assert.False(CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(Reg(RegisterStatus.Maintenance)));
        Assert.False(CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(Reg(RegisterStatus.Disabled)));
        Assert.False(CashRegisterPosOperationalCardinality.CountsTowardPosOperationalCardinality(Reg(RegisterStatus.Closed, isActive: false)));
    }

    [Fact]
    public void GetSingle_ReturnsNull_WhenZeroOrMultipleOperational()
    {
        var open = Reg(RegisterStatus.Open);
        var closed = Reg(RegisterStatus.Closed);
        Assert.Null(CashRegisterPosOperationalCardinality.GetSingleOperationalRegisterOrNull(Array.Empty<CashRegister>()));
        Assert.Equal(open, CashRegisterPosOperationalCardinality.GetSingleOperationalRegisterOrNull(new[] { open, Reg(RegisterStatus.Disabled) }));
        Assert.Null(CashRegisterPosOperationalCardinality.GetSingleOperationalRegisterOrNull(new[] { open, closed }));
    }
}

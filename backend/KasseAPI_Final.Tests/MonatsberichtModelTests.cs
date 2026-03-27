using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Monatsbericht sabitleri ve FinanzOnline mesaj tipi — regresyon için hafif kontroller.
/// </summary>
public class MonatsberichtModelTests
{
    [Fact]
    public void FinanzOnlineMonatsberichtMessageTypes_Matches_Outbox_Branch()
    {
        Assert.Equal("MonatsberichtMonthlySummary", FinanzOnlineMonatsberichtMessageTypes.MonatsberichtMonthlySummary);
    }

    [Fact]
    public void MonatsberichtScopeKinds_Distinct_Register_Company()
    {
        Assert.NotEqual(MonatsberichtScopeKinds.Register, MonatsberichtScopeKinds.Company);
    }
}

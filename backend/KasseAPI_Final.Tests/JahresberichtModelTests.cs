using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Jahresbericht sabitleri ve mesaj tipi için temel regresyon kontrolleri.
/// </summary>
public class JahresberichtModelTests
{
    [Fact]
    public void FinanzOnlineJahresberichtMessageTypes_Matches_Outbox_Branch()
    {
        Assert.Equal("JahresberichtAnnualSummary", FinanzOnlineJahresberichtMessageTypes.JahresberichtAnnualSummary);
    }
}

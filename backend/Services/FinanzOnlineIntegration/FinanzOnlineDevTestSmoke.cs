using System.Linq;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Unit testlerdeki DEP benzeri örnek ile aynı: <see cref="FinanzOnlineRkdbBelegpruefungMappingTests"/> — gerçek fiş verisi değildir.
/// </summary>
public static class FinanzOnlineDevTestSmoke
{
    /// <summary>BMF DEP desenine uyan, sabit uzunlukta test <c>beleg</c> metni.</summary>
    public static string BuildSyntheticDepBeleg()
    {
        return string.Concat(Enumerable.Range(0, 13).Select(i => $"_{i:D8}xxxxxxxx"));
    }
}

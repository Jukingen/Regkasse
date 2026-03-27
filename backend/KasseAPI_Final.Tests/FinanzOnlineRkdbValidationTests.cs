using System.Linq;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineRkdbValidationTests
{
    private static string BuildDepLikeString() =>
        string.Concat(Enumerable.Range(0, 13).Select(i => $"_{i:D8}xxxxxxxx"));

    [Fact]
    public void StatusKasseQueryContext_Rejects_Empty_RegisterId()
    {
        var err = FinanzOnlineStatusKasseQueryContextValidator.ValidateForStatusKasse(new FinanzOnlineTransmissionStatusQueryRequest
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { RegisterId = "" },
            Correlation = new FinanzOnlineCorrelationContext(),
            TransmissionId = "1",
            RkdbTsErstellungIso = "2025-03-26T12:00:00.000Z",
            RkdbSatzNr = 1
        });
        Assert.Contains(err, e => e.Contains("RegisterId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InnerXmlStructureValidator_Accepts_Builder_Output()
    {
        var ns = "https://finanzonline.bmf.gv.at/rkdb";
        var beleg = BuildDepLikeString();
        var xml = FinanzOnlineRkdbBelegpruefungXmlBuilder.Build(ns, new FinanzOnlineRkdbBelegpruefungCommand
        {
            Beleg = beleg,
            PaketNr = 1,
            SatzNr = 1
        });
        var errs = FinanzOnlineRkdbInnerXmlStructureValidator.ValidateBelegpruefungDocument(xml, ns);
        Assert.Empty(errs);
    }

    [Fact]
    public void InnerXmlStructureValidator_Rejects_Wrong_Root()
    {
        var errs = FinanzOnlineRkdbInnerXmlStructureValidator.ValidateBelegpruefungDocument("<foo/>", "https://finanzonline.bmf.gv.at/rkdb");
        Assert.NotEmpty(errs);
    }
}

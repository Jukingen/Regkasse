using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineRkdbAusfallMappingTests
{
    private const string Ns = "https://finanzonline.bmf.gv.at/rkdb";

    [Fact]
    public void XmlBuilder_AusfallSe_ContainsRequiredElements()
    {
        var cmd = new FinanzOnlineRkdbAusfallCommand
        {
            EpisodeType = RksvAusfallEpisodeTypes.Scu,
            OperationKind = RksvAusfallOperationKinds.Ausfall,
            CertificateSerial = "CERT-123",
            Begruendung = RksvAusfallBegruendungCodes.HardwareDefect,
            BeginnAusfallUtc = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            PaketNr = 2,
            SatzNr = 3,
            TsErstellungUtc = new DateTimeOffset(2026, 7, 1, 10, 5, 0, TimeSpan.Zero),
        };

        Assert.Empty(FinanzOnlineRkdbAusfallValidator.Validate(cmd));
        var xml = FinanzOnlineRkdbAusfallXmlBuilder.Build(Ns, cmd);
        Assert.Contains("ausfall_se", xml, StringComparison.Ordinal);
        Assert.Contains("zertifikatsseriennummer", xml, StringComparison.Ordinal);
        Assert.Contains("CERT-123", xml, StringComparison.Ordinal);
        Assert.Contains("begruendung", xml, StringComparison.Ordinal);
        Assert.Contains(RksvAusfallBegruendungCodes.HardwareDefect, xml, StringComparison.Ordinal);
        Assert.Contains("beginn_ausfall", xml, StringComparison.Ordinal);
        Assert.Contains("2026-07-01T10:00:00.000Z", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("password", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void XmlBuilder_WiederinbetriebnahmeSe_ContainsEndeAusfall()
    {
        var cmd = new FinanzOnlineRkdbAusfallCommand
        {
            EpisodeType = RksvAusfallEpisodeTypes.Scu,
            OperationKind = RksvAusfallOperationKinds.Wiederinbetriebnahme,
            CertificateSerial = "CERT-9",
            Begruendung = RksvAusfallBegruendungCodes.Other,
            EndeAusfallUtc = new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero),
        };

        Assert.Empty(FinanzOnlineRkdbAusfallValidator.Validate(cmd));
        var xml = FinanzOnlineRkdbAusfallXmlBuilder.Build(Ns, cmd);
        Assert.Contains("wiederinbetriebnahme_se", xml, StringComparison.Ordinal);
        Assert.Contains("ende_ausfall", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("ausfall_se", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void XmlBuilder_AusfallKasse_UsesKassenId()
    {
        var cmd = new FinanzOnlineRkdbAusfallCommand
        {
            EpisodeType = RksvAusfallEpisodeTypes.Kasse,
            OperationKind = RksvAusfallOperationKinds.Ausfall,
            KassenIdentifikationsnummer = "KASSE-001",
            Begruendung = RksvAusfallBegruendungCodes.PlannedMaintenance,
            BeginnAusfallUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
        };

        Assert.Empty(FinanzOnlineRkdbAusfallValidator.Validate(cmd));
        var xml = FinanzOnlineRkdbAusfallXmlBuilder.Build(Ns, cmd);
        Assert.Contains("ausfall_kasse", xml, StringComparison.Ordinal);
        Assert.Contains("kassenidentifikationsnummer", xml, StringComparison.Ordinal);
        Assert.Contains("KASSE-001", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RejectsFutureBeginn()
    {
        var cmd = new FinanzOnlineRkdbAusfallCommand
        {
            EpisodeType = RksvAusfallEpisodeTypes.Scu,
            OperationKind = RksvAusfallOperationKinds.Ausfall,
            CertificateSerial = "C",
            Begruendung = RksvAusfallBegruendungCodes.Other,
            BeginnAusfallUtc = DateTimeOffset.UtcNow.AddHours(2),
        };
        var errors = FinanzOnlineRkdbAusfallValidator.Validate(cmd);
        Assert.Contains(errors, e => e.Contains("beginn_ausfall", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultMapper_Sets_RkdbPayloadXml_For_Ausfall()
    {
        var monitor = new TestOptionsMonitor<FinanzOnlineRegistrierkassenOptions>(
            new FinanzOnlineRegistrierkassenOptions { SoapNamespace = Ns });
        var mapper = new DefaultFinanzOnlineCommandMapper(monitor);
        var mapped = mapper.MapRegisterSubmission(new FinanzOnlineRegisterSubmissionRequest
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { RegisterId = "R1" },
            Correlation = new FinanzOnlineCorrelationContext { BusinessKey = "bk", PayloadHash = "h", CorrelationId = "c" },
            RkdbAusfall = new FinanzOnlineRkdbAusfallCommand
            {
                EpisodeType = RksvAusfallEpisodeTypes.Scu,
                OperationKind = RksvAusfallOperationKinds.Ausfall,
                CertificateSerial = "SERIAL",
                Begruendung = RksvAusfallBegruendungCodes.NetworkOutage,
                BeginnAusfallUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
        });
        Assert.Null(mapped.RkdbBuildError);
        Assert.NotNull(mapped.RkdbPayloadXml);
        Assert.Contains("ausfall_se", mapped.RkdbPayloadXml, StringComparison.Ordinal);
    }

    private sealed class TestOptionsMonitor<T> : Microsoft.Extensions.Options.IOptionsMonitor<T> where T : class
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}

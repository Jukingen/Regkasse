using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineRkdbBelegpruefungMappingTests
{
    private static string BuildDepLikeString()
    {
        return string.Concat(Enumerable.Range(0, 13).Select(i => $"_{i:D8}xxxxxxxx"));
    }

    [Fact]
    public void DevTestSmoke_BuildSyntheticDepBeleg_Matches_UnitTestPattern()
    {
        var a = BuildDepLikeString();
        var b = FinanzOnlineDevTestSmoke.BuildSyntheticDepBeleg();
        Assert.Equal(a, b);
        Assert.True(FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(b));
    }

    [Fact]
    public void Validator_Accepts_DepPattern_And_Length()
    {
        var beleg = BuildDepLikeString();
        Assert.True(FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(beleg));
        var cmd = new FinanzOnlineRkdbBelegpruefungCommand { Beleg = beleg, PaketNr = 1, SatzNr = 1 };
        Assert.Empty(FinanzOnlineRkdbBelegpruefungValidator.Validate(cmd));
    }

    [Fact]
    public void XmlBuilder_Contains_Rkdb_And_Belegpruefung()
    {
        var beleg = BuildDepLikeString();
        var cmd = new FinanzOnlineRkdbBelegpruefungCommand
        {
            Beleg = beleg,
            PaketNr = 2,
            SatzNr = 3,
            TsErstellungUtc = new DateTimeOffset(2025, 3, 26, 12, 0, 0, TimeSpan.Zero)
        };
        var xml = FinanzOnlineRkdbBelegpruefungXmlBuilder.Build("https://finanzonline.bmf.gv.at/rkdb", cmd);
        Assert.Contains("belegpruefung", xml, StringComparison.Ordinal);
        Assert.Contains("paket_nr", xml, StringComparison.Ordinal);
        Assert.Contains("ts_erstellung", xml, StringComparison.Ordinal);
        Assert.Contains("2025-03-26T12:00:00.000Z", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultMapper_Sets_RkdbPayloadXml_For_Test_Belegpruefung()
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new FinanzOnlineRegistrierkassenOptions
        {
            SoapNamespace = "https://finanzonline.bmf.gv.at/rkdb"
        });
        var monitor = new TestOptionsMonitor<FinanzOnlineRegistrierkassenOptions>(opts.Value);
        var mapper = new DefaultFinanzOnlineCommandMapper(monitor);
        var beleg = BuildDepLikeString();
        var mapped = mapper.MapRegisterSubmission(new FinanzOnlineRegisterSubmissionRequest
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { RegisterId = "R1" },
            Correlation = new FinanzOnlineCorrelationContext { BusinessKey = "bk", PayloadHash = "h", CorrelationId = "c" },
            RkdbBelegpruefung = new FinanzOnlineRkdbBelegpruefungCommand { Beleg = beleg, PaketNr = 1, SatzNr = 1 }
        });
        Assert.Null(mapped.RkdbBuildError);
        Assert.NotNull(mapped.RkdbPayloadXml);
        Assert.Contains("<rkdb", mapped.RkdbPayloadXml, StringComparison.Ordinal);
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

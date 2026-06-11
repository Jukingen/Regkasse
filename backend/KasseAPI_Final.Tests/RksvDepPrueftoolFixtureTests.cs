using KasseAPI_Final.Tests.Fixtures;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace KasseAPI_Final.Tests;

public sealed class RksvDepPrueftoolFixtureTests
{
    private static string FixtureDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tests", "fixtures", "prueftool"));

    private readonly ITestOutputHelper? _output;

    public RksvDepPrueftoolFixtureTests(ITestOutputHelper? output = null) => _output = output;

    [Fact]
    public void GenerateFixtures_WritesDepExportAndCryptoMaterial()
    {
        var paths = RksvDepPrueftoolFixtureGenerator.Generate(FixtureDirectory);

        Assert.True(File.Exists(paths.DepExportPath));
        Assert.True(File.Exists(paths.CryptoMaterialPath));
        Assert.Equal(3, paths.ReceiptCount);

        var dep = File.ReadAllText(paths.DepExportPath);
        Assert.Contains("\"Belege-Gruppe\"", dep, StringComparison.Ordinal);
        Assert.Contains("\"Belege-kompakt\"", dep, StringComparison.Ordinal);

        var crypto = File.ReadAllText(paths.CryptoMaterialPath);
        Assert.Contains("\"base64AESKey\"", crypto, StringComparison.Ordinal);
        Assert.Contains("\"certificateOrPublicKeyMap\"", crypto, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedFixtures_ContainValidSelfSignedJwsChain()
    {
        RksvDepPrueftoolFixtureGenerator.Generate(FixtureDirectory);

        var dep = File.ReadAllText(Path.Combine(FixtureDirectory, "dep-export.json"));
        var keyProvider = new FixedPrueftoolTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);

        foreach (var jws in ExtractCompactJwss(dep))
        {
            var parts = jws.Split('.');
            Assert.Equal(3, parts.Length);
            Assert.Equal("eyJhbGciOiJFUzI1NiJ9", parts[0]);
            Assert.True(pipeline.Verify(jws, keyProvider.GetPublicKey()));
        }
    }

    [Fact(Skip = "Run manually to rotate embedded PKCS#8: dotnet test --filter DumpFixturePkcs8")]
    public void DumpFixturePkcs8()
    {
        var pkcs8 = RksvDepPrueftoolFixtureGenerator.DumpNewFixturePkcs8();
        _output?.WriteLine(pkcs8);
    }

    private static IEnumerable<string> ExtractCompactJwss(string depJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(depJson);
        foreach (var group in doc.RootElement.GetProperty("Belege-Gruppe").EnumerateArray())
        {
            foreach (var jws in group.GetProperty("Belege-kompakt").EnumerateArray())
                yield return jws.GetString()!;
        }
    }
}

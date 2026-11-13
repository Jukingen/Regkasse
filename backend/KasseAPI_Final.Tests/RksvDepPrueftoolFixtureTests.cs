using KasseAPI_Final.Tests.Fixtures;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Generator contract for the BMF Prüftool fixtures.
///
/// A plain test run generates into a throwaway directory, so the committed files under
/// <c>backend/Tests/fixtures/prueftool</c> stay untouched and the working tree stays clean. Set
/// <c>REGKASSE_UPDATE_BASELINE=1</c> to rewrite them — that is the documented regeneration path
/// (see the fixtures README). The committed content itself is guarded by
/// <see cref="RksvDepPrueftoolCiSmokeTests"/>, which runs the BMF tool against it.
///
/// The fixtures cannot be byte-compared against a fresh run: ES256 draws a new nonce per signature,
/// so the JWS signature segment differs every time. This suite therefore verifies structure and
/// signatures cryptographically instead.
/// </summary>
[Collection(PrueftoolFixtureCollection.Name)]
public sealed class RksvDepPrueftoolFixtureTests
{
    private readonly ITestOutputHelper? _output;

    public RksvDepPrueftoolFixtureTests(ITestOutputHelper? output = null) => _output = output;

    [Fact]
    public void GenerateFixtures_DoesNotRewriteCommittedFiles_WhenUpdateBaselineIsOff()
    {
        if (PrueftoolFixtureLocations.ShouldRegenerateCommittedFixtures())
            return;

        var committed = Path.GetFullPath(PrueftoolFixtureLocations.CommittedDirectory);
        var names = new[] { "dep-export.json", "crypto-material.json", "qr-code-rep.json" };
        foreach (var name in names)
            Assert.True(File.Exists(Path.Combine(committed, name)), $"Missing committed fixture: {name}");
        var before = names.ToDictionary(
            name => name,
            name => File.ReadAllBytes(Path.Combine(committed, name)));

        RunAgainstGeneratedFixtures(paths =>
        {
            var generatedDir = Path.GetFullPath(Path.GetDirectoryName(paths.DepExportPath)!);
            Assert.False(
                string.Equals(generatedDir, committed, StringComparison.OrdinalIgnoreCase),
                "Without REGKASSE_UPDATE_BASELINE the generator must not write into the committed fixture directory.");
        });

        foreach (var name in names)
            Assert.Equal(before[name], File.ReadAllBytes(Path.Combine(committed, name)));
    }

    [Fact]
    public void GenerateFixtures_WritesDepExportAndCryptoMaterial()
    {
        RunAgainstGeneratedFixtures(paths =>
        {
            Assert.True(File.Exists(paths.DepExportPath));
            Assert.True(File.Exists(paths.CryptoMaterialPath));
            Assert.True(File.Exists(paths.QrCodeRepPath));
            Assert.Equal(3, paths.ReceiptCount);

            var dep = File.ReadAllText(paths.DepExportPath);
            Assert.Contains("\"Belege-Gruppe\"", dep, StringComparison.Ordinal);
            Assert.Contains("\"Belege-kompakt\"", dep, StringComparison.Ordinal);

            var crypto = File.ReadAllText(paths.CryptoMaterialPath);
            Assert.Contains("\"base64AESKey\"", crypto, StringComparison.Ordinal);
            Assert.Contains("\"certificateOrPublicKeyMap\"", crypto, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void GeneratedFixtures_ContainValidSelfSignedJwsChain()
    {
        RunAgainstGeneratedFixtures(paths =>
        {
            var dep = File.ReadAllText(paths.DepExportPath);
            var keyProvider = new FixedPrueftoolTseKeyProvider();
            var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);

            foreach (var jws in ExtractCompactJwss(dep))
            {
                var parts = jws.Split('.');
                Assert.Equal(3, parts.Length);
                Assert.Equal("eyJhbGciOiJFUzI1NiJ9", parts[0]);
                Assert.True(pipeline.Verify(jws, keyProvider.GetPublicKey()));
            }
        });
    }

    [Fact(Skip = "Run manually to rotate embedded PKCS#8: dotnet test --filter DumpFixturePkcs8")]
    public void DumpFixturePkcs8()
    {
        var pkcs8 = RksvDepPrueftoolFixtureGenerator.DumpNewFixturePkcs8();
        _output?.WriteLine(pkcs8);
    }

    /// <summary>
    /// Generates the fixtures into the committed directory when regeneration is requested, otherwise
    /// into a temp directory that is removed afterwards.
    /// </summary>
    private static void RunAgainstGeneratedFixtures(
        Action<RksvDepPrueftoolFixtureGenerator.PrueftoolFixturePaths> assert)
    {
        var regenerate = PrueftoolFixtureLocations.ShouldRegenerateCommittedFixtures();
        var outputDirectory = regenerate
            ? PrueftoolFixtureLocations.CommittedDirectory
            : PrueftoolFixtureLocations.CreateTempDirectory("dep-fixtures");

        try
        {
            assert(RksvDepPrueftoolFixtureGenerator.Generate(outputDirectory));
        }
        finally
        {
            if (!regenerate)
                PrueftoolFixtureLocations.TryDelete(outputDirectory);
        }
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

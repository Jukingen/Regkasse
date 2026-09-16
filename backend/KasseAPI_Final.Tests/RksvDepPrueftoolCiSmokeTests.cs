using KasseAPI_Final.Tests.Fixtures;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// CI smoke: committed BMF Prüftool fixtures must PASS CheckDEPExportFormat when JARs + JDK 17+ are present.
/// </summary>
[Collection(PrueftoolFixtureCollection.Name)]
public sealed class RksvDepPrueftoolCiSmokeTests
{
    private static string FixtureDirectory => PrueftoolFixtureLocations.CommittedDirectory;

    [SkippableFact]
    [Trait("Category", "DepPrueftool")]
    public void CommittedFixtures_PassBmfCheckDepExport_WhenPrueftoolInstalled()
    {
        Skip.IfNot(
            PrueftoolDepVerificationHelper.IsDepVerificationAvailable(out var skipReason),
            skipReason ?? "Prüftool not available.");

        var depPath = Path.Combine(FixtureDirectory, "dep-export.json");
        var cryptoPath = Path.Combine(FixtureDirectory, "crypto-material.json");
        Assert.True(File.Exists(depPath), $"Missing committed fixture: {depPath}");
        Assert.True(File.Exists(cryptoPath), $"Missing committed fixture: {cryptoPath}");

        var outputDir = Path.Combine(Path.GetTempPath(), $"regkasse-dep-ci-fixtures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        try
        {
            var result = PrueftoolDepVerificationHelper.RunCheckDepExport(depPath, cryptoPath, outputDir);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("PASS", result.VerificationState);
        }
        finally
        {
            try { Directory.Delete(outputDir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}

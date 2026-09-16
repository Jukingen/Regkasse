using Xunit;

namespace KasseAPI_Final.Tests.Fixtures;

/// <summary>
/// Resolves the committed BMF Prüftool fixture directory and hands out throwaway directories for tests
/// that only need freshly generated files.
///
/// The committed files are real artifacts: <c>scripts/verify-rksv-dep-export.ps1 -UseFixtures</c> and
/// <c>scripts/verify-rksv-receipt-qr.ps1 -UseFixtures</c> read them, and
/// <c>RksvDepPrueftoolCiSmokeTests</c> asserts they still pass the BMF tool. Only the documented
/// regenerator (<c>RksvDepPrueftoolFixtureTests</c>) may write into them.
/// </summary>
internal static class PrueftoolFixtureLocations
{
    /// <summary>
    /// Same switch as the country baseline suite: set it to <c>1</c> to rewrite committed fixtures.
    /// </summary>
    public const string UpdateEnvironmentVariable = CountryBaseline.BaselineFixtureFile.UpdateEnvironmentVariable;

    /// <summary>Committed fixtures under <c>backend/Tests/fixtures/prueftool</c>.</summary>
    public static string CommittedDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tests", "fixtures", "prueftool"));

    /// <summary>
    /// True only when the regeneration switch is set. A plain test run must leave the committed
    /// fixtures untouched, so it must not dirty the working tree either.
    /// </summary>
    public static bool ShouldRegenerateCommittedFixtures()
    {
        var raw = Environment.GetEnvironmentVariable(UpdateEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(raw)
               && (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Fresh directory under the temp path. Caller deletes it; failure to delete is not a test failure.</summary>
    public static string CreateTempDirectory(string purpose)
    {
        var path = Path.Combine(Path.GetTempPath(), $"regkasse-prueftool-{purpose}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Best effort: a leftover temp directory must not fail a test.
        }
    }
}

/// <summary>
/// Serializes every test class that touches the committed Prüftool fixture directory. Without this,
/// xUnit runs the classes in parallel and the regenerator's writes race the readers' reads.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PrueftoolFixtureCollection
{
    public const string Name = "PrueftoolCommittedFixtures";
}

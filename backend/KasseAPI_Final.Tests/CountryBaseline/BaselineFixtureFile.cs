using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace KasseAPI_Final.Tests.CountryBaseline;

/// <summary>
/// Reads and byte-compares committed golden-master fixtures for the country baseline suites.
/// Set <c>REGKASSE_UPDATE_BASELINE=1</c> to rewrite a fixture after an intentional behaviour change.
/// </summary>
internal static class BaselineFixtureFile
{
    internal const string UpdateEnvironmentVariable = "REGKASSE_UPDATE_BASELINE";

    internal static JsonNode Read(string fixtureFileName)
    {
        var fixturePath = Path.Combine(ResolveFixtureDirectory(), fixtureFileName);
        var text = File.ReadAllText(fixturePath).ReplaceLineEndings("\n");
        return JsonNode.Parse(text)
            ?? throw new InvalidOperationException($"Fixture '{fixtureFileName}' parsed to null.");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>Stable text form: indented JSON, LF line endings, trailing newline.</summary>
    internal static string Serialize(JsonNode node) =>
        node.ToJsonString(SerializerOptions).ReplaceLineEndings("\n") + "\n";

    internal static void AssertMatchesFixture(string fixtureFileName, JsonNode actual)
    {
        var actualText = Serialize(actual);
        var fixturePath = Path.Combine(ResolveFixtureDirectory(), fixtureFileName);

        if (ShouldUpdate())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
            File.WriteAllText(fixturePath, actualText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(
            File.Exists(fixturePath),
            $"Baseline fixture '{fixtureFileName}' is missing at '{fixturePath}'. "
            + $"Capture it once with {UpdateEnvironmentVariable}=1 and commit the file.");

        var expectedText = File.ReadAllText(fixturePath).ReplaceLineEndings("\n");
        if (string.Equals(expectedText, actualText, StringComparison.Ordinal))
            return;

        var actualPath = Path.ChangeExtension(fixturePath, ".actual.json");
        File.WriteAllText(actualPath, actualText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.Fail(
            $"Austrian fiscal baseline drifted from '{fixtureFileName}'.{Environment.NewLine}"
            + $"First difference: {DescribeFirstDifference(expectedText, actualText)}{Environment.NewLine}"
            + $"Actual output written to '{actualPath}' — diff it against the committed fixture.{Environment.NewLine}"
            + "If the change is intentional and fiscally reviewed, re-capture with "
            + $"{UpdateEnvironmentVariable}=1 and explain the diff in the PR description.");
    }

    private static bool ShouldUpdate()
    {
        var raw = Environment.GetEnvironmentVariable(UpdateEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(raw)
               && (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeFirstDifference(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var shared = Math.Min(expectedLines.Length, actualLines.Length);

        for (var i = 0; i < shared; i++)
        {
            if (string.Equals(expectedLines[i], actualLines[i], StringComparison.Ordinal))
                continue;

            return $"line {i + 1}{Environment.NewLine}"
                   + $"  expected: {expectedLines[i]}{Environment.NewLine}"
                   + $"  actual:   {actualLines[i]}";
        }

        return $"line count {expectedLines.Length} (expected) vs {actualLines.Length} (actual)";
    }

    private static string ResolveFixtureDirectory([CallerFilePath] string callerFilePath = "")
    {
        // Source-tree location: <tests>/CountryBaseline/BaselineFixtureFile.cs
        var countryBaselineDirectory = Path.GetDirectoryName(callerFilePath);
        var testProjectDirectory = countryBaselineDirectory == null
            ? null
            : Path.GetDirectoryName(countryBaselineDirectory);

        if (testProjectDirectory != null && Directory.Exists(testProjectDirectory))
            return Path.Combine(testProjectDirectory, "Fixtures", "CountryBaseline");

        return Path.Combine(ResolveTestProjectRootFromOutput(), "Fixtures", "CountryBaseline");
    }

    private static string ResolveTestProjectRootFromOutput()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.Tests.csproj")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}

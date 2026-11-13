using System.Text.Json;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// The CH QR known-gap fixture is the list of print features that are still missing.
/// A hit in production code means the fixture must be updated before the gap is treated as closed.
/// </summary>
public sealed class ChQrKnownGapsTests
{
    private static readonly IReadOnlyDictionary<string, GapProbe> Probes =
        new Dictionary<string, GapProbe>(StringComparer.Ordinal)
        {
            ["official-swiss-cross"] = new(QrOnly: true, ["7 mm", "7mm", "white border"]),
            ["font-embedding-liberation-arial"] = new(QrOnly: true, ["LiberationSans", "Liberation Sans", "FontFamily(\"Arial\""]),
            ["pain001"] = new(QrOnly: false, ["pain.001", "Pain001", "pain001"]),
            ["bank-scan"] = new(QrOnly: false, ["BankScan", "bank scan"]),
            ["perforation-line"] = new(QrOnly: true, ["perforation", "Perforation"]),
        };

    [Fact]
    public void OpenGaps_HaveNoProductionImplementation()
    {
        var fixture = LoadFixture();
        var ids = fixture.Gaps.Select(gap => gap.Id).ToArray();
        Assert.Equal(Probes.Keys.OrderBy(id => id, StringComparer.Ordinal), ids.OrderBy(id => id, StringComparer.Ordinal));

        var backendRoot = FindBackendRoot();
        var sources = ReadProductionSources(backendRoot).ToArray();
        var offenders = new List<string>();

        foreach (var gap in fixture.Gaps)
        {
            Assert.True(Probes.ContainsKey(gap.Id), $"Fixture gap '{gap.Id}' has no probe.");
            var probe = Probes[gap.Id];
            var hits = sources
                .Where(source => !probe.QrOnly || source.Path.Contains(
                    $"{Path.DirectorySeparatorChar}QrRechnung{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
                .SelectMany(source => probe.Markers
                    .Where(marker => source.Text.Contains(marker, StringComparison.Ordinal))
                    .Select(marker => $"{Relative(backendRoot, source.Path)} contains `{marker}`"))
                .ToArray();

            if (!gap.Present && hits.Length > 0)
                offenders.Add($"{gap.Id} is marked missing but has an implementation:{Environment.NewLine}{string.Join(Environment.NewLine, hits)}");
            if (gap.Present && hits.Length == 0)
                offenders.Add($"{gap.Id} is marked present but no implementation marker was found.");
        }

        Assert.True(
            offenders.Count == 0,
            "CH QR known gaps are listed in Services/Countries/QrRechnung/ChQrKnownGaps.json. "
            + "Update that fixture and docs/FISCAL_SWITZERLAND.md together when a gap is closed."
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static GapFile LoadFixture()
    {
        var path = Path.Combine(FindBackendRoot(), "Services", "Countries", "QrRechnung", "ChQrKnownGaps.json");
        Assert.True(File.Exists(path), path);
        var file = JsonSerializer.Deserialize<GapFile>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        Assert.NotNull(file);
        Assert.NotEmpty(file!.Gaps);
        return file;
    }

    private static IEnumerable<(string Path, string Text)> ReadProductionSources(string backendRoot) =>
        Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains("KasseAPI_Final.Tests", StringComparison.Ordinal))
            .Select(path => (path, File.ReadAllText(path)));

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path);

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private sealed record GapProbe(bool QrOnly, string[] Markers);

    private sealed class GapFile
    {
        public List<GapRow> Gaps { get; set; } = [];
    }

    private sealed class GapRow
    {
        public string Id { get; set; } = string.Empty;

        public bool Present { get; set; }
    }
}

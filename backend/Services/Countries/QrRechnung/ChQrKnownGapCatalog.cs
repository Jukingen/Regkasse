using System.Text.Json;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Open print gaps for the Swiss QR-Rechnung PDF. The embedded JSON is the only list.
/// A gap id that is not in this file is not a valid acceptance entry.
/// </summary>
public sealed class ChQrKnownGapCatalog
{
    public const string ResourceName = "KasseAPI_Final.Services.Countries.QrRechnung.ChQrKnownGaps.json";

    public ChQrKnownGapCatalog()
    {
        Gaps = Load();
        Ids = Gaps.Select(gap => gap.Id).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<ChQrKnownGap> Gaps { get; }

    public IReadOnlySet<string> Ids { get; }

    private static IReadOnlyList<ChQrKnownGap> Load()
    {
        var assembly = typeof(ChQrKnownGapCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        var file = JsonSerializer.Deserialize<GapFile>(reader.ReadToEnd(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        if (file?.Gaps is null || file.Gaps.Count == 0)
            throw new InvalidOperationException("CH QR known-gap catalog is empty.");

        var gaps = file.Gaps
            .Select(row => new ChQrKnownGap(row.Id ?? string.Empty, row.Present))
            .ToArray();
        if (gaps.Any(gap => string.IsNullOrWhiteSpace(gap.Id)))
            throw new InvalidOperationException("CH QR known-gap catalog contains an empty id.");

        return gaps;
    }

    private sealed class GapFile
    {
        public List<GapRow> Gaps { get; set; } = [];
    }

    private sealed class GapRow
    {
        public string? Id { get; set; }

        public bool Present { get; set; }
    }
}

/// <summary>One row from the CH QR known-gap catalog. Present false means the print feature is still missing.</summary>
public sealed record ChQrKnownGap(string Id, bool Present);

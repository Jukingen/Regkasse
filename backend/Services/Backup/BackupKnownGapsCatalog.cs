using System.Text.Json;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Versioned Backup/DR known-gap catalog. The embedded JSON is the only list.
/// An id that is not in this file cannot be accepted.
/// </summary>
public interface IBackupKnownGapsCatalog
{
    int Version { get; }

    IReadOnlyList<BackupKnownGapDefinition> Gaps { get; }

    IReadOnlySet<string> Ids { get; }
}

public sealed class BackupKnownGapsCatalog : IBackupKnownGapsCatalog
{
    public const string ResourceName = "KasseAPI_Final.Services.Backup.BackupKnownGaps.json";

    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.Ordinal)
    {
        "warning",
        "error",
    };

    public BackupKnownGapsCatalog()
    {
        var file = Load();
        Version = file.Version;
        Gaps = file.Gaps;
        Ids = Gaps.Select(gap => gap.Id).ToHashSet(StringComparer.Ordinal);
    }

    public int Version { get; }

    public IReadOnlyList<BackupKnownGapDefinition> Gaps { get; }

    public IReadOnlySet<string> Ids { get; }

    private static (int Version, IReadOnlyList<BackupKnownGapDefinition> Gaps) Load()
    {
        var assembly = typeof(BackupKnownGapsCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        var file = JsonSerializer.Deserialize<GapFile>(reader.ReadToEnd(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        if (file is null || file.Version < 1)
            throw new InvalidOperationException("Backup known-gap catalog version is missing.");
        if (file.Gaps is null || file.Gaps.Count == 0)
            throw new InvalidOperationException("Backup known-gap catalog is empty.");

        var gaps = file.Gaps
            .Select(row => new BackupKnownGapDefinition(
                (row.Id ?? string.Empty).Trim(),
                (row.Title ?? string.Empty).Trim(),
                (row.Severity ?? string.Empty).Trim()))
            .ToArray();
        if (gaps.Any(gap => gap.Id.Length == 0 || gap.Title.Length == 0))
            throw new InvalidOperationException("Backup known-gap catalog contains an empty id or title.");
        if (gaps.Any(gap => !AllowedSeverities.Contains(gap.Severity)))
            throw new InvalidOperationException("Backup known-gap catalog contains an unknown severity.");
        if (gaps.Select(gap => gap.Id).Distinct(StringComparer.Ordinal).Count() != gaps.Length)
            throw new InvalidOperationException("Backup known-gap catalog contains a duplicate id.");

        return (file.Version, gaps);
    }

    private sealed class GapFile
    {
        public int Version { get; set; }

        public List<GapRow> Gaps { get; set; } = [];
    }

    private sealed class GapRow
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Severity { get; set; }
    }
}

/// <summary>One row from <c>BackupKnownGaps.json</c>. Severity is <c>warning</c> or <c>error</c>.</summary>
public sealed record BackupKnownGapDefinition(string Id, string Title, string Severity);

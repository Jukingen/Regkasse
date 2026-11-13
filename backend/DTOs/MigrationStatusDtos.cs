namespace KasseAPI_Final.DTOs;

public sealed class MigrationStatusDto
{
    public string Status { get; set; } = "Healthy";

    public int AppliedCount { get; set; }

    public int PendingCount { get; set; }

    public string? LatestApplied { get; set; }

    public IReadOnlyList<string> Pending { get; set; } = Array.Empty<string>();

    public IReadOnlyList<MigrationEntryDto> Applied { get; set; } = Array.Empty<MigrationEntryDto>();

    public DateTime CheckedAtUtc { get; set; }
}

public sealed class MigrationEntryDto
{
    public string Id { get; set; } = string.Empty;

    public string? ProductVersion { get; set; }
}

public sealed class AdminMigrationStatusDto
{
    public string Status { get; set; } = "Healthy";

    public int AppliedCount { get; set; }

    public int PendingCount { get; set; }

    public string? LatestApplied { get; set; }

    public IReadOnlyList<string> Pending { get; set; } = Array.Empty<string>();

    /// <summary>Newest first (capped).</summary>
    public IReadOnlyList<MigrationEntryDto> RecentApplied { get; set; } = Array.Empty<MigrationEntryDto>();

    public DateTime CheckedAtUtc { get; set; }

    public string StrategyDoc { get; set; } = "docs/DATABASE_MIGRATION_STRATEGY.md";
}

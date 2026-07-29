namespace KasseAPI_Final.DTOs;

public sealed class DepExportComplianceScoreDto
{
    public Guid TenantId { get; set; }

    /// <summary>0–100 weighted operational DEP export readiness score.</summary>
    public int Score { get; set; }

    /// <summary>A, B, C, D, or F.</summary>
    public string Grade { get; set; } = "F";

    public DateTime CalculatedAt { get; set; }

    public IReadOnlyList<DepExportScoreFactorDto> Factors { get; set; } =
        Array.Empty<DepExportScoreFactorDto>();

    public IReadOnlyList<string> CriticalIssues { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

    public string Disclaimer { get; set; } =
        "Operational DEP export readiness score (not official BMF/RKSV certification).";
}

public sealed class DepExportScoreFactorDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Weight as percent of overall score (factors sum to 100).</summary>
    public int Weight { get; set; }

    /// <summary>0–100 factor score.</summary>
    public int Score { get; set; }

    /// <summary>Passed, Warning, or Failed.</summary>
    public string Status { get; set; } = DepExportScoreFactorStatuses.Failed;

    public string Description { get; set; } = string.Empty;
}

public static class DepExportScoreFactorStatuses
{
    public const string Passed = "Passed";
    public const string Warning = "Warning";
    public const string Failed = "Failed";
}

public sealed class DepExportComplianceScoreHistoryDto
{
    public Guid TenantId { get; set; }

    public IReadOnlyList<DepExportComplianceScoreHistoryItemDto> Items { get; set; } =
        Array.Empty<DepExportComplianceScoreHistoryItemDto>();
}

public sealed class DepExportComplianceScoreHistoryItemDto
{
    public Guid Id { get; set; }

    public int Score { get; set; }

    public string Grade { get; set; } = "F";

    public DateTime CalculatedAt { get; set; }
}

public sealed class DepExportImprovementSuggestionDto
{
    public string Code { get; set; } = string.Empty;

    public string Severity { get; set; } = "Warning";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? DeepLink { get; set; }
}

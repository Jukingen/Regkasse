namespace KasseAPI_Final.Models;

/// <summary>
/// Computed DEP export compliance requirement (not persisted).
/// Derived from calendar deadlines and <see cref="DepExportCompliancePeriod"/> / history.
/// </summary>
public class DepExportRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    /// <summary>Legal, Recommended, or Optional.</summary>
    public string RequirementType { get; set; } = DepExportRequirementTypes.Recommended;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    public bool IsCompleted { get; set; }

    /// <summary>1–5 (5 = highest).</summary>
    public int Priority { get; set; }

    /// <summary>Yearly, Quarterly, Monthly, Urgent, or EventBased.</summary>
    public string Category { get; set; } = DepExportRequirementCategories.Yearly;

    public DateTime? PeriodStart { get; set; }

    public DateTime? PeriodEnd { get; set; }
}

public static class DepExportRequirementTypes
{
    public const string Legal = "Legal";
    public const string Recommended = "Recommended";
    public const string Optional = "Optional";
}

public static class DepExportRequirementCategories
{
    public const string Yearly = "Yearly";
    public const string Quarterly = "Quarterly";
    public const string Monthly = "Monthly";
    public const string Urgent = "Urgent";
    public const string EventBased = "EventBased";
}

/// <summary>Aggregated DEP export compliance snapshot for a tenant.</summary>
public class DepExportComplianceStatus
{
    public Guid TenantId { get; set; }

    public bool IsCompliant { get; set; }

    public int TotalRequirements { get; set; }

    public int CompletedCount { get; set; }

    public int PendingCount { get; set; }

    public int OverdueCount { get; set; }

    public int LegalIncompleteCount { get; set; }

    public DepExportRequirement? NextRequirement { get; set; }

    public DepExportCompliancePeriod? CurrentPeriod { get; set; }

    public DateTime CheckedAtUtc { get; set; }

    public string Disclaimer { get; set; } =
        "Operational DEP export readiness (yearly legal deadline Jan 31). Not official BMF/RKSV certification.";
}

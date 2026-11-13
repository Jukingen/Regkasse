namespace KasseAPI_Final.Models;

public class ComplianceIssue
{
    /// <summary>Critical | Warning | Info</summary>
    public string Severity { get; set; } = "Warning";

    /// <summary>Stable machine code for FA i18n (e.g. MISSING_TAX_GROUP).</summary>
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public int AffectedCount { get; set; }

    public IReadOnlyList<Guid> SampleProductIds { get; set; } = [];
}

public class ComplianceReport
{
    public bool IsCompliant { get; set; }

    public List<ComplianceIssue> Issues { get; set; } = [];

    public int TotalProducts { get; set; }

    public int CompliantProducts { get; set; }

    public int NonCompliantProducts { get; set; }

    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

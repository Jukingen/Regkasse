namespace KasseAPI_Final.Models;

/// <summary>Per tax-group catalog + period sales snapshot for FA stats cards.</summary>
public sealed class TaxGroupStat
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public string? Color { get; set; }

    public string? Icon { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Active products currently assigned to this tax group.</summary>
    public int ProductCount { get; set; }

    /// <summary>Gross Umsatz (receipt tax lines) for this group's rate in the stats period.</summary>
    public decimal Revenue { get; set; }

    /// <summary>Share of active catalog products (0–100).</summary>
    public decimal Percentage { get; set; }
}

public sealed class TaxGroupStatsReport
{
    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public int TotalProducts { get; set; }

    public decimal TotalRevenue { get; set; }

    public List<TaxGroupStat> Groups { get; set; } = [];
}

namespace KasseAPI_Final.Models;

/// <summary>Period MwSt / VAT report for a tenant (analytics + export).</summary>
public class TaxReport
{
    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public List<TaxGroupSummary> TaxGroups { get; set; } = [];

    public decimal TotalNetRevenue { get; set; }

    public decimal TotalTaxAmount { get; set; }

    public decimal TotalGrossRevenue { get; set; }
}

/// <summary>Aggregated VAT bucket within a <see cref="TaxReport"/>.</summary>
public class TaxGroupSummary
{
    public string TaxGroupName { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public decimal NetRevenue { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal GrossRevenue { get; set; }

    public int TransactionCount { get; set; }
}

/// <summary>Time-series point for tax-amount trends by rate.</summary>
public class TaxTrendPoint
{
    public DateTime Date { get; set; }

    public decimal Rate { get; set; }

    public string TaxRateLabel { get; set; } = string.Empty;

    /// <summary>Sum of VAT / MwSt for the bucket on that date.</summary>
    public decimal Amount { get; set; }
}

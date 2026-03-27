namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// Operatör / muhasebe rapor paketi — POS <c>payment_details</c> üzerinden; fatura tablosu değil.
/// </summary>
public sealed class OperationalReportMetaDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime ReportGeneratedAtUtc { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    /// <summary>Austria takvim günü başlangıç (gösterim).</summary>
    public DateTime PeriodStartLocalDate { get; set; }
    public DateTime PeriodEndLocalDate { get; set; }
    public string PeriodPreset { get; set; } = "custom";
    public Guid? CashRegisterId { get; set; }
    public string? CashierId { get; set; }
    public int? PaymentMethodFilter { get; set; }
    public bool ActiveOnly { get; set; }
}

public sealed class OperationalSummaryDto
{
    public OperationalReportMetaDto Meta { get; set; } = new();
    public int PaymentRowCount { get; set; }
    public decimal GrossTotalAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public int RefundRowCount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public IReadOnlyList<PaymentMethodBucketDto> ByPaymentMethod { get; set; } = Array.Empty<PaymentMethodBucketDto>();
    public IReadOnlyList<CashierBucketDto> ByCashier { get; set; } = Array.Empty<CashierBucketDto>();
    /// <summary>Donanım TSE X raporu değildir — kasa içi ara özet.</summary>
    public string? InterimDisclaimer { get; set; }
    /// <summary>Tagesabschluss / Kassenabschluss ile ilişkilendirme notu.</summary>
    public string? ClosingDisclaimer { get; set; }
}

public sealed class PaymentMethodBucketDto
{
    public string MethodKey { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class CashierBucketDto
{
    public string CashierId { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class PeriodicOperationalReportDto
{
    public OperationalSummaryDto Summary { get; set; } = new();
}

public sealed class InterimOperationalReportDto
{
    public OperationalSummaryDto Summary { get; set; } = new();
}

public sealed class ClosingReferenceRowDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime ClosingDateUtc { get; set; }
    public string ClosingType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public int TransactionCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasTseSignature { get; set; }
}

public sealed class ClosingReferenceReportDto
{
    public OperationalReportMetaDto Meta { get; set; } = new();
    public IReadOnlyList<ClosingReferenceRowDto> DailyClosings { get; set; } = Array.Empty<ClosingReferenceRowDto>();
    public string OperatorNote { get; set; } =
        "Entspricht dem Kassenabschluss (Tagesabschluss) in der Datenbank — kein separater Hardware-Z-Bon.";
}

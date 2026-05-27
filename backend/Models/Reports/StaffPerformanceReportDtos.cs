namespace KasseAPI_Final.Models.Reports;

/// <summary>
/// Personal-/Kassenleistung aus <c>payment_details</c> — keine Audit-Doppelzählung.
/// </summary>
public sealed class StaffPerformanceReportDto
{
    public OperationalReportMetaDto Meta { get; set; } = new();
    /// <summary>Explizite Datenqualität / Grenzen (keine Schätzungen).</summary>
    public StaffPerformanceReliabilityDto Reliability { get; set; } = new();
    public StaffPerformanceTotalsDto Totals { get; set; } = new();
    /// <summary>Pro Kasiyer (<see cref="PaymentDetails.CashierId"/> = Application User Id).</summary>
    public IReadOnlyList<StaffPerformanceStaffRowDto> ByStaff { get; set; } = Array.Empty<StaffPerformanceStaffRowDto>();
    /// <summary>Zahlungsart × Kasiyer (nur Verkaufszeilen).</summary>
    public IReadOnlyList<StaffPerformanceStaffMethodSliceDto> ByStaffAndPaymentMethod { get; set; } = Array.Empty<StaffPerformanceStaffMethodSliceDto>();
    /// <summary>Österreichischer Kalendertag — aggregiert über alle im Filter enthaltenen Kasiyer.</summary>
    public IReadOnlyList<StaffPerformanceLocalDayAggregateDto> AggregateByLocalDay { get; set; } = Array.Empty<StaffPerformanceLocalDayAggregateDto>();
    /// <summary>Optional: Kalendertag × Kasiyer (kann viele Zeilen erzeugen).</summary>
    public IReadOnlyList<StaffPerformanceLocalDayStaffDto> ByLocalDayAndStaff { get; set; } = Array.Empty<StaffPerformanceLocalDayStaffDto>();
    public IReadOnlyList<StaffPerformanceAnomalyDto> Anomalies { get; set; } = Array.Empty<StaffPerformanceAnomalyDto>();
}

/// <summary>Kein „ungefähr“: nur dokumentierte Annahmen.</summary>
public sealed class StaffPerformanceReliabilityDto
{
    public string PrimaryDataSource { get; set; } = "payment_details";
    public string BusinessTimeZone { get; set; } = "Europe/Vienna";
    /// <summary>Keine separate Schicht-Tabelle: Tagesgrenzen = AT-Kalendertag auf Basis von <c>CreatedAt</c> (UTC).</summary>
    public string DayBucketNote { get; set; } =
        "Day buckets use Austria local calendar date derived from payment CreatedAt (UTC). This is not a POS shift boundary.";
    public string AuditSeparationNote { get; set; } =
        "Metrics are computed from payment rows only; audit logs are not summed into amounts to avoid double-counting.";
    public string CashierIdentityNote { get; set; } =
        "CashierId is stored at payment time; display names are resolved from users when the id matches an account.";
}

public sealed class StaffPerformanceTotalsDto
{
    public int SaleTransactionCount { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public int RefundRowCount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public int StornoRowCount { get; set; }
}

public sealed class StaffPerformanceStaffRowDto
{
    public string CashierId { get; set; } = string.Empty;
    /// <summary>Auflösung über AspNetUsers; sonst null.</summary>
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public int SaleTransactionCount { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public int RefundRowCount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public int StornoRowCount { get; set; }
    /// <summary>Refund-Zeilen / Verkaufs-Transaktionen (kann &gt; 1 sein).</summary>
    public decimal RefundRowsPerSale { get; set; }
    /// <summary>Storno-Zeilen / Verkaufs-Transaktionen.</summary>
    public decimal StornoRowsPerSale { get; set; }
    /// <summary>|Refund-Betragssumme| / Bruttoumsatz (0 wenn Umsatz 0).</summary>
    public decimal RefundAmountToGrossRatio { get; set; }
}

public sealed class StaffPerformanceStaffMethodSliceDto
{
    public string CashierId { get; set; } = string.Empty;
    public string PaymentMethodRaw { get; set; } = string.Empty;
    public int SaleCount { get; set; }
    public decimal GrossAmount { get; set; }
}

public sealed class StaffPerformanceLocalDayAggregateDto
{
    /// <summary>Format yyyyMMdd (Vienna).</summary>
    public string LocalDayYyyyMmDd { get; set; } = string.Empty;
    public int SaleTransactionCount { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public int RefundRowCount { get; set; }
    public int StornoRowCount { get; set; }
}

public sealed class StaffPerformanceLocalDayStaffDto
{
    public string LocalDayYyyyMmDd { get; set; } = string.Empty;
    public string CashierId { get; set; } = string.Empty;
    public int SaleTransactionCount { get; set; }
    public decimal GrossSalesAmount { get; set; }
    public int RefundRowCount { get; set; }
    public int StornoRowCount { get; set; }
}

public sealed class StaffPerformanceAnomalyDto
{
    public string Kind { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string CashierId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? MetricValue { get; set; }
    public decimal? Threshold { get; set; }
}

/// <summary>Extended user performance report (payment_details, per cashier user id).</summary>
public sealed class UserPerformanceReportDto
{
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }
    public OperationalReportMetaDto Meta { get; set; } = new();
    public StaffPerformanceReliabilityDto Reliability { get; set; } = new();
    public IReadOnlyList<UserPerformanceRowDto> PerUser { get; set; } = Array.Empty<UserPerformanceRowDto>();
    public IReadOnlyList<string> TopPerformers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> HighStornoRateWarning { get; set; } = Array.Empty<string>();
    public const decimal DefaultHighStornoRateThreshold = 0.05m;
    public decimal HighStornoRateThreshold { get; set; } = DefaultHighStornoRateThreshold;
}

public sealed class UserPerformanceRowDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageTransactionValue { get; set; }

    public int StornoCount { get; set; }
    public decimal StornoRate { get; set; }
    public int RefundCount { get; set; }
    public decimal RefundRate { get; set; }

    public decimal TransactionsPerHour { get; set; }
    public double AverageProcessingSeconds { get; set; }

    public DateTime? FirstTransactionAtUtc { get; set; }
    public DateTime? LastTransactionAtUtc { get; set; }
    public double ActiveHours { get; set; }
}

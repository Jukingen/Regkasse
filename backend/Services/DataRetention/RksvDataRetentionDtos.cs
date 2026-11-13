namespace KasseAPI_Final.Services.DataRetention;

/// <summary>RKSV 7-year retention snapshot for a single tenant (compliance dashboard).</summary>
public sealed class RetentionReport
{
    public Guid TenantId { get; init; }

    /// <summary>Austrian RKSV minimum retention period in years.</summary>
    public int RetentionYears { get; init; } = 7;

    public DateTime AsOfUtc { get; init; }

    /// <summary>UTC cutoff: records with CreatedAt/IssuedAt before this are past the minimum retention window.</summary>
    public DateTime RetentionCutoffUtc { get; init; }

    public required RksvDataStatus RksvData { get; init; }

    public required NonRksvDataStatus NonRksvData { get; init; }
}

public sealed class RksvDataStatus
{
    /// <summary>Total fiscal payment rows for the tenant.</summary>
    public int PaymentDetailsCount { get; init; }

    /// <summary>Payments older than the 7-year cutoff (past minimum retention; not auto-deleted).</summary>
    public int PaymentDetailsPastRetentionCount { get; init; }

    public int ReceiptsCount { get; init; }

    public int DailyClosingsCount { get; init; }

    public int AuditLogsCount { get; init; }

    public DateTime? OldestPaymentDate { get; init; }

    public DateTime? OldestReceiptDate { get; init; }

    /// <summary>
    /// Legal retention end for the oldest payment (<see cref="OldestPaymentDate"/> + retention years).
    /// Null when there is no payment history.
    /// </summary>
    public DateTime? RetentionUntil { get; init; }

    /// <summary>
    /// Informational day after <see cref="RetentionUntil"/>. Fiscal RKSV rows are retained by policy
    /// and are not purged by customer-data deletion.
    /// </summary>
    public DateTime? WillBeDeletedOn { get; init; }

    /// <summary>True when any RKSV payment is still within the 7-year window.</summary>
    public bool IsUnderRetentionObligation { get; init; }
}

public sealed class NonRksvDataStatus
{
    public int ProductsCount { get; init; }

    public int CustomersCount { get; init; }

    public int CategoriesCount { get; init; }

    public int InvoicesCount { get; init; }

    /// <summary>Non-fiscal rows may be purged after an approved deletion request (RKSV rows remain).</summary>
    public bool CanBeDeleted { get; init; }
}

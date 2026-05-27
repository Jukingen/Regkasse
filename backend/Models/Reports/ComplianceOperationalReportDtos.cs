namespace KasseAPI_Final.Models.Reports;

/// <summary>Daily cash/payment reconciliation read model (payment rows + shift close when available).</summary>
public sealed class DailyReconciliationReportDto
{
    public DateTime BusinessDate { get; set; }
    public Guid? CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }

    public decimal CashTotal { get; set; }
    public decimal CardTotal { get; set; }
    public decimal VoucherTotal { get; set; }
    public decimal OtherTotal { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? CashDifference { get; set; }

    public int VouchersIssued { get; set; }
    public int VouchersRedeemed { get; set; }
    public int VouchersExpired { get; set; }

    public bool IsReconciled { get; set; }
    public string? ReconciledByUserId { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public string? Notes { get; set; }

    /// <summary>German operator disclaimer when counts are derived, not manually posted.</summary>
    public string DisclaimerDe { get; set; } = string.Empty;
}

public sealed class TseChainContinuityReportDto
{
    public OperationalReportMetaDto Meta { get; set; } = new();
    public IReadOnlyList<TseContinuityRegisterReportDto> Registers { get; set; } = Array.Empty<TseContinuityRegisterReportDto>();
    public int TotalReceiptsChecked { get; set; }
    public int TotalSignatureCount { get; set; }
    public int TotalGapsCount { get; set; }
    public int TotalDuplicateCount { get; set; }
    /// <summary>Legacy aggregate: sum of chain breaks across registers.</summary>
    public int BreakCount { get; set; }
    public string OperatorNoteDe { get; set; } =
        "Prüft gespeicherte RKSV-Belegkette (PrevSignatureValue), BelegNr-Lücken und Duplikate — kein Ersatz für DEP-Export oder Hardware-TSE-Audit.";
}

/// <summary>Per-register TSE continuity summary (maps to operator TseContinuityReport contract).</summary>
public sealed class TseContinuityRegisterReportDto
{
    public Guid CashRegisterId { get; set; }
    public string? RegisterNumber { get; set; }
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }

    public DateTime? FirstSignatureAtUtc { get; set; }
    public DateTime? LastSignatureAtUtc { get; set; }
    public int SignatureCount { get; set; }
    public int GapsCount { get; set; }
    public int DuplicateCount { get; set; }
    public bool HasGaps { get; set; }
    public bool HasDuplicates { get; set; }
    public double MaxGapDurationSeconds { get; set; }

    public int ChainBreakCount { get; set; }
    public int SequenceGapCount { get; set; }
    public int MissingSignatureCount { get; set; }
    public int ReceiptsInRange { get; set; }
    public int LastCounterFromState { get; set; }
    public string? LastSignaturePreview { get; set; }

    /// <summary>Relative API path for CSV chain export (append to API base URL).</summary>
    public string DetailsExportPath { get; set; } = string.Empty;
    public string DetailsExportJsonPath { get; set; } = string.Empty;

    public IReadOnlyList<TseChainBreakDto> Breaks { get; set; } = Array.Empty<TseChainBreakDto>();
}

public sealed class TseChainDetailRowDto
{
    public Guid CashRegisterId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool HasSignature { get; set; }
    public int? ParsedSequence { get; set; }
    public string? ParsedSequenceDateYmd { get; set; }
    public bool ChainLinkValid { get; set; }
    public string? SignaturePreview { get; set; }
    public string? PrevSignaturePreview { get; set; }
}

public sealed class TseChainBreakDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? ExpectedPrevSignature { get; set; }
    public string? ActualPrevSignature { get; set; }
}

public sealed class OfflineRecoveryReportDto
{
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }

    public int PendingAtStart { get; set; }
    public int PendingAtEnd { get; set; }

    public int RecoveredSuccessfully { get; set; }
    public int RecoveredWithRetry { get; set; }
    public int PermanentlyFailed { get; set; }
    public int ManuallyIntervened { get; set; }

    public double AverageRecoverySeconds { get; set; }
    public double MaxRecoverySeconds { get; set; }

    public IReadOnlyList<OfflineRecoveryRegisterBreakdownDto> ByRegister { get; set; } =
        Array.Empty<OfflineRecoveryRegisterBreakdownDto>();

    /// <summary>Legacy summary fields (aligned with queue snapshot).</summary>
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public int CompletedCount { get; set; }
    public int ClockDriftWarningCount { get; set; }
    public int SequenceGapCount { get; set; }
    public DateTime? LastReplayAtUtc { get; set; }
    public IReadOnlyList<OfflineRecoveryRowDto> RecentRows { get; set; } = Array.Empty<OfflineRecoveryRowDto>();
    public string OperatorNoteDe { get; set; } =
        "Offline-Warteschlange (Server-Intents). Wiederholung über Admin-Offline-Transaktionen.";
}

public sealed class OfflineRecoveryRegisterBreakdownDto
{
    public Guid CashRegisterId { get; set; }
    public string RegisterNumber { get; set; } = string.Empty;
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
}

public sealed class OfflineRecoveryRowDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ServerReceivedAtUtc { get; set; }
    public DateTime? LastReplayAttemptAt { get; set; }
    public string? LastError { get; set; }
    public bool ClockDriftWarning { get; set; }
    public bool SequenceGapDetected { get; set; }
    public int RetryCount { get; set; }
}

public sealed class PeakHourHeatmapReportDto
{
    public OperationalReportMetaDto Meta { get; set; } = new();
    /// <summary>7 rows (Mon–Sun) × 24 columns (hour 0–23), Vienna local time. Values = payment count.</summary>
    public int[][] Cells { get; set; } = Array.Empty<int[]>();
    public int MaxCellCount { get; set; }
    public decimal MaxCellAmount { get; set; }
    public IReadOnlyList<PeakHourDayTotalDto> DayTotals { get; set; } = Array.Empty<PeakHourDayTotalDto>();
    public PeakHourSlotDto? BusiestHour { get; set; }
    public PeakHourSlotDto? QuietestHour { get; set; }
    public double AverageTransactionsPerHour { get; set; }
    public IReadOnlyList<StaffingRecommendationDto> RecommendedStaffingLevels { get; set; } =
        Array.Empty<StaffingRecommendationDto>();
}

public sealed class PeakHourDayTotalDto
{
    public int DayOfWeek { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public sealed class ProductMovementReportDto
{
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }
    public OperationalReportMetaDto Meta { get; set; } = new();
    public IReadOnlyList<ProductMovementItemDto> TopSellingByQuantity { get; set; } = Array.Empty<ProductMovementItemDto>();
    public IReadOnlyList<ProductMovementItemDto> TopSellingByRevenue { get; set; } = Array.Empty<ProductMovementItemDto>();
    public IReadOnlyList<ProductMovementItemDto> SlowMovers { get; set; } = Array.Empty<ProductMovementItemDto>();
    public decimal StockTurnoverRate { get; set; }
    public double DaysOfInventoryOnHand { get; set; }
    public IReadOnlyList<ProductSeasonalTrendDto> SeasonalTrends { get; set; } = Array.Empty<ProductSeasonalTrendDto>();
    /// <summary>Legacy flat list (same as quantity leaders).</summary>
    public IReadOnlyList<ProductMovementLineDto> Lines { get; set; } = Array.Empty<ProductMovementLineDto>();
    public IReadOnlyList<InventoryMovementLineDto> StockMovements { get; set; } = Array.Empty<InventoryMovementLineDto>();
}

public sealed class ProductMovementLineDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class InventoryMovementLineDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime TransactionDateUtc { get; set; }
}

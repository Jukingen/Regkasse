namespace KasseAPI_Final.Models.Export;

/// <summary>Root payload for fiscal / RKSV export (JSON; CSV fragments optional).</summary>
public sealed class FiscalExportPackageDto
{
    /// <summary>Increment when export shape or semantics change materially.</summary>
    public string SchemaVersion { get; set; } = "1.4";

    /// <summary>Mandatory misuse guard: this export is NOT legal proof. Always present; clients must not treat the export as a legal RKSV attestation.</summary>
    public string NotLegalProofNotice { get; set; } = string.Empty;

    /// <summary>İşletim profili: operational_preview | accounting_report | legal_compliance_export | diagnostic_package.</summary>
    public string ExportProfile { get; set; } = "operational_preview";

    /// <summary>Profil amacı (İngilizce sabit metin); operatör yönlendirmesi için.</summary>
    public string ExportProfileIntentNotice { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }
    public Guid CashRegisterId { get; set; }
    public string RegisterNumber { get; set; } = string.Empty;
    public string RegisterLocation { get; set; } = string.Empty;
    public FiscalExportPeriodDto Period { get; set; } = new();

    /// <summary>
    /// Live chain head for the register (not limited to the export period). May differ from the last receipt in this export if more receipts exist after period.ToUtc.
    /// </summary>
    public FiscalSignatureChainStateDto? SignatureChainState { get; set; }

    /// <summary>Receipt chain fields in issuance order within the export period only.</summary>
    public IReadOnlyList<FiscalReceiptChainLinkDto> ReceiptChain { get; set; } = Array.Empty<FiscalReceiptChainLinkDto>();

    public IReadOnlyList<FiscalReceiptExportDto> Receipts { get; set; } = Array.Empty<FiscalReceiptExportDto>();
    public IReadOnlyList<FiscalClosingExportDto> Closings { get; set; } = Array.Empty<FiscalClosingExportDto>();
    public string? ReceiptsCsv { get; set; }
    public string? ClosingsCsv { get; set; }
    public int ReceiptCount { get; set; }
    public int ClosingCount { get; set; }

    /// <summary>Receipts matching the period filter (before any truncation cap).</summary>
    public int TotalReceiptsMatchingPeriod { get; set; }

    /// <summary>True when more receipts matched the period than included in Receipts (see MaxReceiptRows in service).</summary>
    public bool ReceiptsTruncated { get; set; }

    /// <summary>
    /// Warnings and scope limits: window boundaries, truncation, and that integrity flags are diagnostic only (not legal proof).
    /// Always read before interpreting integrity booleans; they are best-effort and observed-within-scope only.
    /// </summary>
    public IReadOnlyList<string> ExportScopeWarnings { get; set; } = Array.Empty<string>();

    /// <summary>Diagnostic: prev/current signature mismatches between consecutive exported receipts (same order as ReceiptChain). Not proof of full chain.</summary>
    public IReadOnlyList<string> ChainContinuityWarnings { get; set; } = Array.Empty<string>();

    /// <summary>Best-effort diagnostics only; not a legal RKSV attestation. All booleans are observed-within-scope, not global guarantees.</summary>
    public FiscalExportIntegrityDto Integrity { get; set; } = new();
}

/// <summary>
/// Diagnostic flags and counts for the export slice only. No property here is a legal or global guarantee;
/// use IntegrityDiagnosticNotes and ExportScopeWarnings to interpret scope and limitations.
/// </summary>
public sealed class FiscalExportIntegrityDto
{
    /// <summary>
    /// Diagnostic (observed-within-scope): true when no chainContinuityWarnings among exported receipts in order.
    /// Does not prove global chain integrity—only adjacency within this export slice. Not RKSV compliance proof.
    /// </summary>
    public bool SignatureChainValid { get; set; }

    /// <summary>Same value as SignatureChainValid; diagnostic only, observed within export order.</summary>
    public bool ReceiptSignatureLinkageOkInExportOrder { get; set; }

    /// <summary>
    /// Diagnostic (observed-within-scope): Beleg SEQ +1 per calendar day along exported issuance order; unparseable numbers skipped.
    /// Not a full register sequence audit; not a guarantee.
    /// </summary>
    public bool SequenceContinuous { get; set; }

    /// <summary>Same value as SequenceContinuous; diagnostic only, observed within export order.</summary>
    public bool BelegSequenceContiguousInExportedOrderPerDay { get; set; }

    public int OfflineReplayGaps { get; set; }
    public int TotalOfflineTransactions { get; set; }
    public int SyncedOfflineTransactions { get; set; }
    public int FailedOfflineTransactions { get; set; }

    /// <summary>Observability: replayed intents in period with DeviceId present (for coverage %).</summary>
    public int OfflineIntentCoverageTotal { get; set; }
    public int OfflineIntentCoverageWithDeviceId { get; set; }
    public int OfflineIntentCoverageWithSequence { get; set; }

    /// <summary>DeviceId coverage in export period as percent (0..100). Null if no samples.</summary>
    public double? DeviceIdCoveragePercent { get; set; }
    /// <summary>Sequence coverage in export period as percent (0..100). Null if no samples.</summary>
    public double? SequenceCoveragePercent { get; set; }
    /// <summary>True when DeviceId or Sequence coverage is below configured threshold (actionable).</summary>
    public bool LowCoverageAlert { get; set; }

    /// <summary>Offline metrics use OfflineCreatedAtUtc in the same UTC window as Period.</summary>
    public bool OfflineMetricsScopedToPeriod { get; set; } = true;

    /// <summary>When true, a sample of offline_transactions had payload_hash mismatch ratio above threshold; legacy data quality risk is high (run repair before production).</summary>
    public bool LegacyDataQualityRiskHigh { get; set; }

    /// <summary>When LegacyDataQualityRiskHigh is true, the sampled mismatch ratio (percent). Null if not computed.</summary>
    public double? LegacyPayloadHashMismatchRatioPercent { get; set; }

    /// <summary>Notes explaining how the diagnostic booleans were computed (scope, insufficient rows, what was not checked). Always read for correct interpretation.</summary>
    public IReadOnlyList<string> IntegrityDiagnosticNotes { get; set; } = Array.Empty<string>();
}

public sealed class FiscalExportPeriodDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public sealed class FiscalSignatureChainStateDto
{
    public Guid CashRegisterId { get; set; }
    public string? LastSignature { get; set; }
    public int LastCounter { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class FiscalReceiptChainLinkDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid ReceiptId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string? SignatureValue { get; set; }
    public string? PrevSignatureValue { get; set; }
}

public sealed class FiscalReceiptExportDto
{
    public Guid ReceiptId { get; set; }
    public Guid PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public string? CashierId { get; set; }
    public Guid CashRegisterId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? QrCodePayload { get; set; }
    public string? SignatureValue { get; set; }
    public string? PrevSignatureValue { get; set; }
    public string? SignatureFormat { get; set; }
    public string? JwsHeader { get; set; }
    public string? JwsPayload { get; set; }
    public string? JwsSignature { get; set; }
    public string? Provider { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<FiscalReceiptItemExportDto> Items { get; set; } = Array.Empty<FiscalReceiptItemExportDto>();
    public IReadOnlyList<FiscalReceiptTaxLineExportDto> TaxLines { get; set; } = Array.Empty<FiscalReceiptTaxLineExportDto>();
    public bool IsStorno { get; set; }
    public bool IsRefund { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public Guid? OriginalReceiptId { get; set; }
    /// <summary>Unified reversal reason (storno text or refund reason).</summary>
    public string? ReversalReason { get; set; }

    /// <summary>True when this fiscal receipt originates from a controlled offline intent replay.</summary>
    public bool HasOfflineOrigin { get; set; }

    /// <summary>Device/client UTC when the offline intent was created (only when HasOfflineOrigin=true).</summary>
    public DateTime? OfflineCreatedAtUtc { get; set; }

    /// <summary>Server UTC when replay completed and fiscal payment was created (only when HasOfflineOrigin=true).</summary>
    public DateTime? FiscalizedAtUtc { get; set; }

    /// <summary>One POST /offline/replay batch that produced this payment (support traceability).</summary>
    public Guid? OfflineReplayBatchCorrelationId { get; set; }
}

public sealed class FiscalReceiptItemExportDto
{
    public Guid ItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal LineNet { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TaxRate { get; set; }
    public Guid? ParentItemId { get; set; }
    public string? CategoryName { get; set; }
}

public sealed class FiscalReceiptTaxLineExportDto
{
    public Guid LineId { get; set; }
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
}

public sealed class FiscalClosingExportDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime ClosingDateUtc { get; set; }
    public string ClosingType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public int TransactionCount { get; set; }
    public string TseSignature { get; set; } = string.Empty;
    public string? SignatureFormat { get; set; }
    public string? JwsHeader { get; set; }
    public string? JwsPayload { get; set; }
    public string? JwsSignature { get; set; }
    public string? Provider { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FinanzOnlineStatus { get; set; }
    public string? FinanzOnlineReferenceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

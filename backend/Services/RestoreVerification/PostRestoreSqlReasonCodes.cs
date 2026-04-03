namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Post-restore SQL kontrolleri için makine sabitleri; UI/i18n ve log korelasyonu için (teknik metin İngilizce kalır).
/// </summary>
public static class PostRestoreSqlReasonCodes
{
    public const string MissingTargetConnection = "MISSING_TARGET_CONNECTION";

    public const string SchemaRelationMissing = "SCHEMA_RELATION_MISSING";

    /// <summary>İleride şema doğrulama dedektörleri için ayrıldı.</summary>
    public const string SchemaMismatch = "SCHEMA_MISMATCH";

    public const string TableAccessible = "TABLE_ACCESSIBLE";

    public const string TableQueryFailed = "TABLE_QUERY_FAILED";

    public const string MigrationHistoryInsufficient = "MIGRATION_HISTORY_INSUFFICIENT";

    public const string FiscalReceiptNumberInvariantOk = "FISCAL_RECEIPT_NUMBER_INVARIANT_OK";

    public const string FiscalReceiptNumberEmptyOrNull = "FISCAL_RECEIPT_NUMBER_EMPTY_OR_NULL";

    public const string FiscalReceiptNumberDuplicate = "FISCAL_RECEIPT_NUMBER_DUPLICATE";

    public const string ReferentialOk = "REFERENTIAL_OK";

    public const string OrphanReceiptItems = "ORPHAN_RECEIPT_ITEMS";

    public const string OrphanReceiptTaxLines = "ORPHAN_RECEIPT_TAX_LINES";

    public const string ReceiptsMissingPaymentDetails = "RECEIPTS_MISSING_PAYMENT_DETAILS";

    public const string DatasetSizeNote = "DATASET_SIZE_NOTE";

    public const string UnhandledException = "UNHANDLED_EXCEPTION";
}

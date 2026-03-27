namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Export paketine profil alanlarını ve ek uyarı satırlarını uygular (üretim hattı aynı kalır).
/// </summary>
public static class FiscalExportProfileMetadata
{
    public const string IntentDiagnostic =
        "Diagnostic package: support and engineering analysis only; not for accounting or legal filing.";

    public const string IntentOperational =
        "Operational preview profile: day-to-day operator checks and process monitoring. Not an official accounting/legal filing package.";

    public const string IntentAccounting =
        "Accounting report profile: structured figures for bookkeeping and reconciliation workflows. Not a legal filing package.";

    public const string IntentCompliance =
        "Legal compliance export profile: structured evidence for compliance/legal review. Still not a substitute for official FinanzOnline filings.";

    public static void Apply(FiscalExportPackageDto package, FiscalExportProfile profile)
    {
        package.ExportProfile = profile switch
        {
            FiscalExportProfile.OperationalPreview => "operational_preview",
            FiscalExportProfile.AccountingReport => "accounting_report",
            FiscalExportProfile.LegalComplianceExport => "legal_compliance_export",
            FiscalExportProfile.DiagnosticPackage => "diagnostic_package",
            _ => "operational_preview",
        };

        package.ExportProfileIntentNotice = profile switch
        {
            FiscalExportProfile.OperationalPreview => IntentOperational,
            FiscalExportProfile.AccountingReport => IntentAccounting,
            FiscalExportProfile.LegalComplianceExport => IntentCompliance,
            FiscalExportProfile.DiagnosticPackage => IntentDiagnostic,
            _ => IntentOperational,
        };

        var extra = profile switch
        {
            FiscalExportProfile.OperationalPreview =>
                "OPERATIONAL_PREVIEW: Operator guidance only; not for official accounting/legal submission.",
            FiscalExportProfile.AccountingReport =>
                "ACCOUNTING_REPORT: Bookkeeping/reconciliation use; legal compliance filing requires legal profile + complete evidence.",
            FiscalExportProfile.LegalComplianceExport =>
                "LEGAL_COMPLIANCE_EXPORT: Intended for compliance/legal review; still does not replace official FinanzOnline acknowledgements.",
            FiscalExportProfile.DiagnosticPackage =>
                "DIAGNOSTIC_PACKAGE: Technical support/troubleshooting only; do not use as formal document.",
            _ => null,
        };

        if (extra == null) return;

        var list = package.ExportScopeWarnings.ToList();
        list.Add(extra);
        package.ExportScopeWarnings = list;
    }
}

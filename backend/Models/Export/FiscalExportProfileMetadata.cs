namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Export paketine profil alanlarını ve ek uyarı satırlarını uygular (üretim hattı aynı kalır).
/// </summary>
public static class FiscalExportProfileMetadata
{
    public const string IntentDiagnostic =
        "Diagnostic profile: support and engineering analysis only; integrity flags are slice-scoped and best-effort.";

    public const string IntentAuditHandoff =
        "Audit handoff profile: structured bundle for third-party or internal audit review. Same statutory disclaimer applies; use alongside audit trail and source systems.";

    public const string IntentCompliance =
        "Compliance / legal review profile: structured evidence for external compliance or legal counsel review. Not a substitute for FinanzOnline filings or statutory RKSV attestations.";

    public static void Apply(FiscalExportPackageDto package, FiscalExportProfile profile)
    {
        package.ExportProfile = profile switch
        {
            FiscalExportProfile.Diagnostic => "diagnostic",
            FiscalExportProfile.AuditHandoff => "audit_handoff",
            FiscalExportProfile.LegalCompliance => "compliance",
            _ => "diagnostic",
        };

        package.ExportProfileIntentNotice = profile switch
        {
            FiscalExportProfile.Diagnostic => IntentDiagnostic,
            FiscalExportProfile.AuditHandoff => IntentAuditHandoff,
            FiscalExportProfile.LegalCompliance => IntentCompliance,
            _ => IntentDiagnostic,
        };

        var extra = profile switch
        {
            FiscalExportProfile.AuditHandoff =>
                "AUDIT_HANDOFF: This file is labelled for controlled handoff to auditors; it remains non-statutory (see notLegalProofNotice).",
            FiscalExportProfile.LegalCompliance =>
                "COMPLIANCE_PACK: Labelled for compliance/legal review workflows; does not replace official registers or FinanzOnline evidence.",
            _ => null,
        };

        if (extra == null) return;

        var list = package.ExportScopeWarnings.ToList();
        list.Add(extra);
        package.ExportScopeWarnings = list;
    }
}

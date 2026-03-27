using System.Security.Claims;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Security;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Fiscal export profili için izin kuralları (report.export tabanı + profil katmanları).
/// Geriye dönük: eski profil adları alias olarak çözülür.
/// </summary>
public static class FiscalExportProfileRules
{
    /// <summary>
    /// Profil dizesini çözümler; bilinmeyen değerler reddedilir.
    /// </summary>
    public static bool TryParseProfile(string? exportProfile, out FiscalExportProfile profile)
    {
        if (string.IsNullOrWhiteSpace(exportProfile))
        {
            profile = FiscalExportProfile.OperationalPreview;
            return true;
        }

        switch (exportProfile.Trim().ToLowerInvariant())
        {
            case "operational_preview":
            case "operationalpreview":
                profile = FiscalExportProfile.OperationalPreview;
                return true;
            case "accounting_report":
            case "accountingreport":
                profile = FiscalExportProfile.AccountingReport;
                return true;
            case "legal_compliance_export":
            case "legalcomplianceexport":
                profile = FiscalExportProfile.LegalComplianceExport;
                return true;
            case "diagnostic_package":
            case "diagnosticpackage":
                profile = FiscalExportProfile.DiagnosticPackage;
                return true;
            // legacy aliases
            case "diagnostic":
                profile = FiscalExportProfile.OperationalPreview;
                return true;
            case "audit_handoff":
                profile = FiscalExportProfile.AccountingReport;
                return true;
            case "compliance":
                profile = FiscalExportProfile.LegalComplianceExport;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    /// <summary>
    /// OperationalPreview + DiagnosticPackage: report.export.
    /// AccountingReport: report.export + audit.view.
    /// LegalComplianceExport: report.export + audit.view + fiscal.export.compliance.
    /// </summary>
    public static bool CanExport(ClaimsPrincipal? user, FiscalExportProfile profile)
    {
        if (user == null) return false;

        var re = user.HasPermissionClaim(AppPermissions.ReportExport);
        if (!re) return false;

        return profile switch
        {
            FiscalExportProfile.OperationalPreview => true,
            FiscalExportProfile.DiagnosticPackage => true,
            FiscalExportProfile.AccountingReport => user.HasPermissionClaim(AppPermissions.AuditView),
            FiscalExportProfile.LegalComplianceExport =>
                user.HasPermissionClaim(AppPermissions.AuditView) &&
                user.HasPermissionClaim(AppPermissions.FiscalExportCompliance),
            _ => false,
        };
    }

    public static string ForbiddenDetail(FiscalExportProfile profile) =>
        profile switch
        {
            FiscalExportProfile.OperationalPreview => "Requires permission: report.export",
            FiscalExportProfile.DiagnosticPackage => "Requires permission: report.export",
            FiscalExportProfile.AccountingReport => "Requires permissions: report.export and audit.view",
            FiscalExportProfile.LegalComplianceExport =>
                "Requires permissions: report.export, audit.view, and fiscal.export.compliance",
            _ => "Invalid export profile",
        };
}

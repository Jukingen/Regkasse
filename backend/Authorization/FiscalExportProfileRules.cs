using System.Security.Claims;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Security;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Fiscal export profili için izin kuralları (report.export tabanı + profil katmanları).
/// Geriye dönük: exportProfile yok veya diagnostic → yalnızca report.export.
/// </summary>
public static class FiscalExportProfileRules
{
    /// <summary>
    /// Profil dizesini çözümler; bilinmeyen veya boş değerler diagnostic sayılır (geriye dönük uyumluluk).
    /// </summary>
    public static bool TryParseProfile(string? exportProfile, out FiscalExportProfile profile)
    {
        if (string.IsNullOrWhiteSpace(exportProfile))
        {
            profile = FiscalExportProfile.Diagnostic;
            return true;
        }

        switch (exportProfile.Trim().ToLowerInvariant())
        {
            case "diagnostic":
                profile = FiscalExportProfile.Diagnostic;
                return true;
            case "audit_handoff":
                profile = FiscalExportProfile.AuditHandoff;
                return true;
            case "compliance":
                profile = FiscalExportProfile.LegalCompliance;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    /// <summary>
    /// Diagnostic: report.export.
    /// Audit handoff: report.export + audit.view.
    /// Compliance: report.export + audit.view + fiscal.export.compliance (rol matrisinde dar rol).
    /// </summary>
    public static bool CanExport(ClaimsPrincipal? user, FiscalExportProfile profile)
    {
        if (user == null) return false;

        var re = user.HasPermissionClaim(AppPermissions.ReportExport);
        if (!re) return false;

        return profile switch
        {
            FiscalExportProfile.Diagnostic => true,
            FiscalExportProfile.AuditHandoff => user.HasPermissionClaim(AppPermissions.AuditView),
            FiscalExportProfile.LegalCompliance =>
                user.HasPermissionClaim(AppPermissions.AuditView) &&
                user.HasPermissionClaim(AppPermissions.FiscalExportCompliance),
            _ => false,
        };
    }

    public static string ForbiddenDetail(FiscalExportProfile profile) =>
        profile switch
        {
            FiscalExportProfile.Diagnostic => "Requires permission: report.export",
            FiscalExportProfile.AuditHandoff => "Requires permissions: report.export and audit.view",
            FiscalExportProfile.LegalCompliance =>
                "Requires permissions: report.export, audit.view, and fiscal.export.compliance",
            _ => "Invalid export profile",
        };
}

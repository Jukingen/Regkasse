using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Export;
using Xunit;

namespace KasseAPI_Final.Tests;

public class FiscalExportProfileRulesTests
{
    [Fact]
    public void TryParseProfile_Empty_IsDiagnostic()
    {
        Assert.True(FiscalExportProfileRules.TryParseProfile(null, out var p));
        Assert.Equal(FiscalExportProfile.Diagnostic, p);
    }

    [Fact]
    public void TryParseProfile_Invalid_ReturnsFalse()
    {
        Assert.False(FiscalExportProfileRules.TryParseProfile("invalid", out _));
    }

    [Fact]
    public void CanExport_ReportViewer_AuditHandoff_Allowed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.ReportExport),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.AuditView),
        }, "test"));

        Assert.True(FiscalExportProfileRules.CanExport(user, FiscalExportProfile.AuditHandoff));
    }

    [Fact]
    public void CanExport_ReportViewer_Compliance_DeniedWithoutCompliancePermission()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.ReportExport),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.AuditView),
        }, "test"));

        Assert.False(FiscalExportProfileRules.CanExport(user, FiscalExportProfile.LegalCompliance));
    }

    [Fact]
    public void CanExport_WithComplianceClaim_AllowsCompliance()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.ReportExport),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.AuditView),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.FiscalExportCompliance),
        }, "test"));

        Assert.True(FiscalExportProfileRules.CanExport(user, FiscalExportProfile.LegalCompliance));
    }
}

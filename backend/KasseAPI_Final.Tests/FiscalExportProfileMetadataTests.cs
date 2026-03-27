using KasseAPI_Final.Models.Export;
using Xunit;

namespace KasseAPI_Final.Tests;

public class FiscalExportProfileMetadataTests
{
    [Fact]
    public void Apply_Compliance_SetsProfileAndAddsWarning()
    {
        var package = new FiscalExportPackageDto
        {
            NotLegalProofNotice = "n",
            ExportScopeWarnings = new[] { "base" },
        };

        FiscalExportProfileMetadata.Apply(package, FiscalExportProfile.LegalComplianceExport);

        Assert.Equal("legal_compliance_export", package.ExportProfile);
        Assert.Contains(package.ExportScopeWarnings, w => w.Contains("LEGAL_COMPLIANCE_EXPORT", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_Diagnostic_DoesNotAddProfileExtraLine()
    {
        var package = new FiscalExportPackageDto
        {
            ExportScopeWarnings = new[] { "NOT LEGAL PROOF — base" },
        };

        FiscalExportProfileMetadata.Apply(package, FiscalExportProfile.DiagnosticPackage);

        Assert.Equal("diagnostic_package", package.ExportProfile);
        Assert.DoesNotContain(package.ExportScopeWarnings, w => w.Contains("ACCOUNTING_REPORT", StringComparison.Ordinal));
    }
}

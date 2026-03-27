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

        FiscalExportProfileMetadata.Apply(package, FiscalExportProfile.LegalCompliance);

        Assert.Equal("compliance", package.ExportProfile);
        Assert.Contains(package.ExportScopeWarnings, w => w.Contains("COMPLIANCE_PACK", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_Diagnostic_DoesNotAddProfileExtraLine()
    {
        var package = new FiscalExportPackageDto
        {
            ExportScopeWarnings = new[] { "NOT LEGAL PROOF — base" },
        };

        FiscalExportProfileMetadata.Apply(package, FiscalExportProfile.Diagnostic);

        Assert.Equal("diagnostic", package.ExportProfile);
        Assert.DoesNotContain(package.ExportScopeWarnings, w => w.Contains("AUDIT_HANDOFF", StringComparison.Ordinal));
    }
}

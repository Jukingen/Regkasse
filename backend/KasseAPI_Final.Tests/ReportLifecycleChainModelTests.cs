using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Rapor lifecycle zinciri için temel model invariant kontrolleri.
/// </summary>
public class ReportLifecycleChainModelTests
{
    [Fact]
    public void NewReport_HasVersionOne_AndNoCorrectionLink()
    {
        var tages = new TagesberichtReport();
        Assert.Equal(1, tages.ReportVersion);
        Assert.Null(tages.CorrectionOfReportId);
        Assert.Equal(ReportCorrectionTypes.None, tages.CorrectionType);
        Assert.Equal(ReportSubmissionImpacts.None, tages.SubmissionImpact);
    }

    [Fact]
    public void CorrectionTypes_Contain_Amendment()
    {
        Assert.Equal("Amendment", ReportCorrectionTypes.Amendment);
    }
}

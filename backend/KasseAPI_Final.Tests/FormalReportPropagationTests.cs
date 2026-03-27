using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class FormalReportPropagationTests
{
    [Fact]
    public async Task MarkAfterTagesCorrection_SetsMonatsAndJahresReviewFlags()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var regId = Guid.NewGuid();
        var monthStart = new DateTime(2026, 3, 1);
        var yearStart = new DateTime(2026, 1, 1);

        db.Set<MonatsberichtReport>().Add(new MonatsberichtReport
        {
            ViennaMonthStart = monthStart,
            ScopeKind = MonatsberichtScopeKinds.Register,
            CashRegisterId = regId,
            SnapshotJson = "{}",
            SnapshotHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            SnapshotSchemaVersion = "1.0",
            ReportStatus = MonatsberichtReportStatuses.Finalized,
            CreatedByUserId = "test",
        });
        db.Set<JahresberichtReport>().Add(new JahresberichtReport
        {
            ViennaYearStart = yearStart,
            ScopeKind = MonatsberichtScopeKinds.Register,
            CashRegisterId = regId,
            SnapshotJson = "{}",
            SnapshotHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            SnapshotSchemaVersion = "1.0",
            ReportStatus = MonatsberichtReportStatuses.Finalized,
            CreatedByUserId = "test",
        });
        await db.SaveChangesAsync();

        await FormalReportPropagationMarkers.MarkAfterTagesCorrectionAsync(
            db,
            new DateTime(2026, 3, 15),
            regId,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var m = await db.Set<MonatsberichtReport>().AsNoTracking().SingleAsync();
        var j = await db.Set<JahresberichtReport>().AsNoTracking().SingleAsync();

        Assert.True(m.UpstreamReviewRequired);
        Assert.Equal(FormalReportPropagationMarkers.ReasonTagesSupersededInMonth, m.UpstreamReviewReasonCode);
        Assert.True(j.UpstreamReviewRequired);
        Assert.Equal(FormalReportPropagationMarkers.ReasonTagesSupersededInMonth, j.UpstreamReviewReasonCode);
    }

    [Fact]
    public void ClearUpstreamReview_ClearsFlags()
    {
        var m = new MonatsberichtReport
        {
            UpstreamReviewRequired = true,
            UpstreamReviewReasonCode = FormalReportPropagationMarkers.ReasonTagesSupersededInMonth,
        };
        FormalReportPropagationMarkers.ClearUpstreamReview(m);
        Assert.False(m.UpstreamReviewRequired);
        Assert.Null(m.UpstreamReviewReasonCode);
    }
}

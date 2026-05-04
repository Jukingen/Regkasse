using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvSpecialReceiptFinanzOnlineSubmissionTrackerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FonTrack_{System.Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void CreateInitialNotRequiredRow_Startbeleg_Succeeds()
    {
        var tracker = new RksvSpecialReceiptFinanzOnlineSubmissionTracker(CreateContext());
        var pid = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var reg = Guid.NewGuid();
        var row = tracker.CreateInitialNotRequiredRow(pid, rid, reg, RksvSpecialReceiptKinds.Startbeleg);
        Assert.Equal(pid, row.PaymentId);
        Assert.Equal(rid, row.ReceiptId);
        Assert.Equal(reg, row.CashRegisterId);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending, row.Status);
        Assert.Equal(0, row.AttemptCount);
    }

    [Fact]
    public void CreateInitialNotRequiredRow_InvalidKind_Throws()
    {
        var tracker = new RksvSpecialReceiptFinanzOnlineSubmissionTracker(CreateContext());
        Assert.Throws<ArgumentException>(() =>
            tracker.CreateInitialNotRequiredRow(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RksvSpecialReceiptKinds.Nullbeleg));
    }
}

using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
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
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public void CreateInitialPendingRow_Startbeleg_Succeeds()
    {
        var tracker = new RksvSpecialReceiptFinanzOnlineSubmissionTracker(CreateContext());
        var pid = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var reg = Guid.NewGuid();
        var row = tracker.CreateInitialPendingRow(pid, rid, reg, RksvSpecialReceiptKinds.Startbeleg);
        Assert.Equal(pid, row.PaymentId);
        Assert.Equal(rid, row.ReceiptId);
        Assert.Equal(reg, row.CashRegisterId);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending, row.Status);
        Assert.Equal(0, row.AttemptCount);
    }

    [Fact]
    public void CreateInitialPendingRow_InvalidKind_Throws()
    {
        var tracker = new RksvSpecialReceiptFinanzOnlineSubmissionTracker(CreateContext());
        Assert.Throws<ArgumentException>(() =>
            tracker.CreateInitialPendingRow(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RksvSpecialReceiptKinds.Nullbeleg));
    }
}

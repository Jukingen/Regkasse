using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosReceiptStornoEligibilityTests
{
    [Theory]
    [InlineData(Roles.SuperAdmin, true)]
    [InlineData(Roles.Manager, true)]
    [InlineData(Roles.Cashier, false)]
    [InlineData(Roles.Waiter, false)]
    public void IsPrivilegedPosActor_MatchesRole(string role, bool expected)
    {
        Assert.Equal(expected, PosReceiptStornoEligibility.IsPrivilegedPosActor(role));
    }

    [Fact]
    public void IsOwnReceipt_IsCaseInsensitive()
    {
        Assert.True(PosReceiptStornoEligibility.IsOwnReceipt("Cashier-1", "cashier-1"));
        Assert.False(PosReceiptStornoEligibility.IsOwnReceipt("other", "cashier-1"));
        Assert.False(PosReceiptStornoEligibility.IsOwnReceipt(null, "cashier-1"));
    }

    [Fact]
    public void IsViennaCalendarToday_AcceptsCurrentInstant()
    {
        Assert.True(PosReceiptStornoEligibility.IsViennaCalendarToday(DateTime.UtcNow));
    }

    [Fact]
    public void IsViennaCalendarToday_RejectsPreviousViennaDay()
    {
        var yesterdayUtc = DateTime.UtcNow.AddHours(-30);
        Assert.False(PosReceiptStornoEligibility.IsViennaCalendarToday(yesterdayUtc));
    }

    [Fact]
    public void ResolveReason_UsesDefaultWhenEmpty()
    {
        Assert.Equal(PosReceiptStornoEligibility.DefaultReason, PosReceiptStornoEligibility.ResolveReason(null));
        Assert.Equal(PosReceiptStornoEligibility.DefaultReason, PosReceiptStornoEligibility.ResolveReason("  "));
    }

    [Fact]
    public void ResolveReason_RejectsShortTypedText()
    {
        Assert.Null(PosReceiptStornoEligibility.ResolveReason("abc"));
    }

    [Fact]
    public void ResolveReason_KeepsValidText()
    {
        Assert.Equal("Kunde hat storniert", PosReceiptStornoEligibility.ResolveReason("  Kunde hat storniert  "));
    }
}

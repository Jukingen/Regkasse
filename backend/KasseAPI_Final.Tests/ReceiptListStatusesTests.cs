using KasseAPI_Final.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ReceiptListStatusesTests
{
    [Theory]
    [InlineData(false, false, null, ReceiptListStatuses.Paid)]
    [InlineData(false, false, "", ReceiptListStatuses.Paid)]
    [InlineData(true, false, null, ReceiptListStatuses.Storno)]
    [InlineData(false, true, null, ReceiptListStatuses.Refund)]
    [InlineData(true, true, "Startbeleg", "Startbeleg")]
    public void FromPayment_ResolvesStatus(bool isStorno, bool isRefund, string? kind, string expected)
    {
        Assert.Equal(expected, ReceiptListStatuses.FromPayment(isStorno, isRefund, kind));
    }
}

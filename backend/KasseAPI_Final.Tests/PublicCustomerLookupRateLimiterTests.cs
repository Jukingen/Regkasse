using KasseAPI_Final.Services.Order;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PublicCustomerLookupRateLimiterTests
{
    [Fact]
    public void Sixth_call_in_the_same_minute_is_rejected()
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var limiter = new PublicCustomerLookupRateLimiter(() => now);

        for (var i = 0; i < PublicCustomerLookupRateLimiter.MaxRequestsPerWindow; i++)
            Assert.True(limiter.TryConsume("203.0.113.8"));

        Assert.False(limiter.TryConsume("203.0.113.8"));
        Assert.True(limiter.TryConsume("203.0.113.9"));
    }
}

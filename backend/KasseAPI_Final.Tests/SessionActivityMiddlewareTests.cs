using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KasseAPI_Final.Tests;

public class SessionActivityMiddlewareTests
{
    [Theory]
    [InlineData("/api/Auth/me", true)]
    [InlineData("/api/admin/products", true)]
    [InlineData("/_next/static/chunk.js", false)]
    [InlineData("/api/Auth/refresh-session", false)]
    [InlineData("/api/user/sessions/heartbeat", false)]
    [InlineData("/favicon.ico", false)]
    [InlineData("/api/health", true)]
    public void IsActivityEndpoint_classifies_paths(string path, bool expected)
    {
        var context = new DefaultHttpContext
        {
            Request = { Method = "GET", Path = path },
        };

        Assert.Equal(expected, SessionActivityMiddleware.IsActivityEndpoint(context));
    }

    [Fact]
    public void IsActivityEndpoint_ignores_options_requests()
    {
        var context = new DefaultHttpContext
        {
            Request = { Method = "OPTIONS", Path = "/api/Auth/me" },
        };

        Assert.False(SessionActivityMiddleware.IsActivityEndpoint(context));
    }
}

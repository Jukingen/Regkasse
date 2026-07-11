using KasseAPI_Final.Middleware;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public void HasDevTenantOverride_True_When_Header_Present()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = "dev";

        Assert.True(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public void HasDevTenantOverride_True_When_Query_Present()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tenant=bar");

        Assert.True(TenantContextMiddleware.HasDevTenantOverride(context));
    }

    [Fact]
    public void HasDevTenantOverride_False_When_No_Dev_Override()
    {
        var context = new DefaultHttpContext();

        Assert.False(TenantContextMiddleware.HasDevTenantOverride(context));
    }
}

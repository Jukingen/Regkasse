using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SubdomainTenantProviderTests
{
    private static SubdomainTenantProvider Create(
        string host,
        bool isDevelopment,
        string? headerTenant = null,
        string? queryTenant = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        if (!string.IsNullOrEmpty(headerTenant))
            httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = headerTenant;
        if (!string.IsNullOrEmpty(queryTenant))
            httpContext.Request.QueryString = new QueryString($"?{SubdomainTenantProvider.DevTenantQueryName}={queryTenant}");

        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new SubdomainTenantProvider(httpAccessor.Object, environment.Object);
    }

    [Fact]
    public void GetCurrentTenantId_Production_Subdomain_ReturnsSlug()
    {
        var provider = Create("companyA.regkasse.at", isDevelopment: false);
        Assert.Equal("companyA", provider.GetCurrentTenantId());
    }

    [Fact]
    public void GetCurrentTenantId_Production_Localhost_ReturnsAdmin()
    {
        var provider = Create("localhost", isDevelopment: false);
        Assert.Equal("admin", provider.GetCurrentTenantId());
    }

    [Fact]
    public void GetCurrentTenantId_Development_HeaderOverridesHost()
    {
        var provider = Create("localhost", isDevelopment: true, headerTenant: "companyB");
        Assert.Equal("companyB", provider.GetCurrentTenantId());
    }

    [Fact]
    public void GetCurrentTenantId_Development_QueryOverridesSubdomain()
    {
        var provider = Create("companyA.regkasse.at", isDevelopment: true, queryTenant: "companyC");
        Assert.Equal("companyC", provider.GetCurrentTenantId());
    }

    [Fact]
    public void GetCurrentTenantId_Development_HeaderTakesPrecedenceOverQuery()
    {
        var provider = Create(
            "companyA.regkasse.at",
            isDevelopment: true,
            headerTenant: "fromHeader",
            queryTenant: "fromQuery");
        Assert.Equal("fromHeader", provider.GetCurrentTenantId());
    }

    [Theory]
    [InlineData("dev.regkasse.local", "dev")]
    [InlineData("prod.regkasse.local", "prod")]
    [InlineData("tenant.example.local", "tenant")]
    public void GetCurrentTenantId_LocalDevelopmentHost_ReturnsSubdomainSlug(string host, string expectedSlug)
    {
        var provider = Create(host, isDevelopment: false);
        Assert.Equal(expectedSlug, provider.GetCurrentTenantId());
    }

    [Theory]
    [InlineData("pos.regkasse.at")]
    [InlineData("api.regkasse.at")]
    [InlineData("admin.regkasse.at")]
    [InlineData("www.regkasse.at")]
    public void GetCurrentTenantId_Production_ReservedPlatformHosts_ReturnAdmin(string host)
    {
        var provider = Create(host, isDevelopment: false);
        Assert.Equal("admin", provider.GetCurrentTenantId());
    }
}

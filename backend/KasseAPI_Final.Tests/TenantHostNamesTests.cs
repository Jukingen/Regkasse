using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantHostNamesTests
{
    [Theory]
    [InlineData("http://dev.regkasse.local:3000", true)]
    [InlineData("http://dev.regkasse.local", true)]
    [InlineData("http://tenant.example.local:5184", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("http://company.regkasse.at", false)]
    public void IsTrustedLocalDevCorsHost_MatchesLocalDevPatterns(string host, bool expected)
    {
        Assert.Equal(expected, TenantHostNames.IsTrustedLocalDevCorsHost(new Uri(host).Host));
    }

    [Theory]
    [InlineData("regkasse.at", true)]
    [InlineData("pos.regkasse.at", true)]
    [InlineData("admin.regkasse.at", true)]
    [InlineData("www.regkasse.at", true)]
    [InlineData("cafe.regkasse.at", true)]
    [InlineData("evil.example.com", false)]
    [InlineData("regkasse.at.evil.com", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsRegkassePlatformWebOriginHost_MatchesPlatformHosts(string? host, bool expected)
    {
        Assert.Equal(expected, TenantHostNames.IsRegkassePlatformWebOriginHost(host));
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("www", true)]
    [InlineData("pos", true)]
    [InlineData("api", true)]
    [InlineData("POS", true)]
    [InlineData("Api", true)]
    [InlineData("dev", false)]
    [InlineData("cafe", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsReservedPlatformHostLabel_MatchesPlatformHosts(string? label, bool expected)
    {
        Assert.Equal(expected, TenantHostNames.IsReservedPlatformHostLabel(label));
    }

    [Theory]
    [InlineData("pos.regkasse.at", "admin")]
    [InlineData("api.regkasse.at", "admin")]
    [InlineData("admin.regkasse.at", "admin")]
    [InlineData("www.regkasse.at", "admin")]
    [InlineData("cafe.regkasse.at", "cafe")]
    [InlineData("dev.regkasse.local", "dev")]
    [InlineData("localhost", "admin")]
    public void GetTenantSlugFromHost_ReservedHostsMapToAdmin(string host, string expected)
    {
        Assert.Equal(expected, TenantHostNames.GetTenantSlugFromHost(host));
    }
}

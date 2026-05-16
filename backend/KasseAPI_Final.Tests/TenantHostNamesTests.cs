using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantHostNamesTests
{
    [Theory]
    [InlineData("http://cafe.regkasse.local:3000", true)]
    [InlineData("http://dev.regkasse.local", true)]
    [InlineData("http://tenant.example.local:5184", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("http://company.regkasse.at", false)]
    public void IsTrustedLocalDevCorsHost_MatchesLocalDevPatterns(string host, bool expected)
    {
        Assert.Equal(expected, TenantHostNames.IsTrustedLocalDevCorsHost(new Uri(host).Host));
    }
}

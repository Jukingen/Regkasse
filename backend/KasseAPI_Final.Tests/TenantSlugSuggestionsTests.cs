using KasseAPI_Final.Services.AdminTenants;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantSlugSuggestionsTests
{
    [Theory]
    [InlineData("cafe", true)]
    [InlineData("dev", true)]
    [InlineData("admin", false)]
    [InlineData("www", false)]
    [InlineData("pos", false)]
    [InlineData("api", false)]
    [InlineData("mail", false)]
    public void IsValidSlug_RejectsReservedPlatformAndMailLabels(string slug, bool expected)
    {
        Assert.Equal(expected, TenantSlugSuggestions.IsValidSlug(slug));
    }
}

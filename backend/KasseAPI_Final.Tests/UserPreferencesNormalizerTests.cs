using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserPreferencesNormalizerTests
{
    [Theory]
    [InlineData("comfortable", "comfortable")]
    [InlineData("STANDARD", "standard")]
    [InlineData("invalid", "standard")]
    public void NormalizeDensityMode_Works(string input, string expected) =>
        Assert.Equal(expected, UserPreferencesNormalizer.NormalizeDensityMode(input));
}

using KasseAPI_Final.Services.Email;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class EmailLogMaskTests
{
    [Theory]
    [InlineData("user@example.com", "u***@example.com")]
    [InlineData("a@b.co", "a***@b.co")]
    [InlineData("not-an-email", "***")]
    [InlineData("", "***")]
    public void Mask_KeepsFirstCharacterAndDomain(string input, string expected)
    {
        Assert.Equal(expected, EmailLogMask.Mask(input));
    }
}

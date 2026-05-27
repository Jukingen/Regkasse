using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ManualRestoreApprovalTokenHasherTests
{
    [Fact]
    public void GenerateSixDigitToken_returns_six_digits_in_range()
    {
        for (var i = 0; i < 20; i++)
        {
            var token = ManualRestoreApprovalTokenHasher.GenerateSixDigitToken();
            Assert.True(ManualRestoreApprovalTokenHasher.IsValidSixDigitFormat(token));
            Assert.Equal(6, token.Length);
        }
    }

    [Fact]
    public void Hash_and_Verify_roundtrip_with_BCrypt()
    {
        const string plain = "654321";
        var hash = ManualRestoreApprovalTokenHasher.Hash(plain);
        Assert.StartsWith("$2", hash);
        Assert.True(ManualRestoreApprovalTokenHasher.Verify(plain, hash));
        Assert.False(ManualRestoreApprovalTokenHasher.Verify("000000", hash));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void IsValidSixDigitFormat_rejects_invalid(string token)
    {
        Assert.False(ManualRestoreApprovalTokenHasher.IsValidSixDigitFormat(token));
    }
}

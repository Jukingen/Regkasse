using KasseAPI_Final.Tse.Fiskaly;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyQrCodeValidatorTests
{
    private const string Sample =
        "_R1-AT3_dGxx_19_2017-10-24T11:07:32_0,00_0,00_0,00_0,00_0,00_7eti9M9dETz2_5474185F_M8LJDeWizNY=_4CtUHTuHoWvNfY0Ty+K8SuUVPYfZHjkM70/ZzATkb7Oj6G8PNWR6K1vsFWTXg2YsMyYHxVXpGJYEiAn0Uojfzw==";

    [Fact]
    public void Validate_FiskalyAt3Sample_IsValid()
    {
        var result = FiskalyQrCodeValidator.Validate(Sample);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("R1-AT3", result.Prefix);
        Assert.Equal("dGxx", result.CashRegisterSerial);
        Assert.Equal("19", result.ReceiptNumber);
        Assert.Equal("2017-10-24T11:07:32", result.Timestamp);
    }

    [Fact]
    public void Validate_Empty_IsInvalid()
    {
        var result = FiskalyQrCodeValidator.Validate("  ");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WrongPrefix_IsInvalid()
    {
        var result = FiskalyQrCodeValidator.Validate("not-a-qr");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("_R1-AT", StringComparison.Ordinal));
    }
}

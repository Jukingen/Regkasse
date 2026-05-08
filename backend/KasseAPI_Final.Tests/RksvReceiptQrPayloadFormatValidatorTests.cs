using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvReceiptQrPayloadFormatValidatorTests
{
    private readonly RksvReceiptQrPayloadFormatValidator _sut = new();

    [Fact]
    public void Validate_ValidPayload_ReturnsTrueWithParsed()
    {
        var jws = "eyJhbGciOiJFUzI1NiJ9.eyJrYXNzZW5JZCI6IjEifQ.signature";
        var qr = $"_R1-AT1_KASSE-001_AT-KASSE-001-20260228-00000001_2026-02-28T17:52:48_10.00_0.00_SW-TEST-abc12345_{jws}";

        var r = _sut.Validate(qr);

        Assert.True(r.IsValidFormat);
        Assert.NotNull(r.Parsed);
        Assert.Empty(r.Errors);
        Assert.Equal("AT-KASSE-001-20260228-00000001", r.Parsed!.ReceiptNumber);
        Assert.Equal("2026-02-28T17:52:48", r.Parsed.Timestamp);
        Assert.Equal("10.00", r.Parsed.Totals.GrossTotal);
        Assert.Equal("0.00", r.Parsed.Totals.SecondAmount);
        Assert.Equal("SW-TEST-abc12345", r.Parsed.CertificateSerial);
        Assert.Null(r.Parsed.PreviousSignature);
    }

    [Fact]
    public void Validate_JwsWithUnderscoresInParts_JoinsCorrectly()
    {
        var jws = "eyJhbGciOiJFUzI1NiJ9.c_d.e_f";
        var qr = $"_R1-AT1_REG_AT-REG-20200101-1_2020-01-01T00:00:00_1.00_0.00_CERT_{jws}";

        var r = _sut.Validate(qr);

        Assert.True(r.IsValidFormat);
        Assert.NotNull(r.Parsed);
    }

    [Fact]
    public void Validate_NegativeAndCommaDecimalTotals_NormalizesToDot()
    {
        var qr = "_R1-AT1_REG_AT-REG-20200101-1_2020-01-01T00:00:00_-1,25_0,00_CERT_eyJhbGciOiJFUzI1NiJ9.e30.sig";

        var r = _sut.Validate(qr);

        Assert.True(r.IsValidFormat);
        Assert.NotNull(r.Parsed);
        Assert.Equal("-1.25", r.Parsed!.Totals.GrossTotal);
        Assert.Equal("0.00", r.Parsed.Totals.SecondAmount);
    }

    [Fact]
    public void Validate_WrongPrefix_ReturnsFalse()
    {
        var r = _sut.Validate("NON_FISCAL_DEMO_x");

        Assert.False(r.IsValidFormat);
        Assert.Null(r.Parsed);
        Assert.Contains(r.Errors, e => e.Contains("_R1-", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WrongVersion_ReturnsFalse()
    {
        var r = _sut.Validate("_R1-AT2_REG_AT-REG-20200101-1_2020-01-01T00:00:00_1.00_0.00_CERT_a.b.c");

        Assert.False(r.IsValidFormat);
        Assert.Contains(r.Errors, e => e.Contains("Unsupported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TooFewSegments_ReturnsFalse()
    {
        var r = _sut.Validate("_R1-AT1_a_b_c");

        Assert.False(r.IsValidFormat);
        Assert.Contains(r.Errors, e => e.Contains("JWS", StringComparison.OrdinalIgnoreCase) || e.Contains("layout", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidReceiptNumber_ReturnsFalse()
    {
        var r = _sut.Validate("_R1-AT1_REG_BAD_2020-01-01T00:00:00_1.00_0.00_CERT_eyJhbGciOiJFUzI1NiJ9.e30.sig");

        Assert.False(r.IsValidFormat);
        Assert.Contains(r.Errors, e => e.Contains("BelegNr", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidTimestamp_ReturnsFalse()
    {
        var r = _sut.Validate("_R1-AT1_REG_AT-REG-20200101-1_2020-01-01 00:00:00_1.00_0.00_CERT_eyJhbGciOiJFUzI1NiJ9.e30.sig");

        Assert.False(r.IsValidFormat);
        Assert.Contains(r.Errors, e => e.Contains("Timestamp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidJwsPartCount_ReturnsFalse()
    {
        var r = _sut.Validate("_R1-AT1_REG_AT-REG-20200101-1_2020-01-01T00:00:00_1.00_0.00_CERT_onlyone");

        Assert.False(r.IsValidFormat);
        Assert.Contains(r.Errors, e => e.Contains("JWS", StringComparison.OrdinalIgnoreCase) || e.Contains("compact", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(_sut.Validate(null).IsValidFormat);
        Assert.False(_sut.Validate("   ").IsValidFormat);
    }
}

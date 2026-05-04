using KasseAPI_Final.Rksv;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvQrParserTests
{
    [Fact]
    public void Parse_InternalCompact_ReturnsPayload()
    {
        var jws = "eyJhbGciOiJFUzI1NiJ9.eyJrYXNzZW5JZCI6IjEifQ.signature";
        var s =
            $"_R1-AT1_KASSE-001_AT-KASSE-001-20260228-00000001_2026-02-28T17:52:48_10.00_0.00_SW-TEST-abc12345_{jws}";

        var r = RksvQrParser.Parse(s);

        Assert.True(r.Success);
        Assert.NotNull(r.Payload);
        Assert.Empty(r.Errors);
        var p = r.Payload!;
        Assert.Equal("R1-AT1", p.AlgorithmId);
        Assert.Equal(RksvQrPayloadLayout.InternalCompact, p.Layout);
        Assert.Equal("KASSE-001", p.CashRegisterId);
        Assert.Equal("AT-KASSE-001-20260228-00000001", p.ReceiptNumber);
        Assert.Equal("2026-02-28T17:52:48", p.Timestamp);
        Assert.Null(p.EncryptedTurnoverCounter);
        Assert.Null(p.PreviousSignature);
        Assert.Equal("SW-TEST-abc12345", p.CertificateSerial);
        Assert.Equal(jws, p.Signature);
        Assert.Equal(2, p.TaxBuckets.Count);
        Assert.Equal("10.00", p.TaxBuckets[0].Amount);
        Assert.Equal("0.00", p.TaxBuckets[1].Amount);
    }

    [Fact]
    public void Parse_StandardRksvV1_ReturnsPayload()
    {
        // Minimal valid JWT header (JSON object) + placeholder payload/signature (not cryptographically verified).
        var jws = "eyJhbGciOiJFUzI1NiJ9.e30.cc";
        var s =
            "_R1-AT1_K1_AT-K1-20200101-1_2020-01-01T00:00:00_1.00_2.00_3.00_4.00_5.00_ENC9_CERT8_PREV7_" + jws;

        var r = RksvQrParser.Parse(s);

        Assert.True(r.Success);
        var p = r.Payload!;
        Assert.Equal(RksvQrPayloadLayout.StandardRksvV1, p.Layout);
        Assert.Equal("ENC9", p.EncryptedTurnoverCounter);
        Assert.Equal("CERT8", p.CertificateSerial);
        Assert.Equal("PREV7", p.PreviousSignature);
        Assert.Equal(5, p.TaxBuckets.Count);
        Assert.Equal("standard", p.TaxBuckets[0].Code);
        Assert.Equal("1.00", p.TaxBuckets[0].Amount);
        Assert.Equal("5.00", p.TaxBuckets[4].Amount);
    }

    [Fact]
    public void Parse_R1_AT2_Allowed()
    {
        var r = RksvQrParser.Parse("_R1-AT2_X_AT-X-20200101-1_2020-01-01T00:00:00_1_2_3_4_5_E_C_P_eyJhbGciOiJFUzI1NiJ9.e30.cc");
        Assert.True(r.Success);
        Assert.Equal("R1-AT2", r.Payload!.AlgorithmId);
    }

    [Fact]
    public void Parse_JwsWithUnderscoresInParts_Succeeds()
    {
        var jws = "eyJhbGciOiJFUzI1NiJ9.c_d.e_f";
        var s = "_R1-AT1_REG_AT-REG-20200101-1_2020-01-01T00:00:00_1_2_CERT_" + jws;
        var r = RksvQrParser.Parse(s);
        Assert.True(r.Success);
        Assert.Equal(jws, r.Payload!.Signature);
    }

    [Fact]
    public void Parse_InvalidPrefix_Fails()
    {
        var r = RksvQrParser.Parse("NON_FISCAL_x");
        Assert.False(r.Success);
        Assert.Contains(r.Errors, e => e.Contains("R1-AT", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_WrongBodyCount_Fails()
    {
        // Five body segments + valid-looking JWS shell — no split yields exactly 6 or 11 body fields.
        var r = RksvQrParser.Parse("_R1-AT1_a_b_c_d_e_eyJhbGciOiJFUzI1NiJ9.e30.cc");
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void ParseOrThrow_Success_ReturnsPayload()
    {
        var p = RksvQrParser.ParseOrThrow("_R1-AT1_A_AT-A-20200101-1_2020-01-01T00:00:00_0_0_C_eyJhbGciOiJFUzI1NiJ9.e30.cc");
        Assert.Equal("A", p.CashRegisterId);
    }

    [Fact]
    public void ParseOrThrow_Fail_Throws()
    {
        Assert.Throws<RksvQrParseException>(() => RksvQrParser.ParseOrThrow("_R1-AT1_bad"));
    }
}

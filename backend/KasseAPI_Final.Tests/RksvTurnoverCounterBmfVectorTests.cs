using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Tse;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Cross-check turnover crypto against BMF testdb golden vectors (TESTSUITE_TEST_SZENARIO_2).
/// </summary>
public sealed class RksvTurnoverCounterBmfVectorTests
{
    private const string GoldenAesKeyBase64 = "WQRtiiya3hYh/Uz44Bv3x8ETl1nrH6nCdErn69g5/lU=";
    private const string GoldenKassenId = "CASHBOX-DEMO-1";
    private const string GoldenStartReceiptId = "CASHBOX-DEMO-1-Receipt-ID-82";

    [Theory]
    [InlineData(8, "NLoiSHL3bsM=")]
    public void Encrypt_Startbeleg_MatchesBmfGoldenVector_R1At1(int lengthBytes, string expected)
    {
        var aesKey = Convert.FromBase64String(GoldenAesKeyBase64);
        var enc = RksvTurnoverCounterCrypto.Encrypt(0, GoldenKassenId, GoldenStartReceiptId, aesKey, lengthBytes);
        Assert.Equal(expected, enc);
    }

    [Fact]
    public void Fixture_Startbeleg_TurnoverRoundTrip()
    {
        var aesKey = SHA256.HashData(Encoding.UTF8.GetBytes("Regkasse.Prueftool.Fixture.AesKey.v1"));
        const string kassen = "KASSE-FIXTURE-01";
        const string beleg = "AT-FIXTURE-20260110-0001";

        var enc = RksvTurnoverCounterCrypto.Encrypt(0, kassen, beleg, aesKey);
        Assert.Equal("omkFyyMJhWA=", enc);
        Assert.Equal(0, RksvTurnoverCounterCrypto.Decrypt(enc, kassen, beleg, aesKey));
    }
}

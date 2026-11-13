using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SoftTseServiceTests
{
    private static readonly byte[] DevAesKey = new SoftwareTseKeyProvider().GetTurnoverCounterAesKeyBytes()!;

    private static BelegdatenPayload BuildPayload() =>
        BelegdatenPayloadBuilder.Build(
            "K1",
            "SOFT_1",
            new DateTime(2025, 3, 1, 22, 59, 59, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 10.00m },
            1000,
            null,
            "SIM-TEST",
            DevAesKey);

    [Fact]
    public async Task SignAsync_DelegatesToFakeProvider_ThreeSegmentPseudoJws()
    {
        var fake = new FakeTseProvider(NullLogger<FakeTseProvider>.Instance);
        var soft = new SoftTseService(fake, NullLogger<SoftTseService>.Instance);
        var payload = BuildPayload();

        Assert.True(await soft.IsReadyAsync());
        var result = await soft.SignAsync(payload, "corr-soft");
        var expected = await fake.SignAsync(payload, "corr-soft");

        Assert.Equal(expected.CompactJws, result.CompactJws);
        Assert.Equal(FakeTseProvider.FakeCertificateSerial, result.CertificateSerialNumber);
        Assert.Equal(3, result.CompactJws.Split('.').Length);
    }
}

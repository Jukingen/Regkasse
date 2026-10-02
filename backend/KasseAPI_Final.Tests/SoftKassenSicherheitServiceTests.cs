using KasseAPI_Final.Services.Countries.KassenSicherheit;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SoftKassenSicherheitServiceTests
{
    [Fact]
    public async Task SignAsync_ReturnsThreeSegmentPseudoJws_WithoutAustrianPayload()
    {
        var soft = new SoftKassenSicherheitService(NullLogger<SoftKassenSicherheitService>.Instance);
        var request = new KassenSicherheitSignRequest(Guid.NewGuid(), "DE-1|120.00");

        Assert.True((await soft.GetStatusAsync(request.TenantId)).Ready);
        var signed = await soft.SignAsync(request);
        var chain = await soft.GetCertificateChainAsync(request.TenantId);

        Assert.True(signed.Signed);
        Assert.Equal(SoftKassenSicherheitService.ProviderId, signed.Provider);
        Assert.Equal(3, signed.Signature!.Split('.').Length);
        Assert.Equal(SoftKassenSicherheitService.BuildPseudoJws(request.Payload), signed.Signature);
        Assert.Equal([SoftKassenSicherheitService.CertificateSerial], chain.Certificates);
    }
}

using KasseAPI_Final.Services;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvSignatureVerifyServiceTests
{
    private readonly Mock<ILogger<SignaturePipeline>> _loggerMock = new();

    private RksvSignatureVerifyService CreateService(ITseKeyProvider keyProvider) =>
        new(keyProvider, new SignaturePipeline(keyProvider, _loggerMock.Object));

    [Fact]
    public async Task VerifyAsync_ValidSignature_ReturnsValid()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE001-20250225-12345678",
            new DateTime(2025, 2, 25, 13, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 123.45m },
            12345,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            keyProvider.GetTurnoverCounterAesKeyBytes()!);
        var compactJws = pipeline.Sign(payload);

        var service = CreateService(keyProvider);
        var result = await service.VerifyAsync(compactJws, certificateThumbprint: null);

        Assert.True(result.Valid);
        Assert.Contains("succeeded", result.Details, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.CertificateThumbprintUsed));
    }

    [Fact]
    public async Task VerifyAsync_WrongKey_ReturnsInvalid()
    {
        var signerProvider = new SoftwareTseKeyProvider();
        var verifierProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(signerProvider, _loggerMock.Object);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE001-20250225-12345678",
            new DateTime(2025, 2, 25, 13, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 123.45m },
            12345,
            null,
            signerProvider.GetCertificateSerialNumber()!,
            signerProvider.GetTurnoverCounterAesKeyBytes()!);
        var compactJws = pipeline.Sign(payload);

        var service = CreateService(verifierProvider);
        var result = await service.VerifyAsync(compactJws, certificateThumbprint: null);

        Assert.False(result.Valid);
        Assert.Contains("failed", result.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_UnknownThumbprint_ReturnsInvalidWithMessage()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var service = CreateService(keyProvider);

        var result = await service.VerifyAsync("a.b.c", certificateThumbprint: "DEADBEEF");

        Assert.False(result.Valid);
        Assert.Contains("not found", result.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_InvalidFormat_ReturnsInvalid()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var service = CreateService(keyProvider);

        var result = await service.VerifyAsync("not-a-jws", certificateThumbprint: null);

        Assert.False(result.Valid);
        Assert.Contains("3 parts", result.Details, StringComparison.OrdinalIgnoreCase);
    }
}

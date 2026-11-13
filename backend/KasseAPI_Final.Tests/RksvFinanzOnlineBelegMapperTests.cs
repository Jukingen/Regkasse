using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvFinanzOnlineBelegMapperTests
{
    [Fact]
    public void TryResolveBeleg_AcceptsValidDepCandidateDirectly()
    {
        var beleg = FinanzOnlineDevTestSmoke.BuildSyntheticDepBeleg();
        Assert.True(RksvFinanzOnlineBelegMapper.TryResolveBeleg(beleg, out var resolved, out var err));
        Assert.Null(err);
        Assert.Equal(beleg, resolved);
    }

    [Fact]
    public void TryResolveBeleg_ExtractsMachineCodeFromCompactJws()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var aesKey = keyProvider.GetTurnoverCounterAesKeyBytes()!;
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-42",
            new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 100.00m },
            10000,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            aesKey);
        var jws = pipeline.Sign(payload, "beleg-mapper-test");
        var expected = SignaturePipeline.GetMachineCode(payload);

        Assert.True(RksvFinanzOnlineBelegMapper.TryResolveBeleg(jws, out var resolved, out var err));
        Assert.Null(err);
        Assert.Equal(expected, resolved);
        Assert.True(FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(resolved));
    }

    [Fact]
    public void TryResolveBeleg_ExtractsMachineCodeFromQrWire()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var aesKey = keyProvider.GetTurnoverCounterAesKeyBytes()!;
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-42",
            new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 100.00m },
            10000,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            aesKey);
        var jws = pipeline.Sign(payload, "beleg-mapper-qr");
        var machineCode = SignaturePipeline.GetMachineCode(payload);
        var qrWire = $"{machineCode}_{jws}";

        Assert.True(RksvFinanzOnlineBelegMapper.TryResolveBeleg(qrWire, out var resolved, out var err));
        Assert.Null(err);
        Assert.Equal(machineCode, resolved);
    }

    [Fact]
    public void TryResolveBeleg_RejectsEmptyAndGarbage()
    {
        Assert.False(RksvFinanzOnlineBelegMapper.TryResolveBeleg(null, out _, out var err1));
        Assert.False(string.IsNullOrWhiteSpace(err1));

        Assert.False(RksvFinanzOnlineBelegMapper.TryResolveBeleg("not-valid", out _, out var err2));
        Assert.False(string.IsNullOrWhiteSpace(err2));
    }
}

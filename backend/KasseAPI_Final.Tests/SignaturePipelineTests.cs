using System.Security.Cryptography;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// RKSV SignaturePipeline unit tests:
/// - Valid flow → PASS
/// - Sertifika mismatch → FAIL
/// - Corrupted payload → FAIL
/// - Padding error → FAIL
/// </summary>
public class SignaturePipelineTests
{
    private readonly Mock<ILogger<SignaturePipeline>> _loggerMock = new();

    [Fact]
    public void ValidFlow_ShouldPass()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var payload = new BelegdatenPayload
        {
            KassenId = "KASSE-001",
            BelegNr = "AT-KASSE001-20250225-12345678",
            BelegDatum = "25.02.2025",
            Uhrzeit = "14:30:00",
            Betrag = "123.45",
            PrevSignatureValue = "",
            TaxDetails = "{}"
        };

        var compactJws = pipeline.Sign(payload, "test-correlation-1");
        Assert.NotNull(compactJws);
        var parts = compactJws.Split('.');
        Assert.Equal(3, parts.Length);

        var valid = pipeline.Verify(compactJws, keyProvider.GetPublicKey(), "verify-correlation-1");
        Assert.True(valid);
    }

    [Fact]
    public void CertificateMismatch_VerifyReturnsFalse()
    {
        var signerProvider = new SoftwareTseKeyProvider();
        var verifierProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(signerProvider, _loggerMock.Object);

        var payload = new BelegdatenPayload
        {
            KassenId = "KASSE-001",
            BelegNr = "BELEG-002",
            BelegDatum = "25.02.2025",
            Uhrzeit = "14:30:00",
            Betrag = "100.00",
            PrevSignatureValue = "",
            TaxDetails = "{}"
        };

        var compactJws = pipeline.Sign(payload);
        var valid = pipeline.Verify(compactJws, verifierProvider.GetPublicKey());
        Assert.False(valid);
    }

    [Fact]
    public void CorruptedPayload_ShouldFail()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var payload = new BelegdatenPayload
        {
            KassenId = "KASSE-001",
            BelegNr = "BELEG-001",
            BelegDatum = "25.02.2025",
            Uhrzeit = "14:30:00",
            Betrag = "100.00",
            PrevSignatureValue = "",
            TaxDetails = "{}"
        };

        var compactJws = pipeline.Sign(payload);
        var parts = compactJws.Split('.');
        var corruptedJws = parts[0] + "." + "CORRUPTED_PAYLOAD" + "." + parts[2];

        var valid = pipeline.Verify(corruptedJws, keyProvider.GetPublicKey());
        Assert.False(valid);
    }

    [Fact]
    public void PaddingError_ShouldThrow()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var payload = new BelegdatenPayload
        {
            KassenId = "KASSE-001",
            BelegNr = "BELEG-001",
            BelegDatum = "25.02.2025",
            Uhrzeit = "14:30:00",
            Betrag = "100.00",
            PrevSignatureValue = "",
            TaxDetails = "{}"
        };

        var compactJws = pipeline.Sign(payload);
        var parts = compactJws.Split('.');
        var paddedSignature = parts[2] + "=="; // Base64 padding ekle

        var ex = Assert.Throws<TsePipelineException>(() =>
            pipeline.Verify(parts[0] + "." + parts[1] + "." + paddedSignature, keyProvider.GetPublicKey()));
        Assert.Equal("BASE64URL_PADDING_ERROR", ex.ErrorCode);
    }

    [Fact]
    public void InvalidPartsCount_ShouldThrow()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var ex = Assert.Throws<TsePipelineException>(() =>
            pipeline.Verify("header.payload", keyProvider.GetPublicKey()));
        Assert.Equal("INVALID_SIGNATURE_FORMAT", ex.ErrorCode);
    }

    /// <summary>
    /// VerifyDiagnostic: Valid JWS returns 5 steps, all PASS.
    /// </summary>
    [Fact]
    public void VerifyDiagnostic_ValidJws_AllStepsPass()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var payload = new BelegdatenPayload
        {
            KassenId = "KASSE-001",
            BelegNr = "AT-KASSE001-20250225-12345678",
            BelegDatum = "25.02.2025",
            Uhrzeit = "14:30:00",
            Betrag = "100.00",
            PrevSignatureValue = "",
            TaxDetails = "{}"
        };
        var compactJws = pipeline.Sign(payload);

        var steps = pipeline.VerifyDiagnostic(compactJws);
        Assert.Equal(5, steps.Count);
        Assert.All(steps, s => Assert.Equal("PASS", s.Status));
        Assert.Contains(steps, s => s.Name == "CMC match");
        Assert.Contains(steps, s => s.Name == "JWS format");
        Assert.Contains(steps, s => s.Name == "Hash");
        Assert.Contains(steps, s => s.Name == "Signature verify");
        Assert.Contains(steps, s => s.Name == "Base64URL padding");
    }

    /// <summary>
    /// VerifyDiagnostic: Empty input returns FAILs.
    /// </summary>
    [Fact]
    public void VerifyDiagnostic_EmptyInput_AllFail()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var steps = pipeline.VerifyDiagnostic("");
        Assert.Equal(5, steps.Count);
        Assert.Equal("PASS", steps[0].Status);
        Assert.Equal("FAIL", steps[1].Status);
        Assert.Equal("FAIL", steps[2].Status);
        Assert.Equal("FAIL", steps[3].Status);
        Assert.Equal("FAIL", steps[4].Status);
    }

    /// <summary>
    /// VerifyDiagnostic: Invalid JWS format (2 parts) returns FAIL for step 2.
    /// </summary>
    [Fact]
    public void VerifyDiagnostic_InvalidJwsFormat_Step2Fail()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var steps = pipeline.VerifyDiagnostic("only.two");
        var step2 = steps.First(s => s.StepId == 2);
        Assert.Equal("FAIL", step2.Status);
        Assert.Contains("3 parts", step2.Evidence ?? "");
    }

    /// <summary>
    /// VerifyDiagnostic: Base64URL padding returns FAIL for step 5.
    /// </summary>
    [Fact]
    public void VerifyDiagnostic_PaddingInPart_Step5Fail()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, _loggerMock.Object);

        var payload = new BelegdatenPayload
        {
            KassenId = "K",
            BelegNr = "B",
            BelegDatum = "01.01.2025",
            Uhrzeit = "00:00:00",
            Betrag = "0",
            PrevSignatureValue = "",
            TaxDetails = "{}"
        };
        var compactJws = pipeline.Sign(payload);
        var parts = compactJws.Split('.');
        var paddedJws = $"{parts[0]}.{parts[1]}.{parts[2]}==";

        var steps = pipeline.VerifyDiagnostic(paddedJws);
        var step5 = steps.First(s => s.StepId == 5);
        Assert.Equal("FAIL", step5.Status);
    }
}

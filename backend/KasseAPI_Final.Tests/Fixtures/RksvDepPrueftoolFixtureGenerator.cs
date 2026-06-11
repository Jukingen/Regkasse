using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;

namespace KasseAPI_Final.Tests.Fixtures;

/// <summary>
/// Generates deterministic BMF Prüftool fixture files (dep-export.json + crypto-material.json).
/// </summary>
internal static class RksvDepPrueftoolFixtureGenerator
{
    private const string KassenId = "KASSE-FIXTURE-01";

    public static PrueftoolFixturePaths Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var keyProvider = new FixedPrueftoolTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var aesKey = keyProvider.GetTurnoverCounterAesKeyBytes()!;
        var serial = keyProvider.GetCertificateSerialNumber()!;

        var receipts = new List<(DateTime IssuedAtUtc, string Belegnummer, decimal NormalGross, long TurnoverCents)>
        {
            (new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), "AT-FIXTURE-20260110-0001", 0m, 0),
            (new DateTime(2026, 1, 10, 10, 30, 0, DateTimeKind.Utc), "AT-FIXTURE-20260110-0002", 12.40m, 1240),
            (new DateTime(2026, 1, 10, 11, 15, 0, DateTimeKind.Utc), "AT-FIXTURE-20260110-0003", 25.00m, 3740),
        };

        string? previousJws = null;
        var compactJwss = new List<string>();
        foreach (var (issuedAt, belegnummer, normalGross, turnoverCents) in receipts)
        {
            var payload = BelegdatenPayloadBuilder.Build(
                KassenId,
                belegnummer,
                issuedAt,
                new RksvTaxSetAmounts { Normal = normalGross },
                turnoverCents,
                previousJws,
                serial,
                aesKey);

            var jws = pipeline.Sign(payload, "prueftool-fixture");
            compactJwss.Add(jws);
            previousJws = jws;
        }

        var certDer = keyProvider.GetCertificateBytes()!;
        var depRoot = new RksvDepExportRootDto
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = Convert.ToBase64String(certDer),
                    Zertifizierungsstellen = [],
                    BelegeKompakt = compactJwss,
                },
            ],
        };

        var depPath = Path.Combine(outputDirectory, "dep-export.json");
        var cryptoPath = Path.Combine(outputDirectory, "crypto-material.json");

        var depJson = JsonSerializer.Serialize(depRoot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true,
        });
        File.WriteAllText(depPath, depJson);

        var crypto = new CryptographicMaterialContainerDto
        {
            Base64AesKey = Convert.ToBase64String(aesKey),
            CertificateOrPublicKeyMap = new Dictionary<string, CryptographicMaterialEntryDto>
            {
                [serial] = new()
                {
                    Id = serial,
                    SignatureDeviceType = "CERTIFICATE",
                    SignatureCertificateOrPublicKey = Convert.ToBase64String(certDer),
                },
            },
        };

        var cryptoJson = JsonSerializer.Serialize(crypto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true,
        });
        File.WriteAllText(cryptoPath, cryptoJson);

        var qrCodes = new List<string>(compactJwss.Count);
        foreach (var jws in compactJwss)
        {
            if (!RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(jws, out var qr))
                throw new InvalidOperationException("Failed to build BMF §9 QR wire format from fixture JWS.");
            qrCodes.Add(qr);
        }

        var qrRepPath = Path.Combine(outputDirectory, "qr-code-rep.json");
        var qrRepJson = JsonSerializer.Serialize(qrCodes, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(qrRepPath, qrRepJson);

        return new PrueftoolFixturePaths(depPath, cryptoPath, qrRepPath, compactJwss.Count);
    }

    /// <summary>One-time helper to emit a valid PKCS#8 for embedding in <see cref="FixedPrueftoolTseKeyProvider"/>.</summary>
    public static string DumpNewFixturePkcs8()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
    }

    internal sealed record PrueftoolFixturePaths(
        string DepExportPath,
        string CryptoMaterialPath,
        string QrCodeRepPath,
        int ReceiptCount);

    private sealed class CryptographicMaterialContainerDto
    {
        [JsonPropertyName("base64AESKey")]
        public string Base64AesKey { get; set; } = string.Empty;

        [JsonPropertyName("certificateOrPublicKeyMap")]
        public Dictionary<string, CryptographicMaterialEntryDto> CertificateOrPublicKeyMap { get; set; } = new();
    }

    private sealed class CryptographicMaterialEntryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("signatureDeviceType")]
        public string SignatureDeviceType { get; set; } = string.Empty;

        [JsonPropertyName("signatureCertificateOrPublicKey")]
        public string SignatureCertificateOrPublicKey { get; set; } = string.Empty;
    }
}

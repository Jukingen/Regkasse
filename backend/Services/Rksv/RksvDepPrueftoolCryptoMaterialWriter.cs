using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models.Export;

namespace KasseAPI_Final.Services.Rksv;

/// <summary>Writes BMF cryptographic material container JSON for DEP Prüftool runs.</summary>
public static class RksvDepPrueftoolCryptoMaterialWriter
{
    public static string Write(CryptoMaterialDto material, string outputPath)
    {
        var container = new CryptographicMaterialContainerDto
        {
            Base64AesKey = material.AesKeyBase64,
            CertificateOrPublicKeyMap = material.Certificates.ToDictionary(
                c => c.SerialNumber,
                c => new CryptographicMaterialEntryDto
                {
                    Id = c.SerialNumber,
                    SignatureDeviceType = "CERTIFICATE",
                    SignatureCertificateOrPublicKey = c.CertificateDerBase64,
                },
                StringComparer.Ordinal),
        };

        var json = JsonSerializer.Serialize(container, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true,
        });
        File.WriteAllText(outputPath, json);
        return outputPath;
    }

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

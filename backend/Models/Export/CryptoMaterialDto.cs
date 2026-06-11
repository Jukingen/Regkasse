using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Cryptographic material for DEP export Prüftool testing (internal admin API shape).
/// </summary>
public sealed class CryptoMaterialDto
{
    [JsonPropertyName("aesKeyBase64")]
    public string AesKeyBase64 { get; set; } = string.Empty;

    [JsonPropertyName("certificates")]
    public List<CertificateInfoDto> Certificates { get; set; } = new();

    [JsonPropertyName("turnoverCounters")]
    public Dictionary<string, string> TurnoverCounters { get; set; } = new();
}

public sealed class CertificateInfoDto
{
    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("certificateDerBase64")]
    public string CertificateDerBase64 { get; set; } = string.Empty;

    [JsonPropertyName("thumbprint")]
    public string Thumbprint { get; set; } = string.Empty;
}

using System.Text.Json.Serialization;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// DR kanıt bantları için makine okumalı sonuç; yanlış yeşili önlemek için <see cref="NotConfigured"/> ve <see cref="Partial"/> ayrı tutulur.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecoveryProofOutcome
{
    /// <summary>Yapılandırma yok veya bu çalıştırmada kanıt üretilmedi.</summary>
    NotConfigured,

    /// <summary>Bilinçli olarak çalıştırılmadı (bayrak kapalı).</summary>
    Skipped,

    /// <summary>Yapılandırılmış kontrol geçti.</summary>
    Passed,

    /// <summary>Yapılandırılmış kontrol başarısız.</summary>
    Failed,

    /// <summary>Kısmi kanıt (ör. yalnızca yapılandırma anlık görüntüsü; canlı harici API yok).</summary>
    Partial
}

using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// L6 kapsamında ayrı harici bağımlılık alanları; restore drill tek başına kanıtlamaz.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalDependencyRecoveryDomain
{
    /// <summary>TSE cihaz / satıcı / imza zinciri hazırlığı (donanım dışı restore kanıtı).</summary>
    TseDeviceVendor = 0,

    /// <summary>Gizli anahtarlar, bağlantı dizeleri, ortam değişkenleri (yalnızca varlık sinyali; değer yok).</summary>
    SecretsAndConfiguration = 1,

    /// <summary><c>pg_dump</c> / <c>pg_restore</c> yolları ve çalıştırılabilirlik (otomasyon için iskelet).</summary>
    BackupTooling = 2,

    /// <summary>Harici arşiv, immutability / erişilebilirlik duruşu (operatör beyanı ayrı).</summary>
    ArchiveStorage = 3,

    /// <summary>FinanzOnline dış API / oturum / gönderim hazırlığı (canlı çağrı yok).</summary>
    FinanzOnlineExternal = 4
}

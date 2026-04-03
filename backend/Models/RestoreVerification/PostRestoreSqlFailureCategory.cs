using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Post-restore süreklilik SQL başarısızlıkları için sabit taksonomi (UI/operatör anahtarı; backend üretir).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PostRestoreSqlFailureCategory
{
    /// <summary>Başarı veya bilgilendirici kontrol; L4 hatası değil.</summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    [JsonStringEnumMemberName("missingTable")]
    MissingTable,

    [JsonStringEnumMemberName("queryFailed")]
    QueryFailed,

    /// <summary>Beklenen sütun/tür uyumsuzluğu (ileride ayrı dedektörlerle doldurulabilir).</summary>
    [JsonStringEnumMemberName("schemaMismatch")]
    SchemaMismatch,

    [JsonStringEnumMemberName("rowCountUnexpected")]
    RowCountUnexpected,

    [JsonStringEnumMemberName("integrityCheckFailed")]
    IntegrityCheckFailed,

    [JsonStringEnumMemberName("migrationHistoryMissing")]
    MigrationHistoryMissing,

    /// <summary>Sınıflandırılamayan veya ön koşul/çalışma zamanı hatası; başarı sayılmaz.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,

    /// <summary>L5 uygulama dumanı (SQL satırı değil); kanıt rollup / birleşik tüketim için.</summary>
    [JsonStringEnumMemberName("smokeFailed")]
    SmokeFailed
}

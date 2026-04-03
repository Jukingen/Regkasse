using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Tek bir post-restore SQL kontrolünün sonucu.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PostRestoreSqlCheckStatus
{
    [JsonStringEnumMemberName("passed")]
    Passed,

    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Örn. sorgu hatası; kanıt üretildi, sonuç kesin değil.</summary>
    [JsonStringEnumMemberName("inconclusive")]
    Inconclusive
}

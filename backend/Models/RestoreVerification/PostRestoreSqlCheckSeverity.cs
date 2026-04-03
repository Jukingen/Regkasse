using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Kontrolün L4 kararına etkisi (abartılı kanıt iddiasını önlemek için).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PostRestoreSqlCheckSeverity
{
    /// <summary>L4 (post-restore süreklilik) için zorunlu; geçmezse drill bu katmanda başarısız sayılır.</summary>
    [JsonStringEnumMemberName("requiredForL4")]
    RequiredForL4,

    /// <summary>Kanıtta yer alır; L4 geçiş koşuluna dahil edilmez.</summary>
    [JsonStringEnumMemberName("informative")]
    Informative
}

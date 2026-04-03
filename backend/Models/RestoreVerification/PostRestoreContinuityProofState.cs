using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// L4 dilimi: izole geri yüklenen DB üzerindeki yapılandırılmış süreklilik SQL sonucu (tam uygulama kurtarılabilirliği değil).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PostRestoreContinuityProofState
{
    /// <summary>SQL çalıştırılmadı veya ön koşul yok (ör. bağlantı eksik).</summary>
    [JsonStringEnumMemberName("notExecuted")]
    NotExecuted = 0,

    /// <summary>Çalıştırıldı; en az bir kritik kontrol başarısız.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed = 1,

    /// <summary>Çalıştırıldı ve yapılandırılmış kritik kontroller geçti.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed = 2,

    /// <summary>Çalıştırıldı; kritik kontroller net geçmedi/kalıcı başarısızlık yok — sonuç muhafazakâr belirsiz.</summary>
    [JsonStringEnumMemberName("inconclusive")]
    Inconclusive = 3
}

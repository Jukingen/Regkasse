namespace KasseAPI_Final.Configuration;

/// <summary>
/// Yedek artefakt saklama politikası: silme işi varsayılan kapalı; önce politika beyanı ve gözlemlenebilirlik.
/// </summary>
public enum BackupRetentionPolicyMode
{
    /// <summary>Politika devre dışı; ArtifactRetentionDays kullanılmaz.</summary>
    Disabled = 0,

    /// <summary>
    /// Salt yapılandırma / hazır okuma: gün sayısı doğrulanır; otomatik silme yok.
    /// </summary>
    ReportOnly = 1,

    /// <summary>
    /// Gelecekte zamanlanmış silme için rezerve; şu an silme yok — sağlık Degraded ile bildirilir.
    /// </summary>
    ExecutionPlanned = 2
}

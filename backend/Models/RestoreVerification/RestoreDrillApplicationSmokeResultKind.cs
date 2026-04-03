namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Geri yüklenen kopyaya karşı uygulama düzeyi duman sonucu (L5 — izole DB).
/// </summary>
public enum RestoreDrillApplicationSmokeResultKind
{
    /// <summary>Yapılandırma kapalı veya bu çalıştırmada çalıştırılmadı.</summary>
    NotAttempted,

    /// <summary>Tüm zorunlu minimal kontroller geçti.</summary>
    Passed,

    /// <summary>Bağlantı, şema veya kritik okuma başarısız — drill başarısız sayılabilir.</summary>
    Failed,

    /// <summary>Ortam bu kontrol türünü desteklemiyor (ör. sağlayıcı uyumsuz).</summary>
    NotSupported,

    /// <summary>Belirsiz sonuç (ör. yedek şema uygulama kodundan geride — bekleyen migrasyon).</summary>
    Inconclusive
}

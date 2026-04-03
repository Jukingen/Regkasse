namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Makine tarafından işlenebilir hata sınıfı; UI’dan bağımsız operatör anlamı.
/// </summary>
public enum RestoreDrillFailureCategory : short
{
    None = 0,

    /// <summary>Dump dosyası / artefakt çözümlemesi.</summary>
    ArtifactResolution = 1,

    /// <summary><c>pg_restore --list</c> veya TOC.</summary>
    PgRestoreList = 2,

    /// <summary>İzole <c>pg_restore</c> süreci.</summary>
    IsolatedPgRestore = 3,

    /// <summary>Geri yükleme sonrası iş sürekliliği SQL kontrolleri.</summary>
    PostRestoreContinuitySql = 4,

    /// <summary>Yapılandırılmış fiscal betik (ayrı bağlantı).</summary>
    FiscalSqlScript = 5,

    /// <summary>Canlı operasyonel bütünlük raporu.</summary>
    LiveOperationalIntegrity = 6,

    /// <summary>Eksik bağlantı dizesi, prod güvenlik kısıtı vb.</summary>
    Configuration = 7,

    /// <summary>İptal / zaman aşımı.</summary>
    CancelledOrTimeout = 8,

    /// <summary>Kurtarma duman testi (HTTP).</summary>
    ApplicationSmokeProbe = 9,

    /// <summary>Geri yüklenen izole DB üzerinde in-process uygulama dumanı (L5a).</summary>
    RestoredDatabaseApplicationSmoke = 10,

    /// <summary>Beklenmeyen istisna.</summary>
    UnhandledException = 99
}

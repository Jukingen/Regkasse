namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Restore drill ilerleme evreleri; terminal başarı için sıralı kanıt zinciri (sayısal depolama için ayrık değerler).
/// </summary>
public enum RestoreDrillStage : short
{
    None = 0,

    /// <summary>Mantıksal dump artefaktı ve yedek çalıştırma kimliği çözüldü.</summary>
    ArtifactDiscovered = 10,

    /// <summary><c>pg_restore --list</c> / TOC okunabilir.</summary>
    PgRestoreListPassed = 20,

    /// <summary>İzole hedefe gerçek <c>pg_restore</c> tamamlandı (veya güvenli şekilde atlandı).</summary>
    RestoreAttemptPassed = 30,

    /// <summary>Geri yüklenen kopyada iş sürekliliği SQL kontrolleri geçti.</summary>
    PostRestoreContinuitySqlPassed = 40,

    /// <summary>Geri yüklenen izole kopyada in-process uygulama dumanı (EF/migrasyon + salt okunur okuma) geçti.</summary>
    RestoredDatabaseApplicationSmokePassed = 45,

    /// <summary>Yapılandırılmış bağlantıda fiscal go-live betiği (ayrı kapsam).</summary>
    FiscalSqlScriptPassed = 50,

    /// <summary>Canlı operasyonel DB üzerinde read-only integrity (ayrı kapsam).</summary>
    LiveOperationalIntegrityPassed = 60,

    /// <summary>Yapılandırılmış taban URL’ye HTTP duman testi (ayrı dağıtım; bu API ile aynı olmak zorunda değil).</summary>
    ApplicationSmokePassed = 70,

    /// <summary>Tüm yapılandırılmış aşamalar başarıyla tamamlandı.</summary>
    Completed = 100
}

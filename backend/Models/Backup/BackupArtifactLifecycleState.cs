namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Staging → staging verified → optional external copy verified (see backup pipeline runbook).
/// </summary>
public enum BackupArtifactLifecycleState
{
    /// <summary>Yürütme tamam; staging doğrulaması (checksum) henüz geçmedi veya run henüz o aşamada değil.</summary>
    Staging = 0,

    /// <summary>Staging tarafı finalize: artifact verification (metadata + isteğe bağlı disk SHA-256) Passed; harici kopya gerekmez veya sırada.</summary>
    StagingVerified = 1,

    /// <summary>Harici arşive kopyalandı ve hedefte post-copy SHA-256 staging ile eşleşti.</summary>
    ExternalCopyVerified = 2,

    /// <summary>Harici kopya veya post-copy checksum başarısız; run VerificationFailed / ilgili kod ile kapanır (sessiz success yok).</summary>
    ExternalCopyFailed = 3
}

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Harici arşiv katmanında uygulamanın depolama immutability’sini ne ölçüde kanıtladığı / zorladığı (politika bayraklarından ayrı).
/// </summary>
public enum BackupExternalArchiveImmutabilityEnforcementKind
{
    /// <summary>Uygulama WORM/object-lock doğrulamaz; yalnızca kopya + hash vb.</summary>
    NotEnforcedByApplication = 0,

    /// <summary>Gelecekteki nesne depolama arka uçları: API nesne kilidini doğrudan doğrulayabilir (şu an uygulanmıyor).</summary>
    ApplicationEnforced = 1
}

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Kayıtlı <see cref="IBackupArtifactExternalArchive"/> uygulamasının yetenekleri — yapılandırma politikasından ayrıdır.
/// </summary>
public sealed record BackupExternalArchiveBackendDescriptor(
    string BackendKind,
    BackupExternalArchiveImmutabilityEnforcementKind ImmutabilityEnforcement,
    bool ApplicationEnforcesStorageImmutability,
    bool ObjectStorageImmutabilityBackendImplemented,
    string CapabilitySummaryEnglish);

/// <summary>DI’da yaygın arka uç tanımları.</summary>
public static class BackupExternalArchiveBackendDescriptors
{
    /// <summary>
    /// Yerel / paylaşımlı disk üzerinde kopya; WORM doğrulaması yok.
    /// </summary>
    public static readonly BackupExternalArchiveBackendDescriptor Filesystem = new(
        BackendKind: "Filesystem",
        ImmutabilityEnforcement: BackupExternalArchiveImmutabilityEnforcementKind.NotEnforcedByApplication,
        ApplicationEnforcesStorageImmutability: false,
        ObjectStorageImmutabilityBackendImplemented: false,
        CapabilitySummaryEnglish:
        "Copies verified staging artifacts under ExternalArchiveRoot with per-file post-copy SHA-256. Does not verify or enforce WORM/object-lock; any immutable tier must be provided by storage, appliance, or mount policy outside this API.");

    /// <summary>
    /// Değerlendirme çağrısında arşiv tanımlayıcı verilmediğinde muhafazakâr varsayılan (mevcut tek üretim uygulaması ile uyumlu).
    /// </summary>
    public static BackupExternalArchiveBackendDescriptor AssumedWhenCallerOmitsRegistration => Filesystem;

    /// <summary>
    /// Yalnızca test: ileride eklenecek nesne depolama + native immutability için beklenen şekil.
    /// </summary>
    public static readonly BackupExternalArchiveBackendDescriptor SimulatedApplicationEnforcedForTests = new(
        BackendKind: "SimulatedObjectStorage",
        ImmutabilityEnforcement: BackupExternalArchiveImmutabilityEnforcementKind.ApplicationEnforced,
        ApplicationEnforcesStorageImmutability: true,
        ObjectStorageImmutabilityBackendImplemented: true,
        CapabilitySummaryEnglish: "Test-only descriptor representing a future object-lock-capable archive backend.");
}

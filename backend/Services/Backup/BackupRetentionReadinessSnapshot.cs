using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Saklama politikasının yürütülebilirlik durumu; API varsayılanında otomatik silme yoktur.
/// </summary>
public sealed class BackupRetentionReadinessSnapshot
{
    public BackupRetentionPolicyMode Mode { get; init; }

    public int? ArtifactRetentionDays { get; init; }

    /// <summary>Backup:RetentionArtifactDeletionEnabled — şu an uygulama silme yapmaz; true başlangıç doğrulamasında reddedilir.</summary>
    public bool DeletionRequestedByConfiguration { get; init; }

    /// <summary>Otomatik artefakt silme işi henüz yok; gelecekte ExecutionPlanned ile hizalanacak.</summary>
    public bool AutomatedDeletionImplemented { get; init; }

    /// <summary>Makine tarafından kararlı kısa durum kodu (İngilizce).</summary>
    public string ExecutableStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> OperatorNotes { get; init; } = Array.Empty<string>();
}

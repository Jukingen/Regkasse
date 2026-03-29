using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>UI-safe artifact locators (no host paths, no secrets).</summary>
public static class BackupArtifactPublicFormatter
{
    public static string RedactedStagingLocator(BackupArtifactType type, string storageDescriptor)
    {
        var name = Path.GetFileName(storageDescriptor.Trim());
        if (string.IsNullOrEmpty(name))
            name = type.ToString();
        return $"staging/{type}/{name}";
    }
}

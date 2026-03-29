using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupExternalArchiveOutcome
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }

    /// <summary>Redacted locator per artifact type (e.g. archive/{runId}/file.dump).</summary>
    public IReadOnlyDictionary<BackupArtifactType, string> RedactedLocators { get; init; } =
        new Dictionary<BackupArtifactType, string>();
}

namespace KasseAPI_Final.Services.Backup;

public enum BackupArtifactDownloadPrepareStatus
{
    Ok,
    RunNotFound,
    ArtifactNotFound,
    RunNotSucceeded,
    FileNotOnDisk,
    InvalidConfiguration
}

/// <summary>
/// İndirme öncesi doğrulama sonucu; <see cref="BackupArtifactDownloadPrepareStatus.Ok"/> ise <see cref="AbsolutePath"/> okunabilir.
/// </summary>
public sealed class BackupArtifactDownloadPrepareResult
{
    public BackupArtifactDownloadPrepareStatus Status { get; init; }

    /// <summary>Tam dosya yolu (yalnızca Ok).</summary>
    public string? AbsolutePath { get; init; }

    /// <summary>Content-Disposition için güvenli dosya adı.</summary>
    public string? DownloadFileName { get; init; }

    public static BackupArtifactDownloadPrepareResult OkResult(string absolutePath, string downloadFileName) =>
        new()
        {
            Status = BackupArtifactDownloadPrepareStatus.Ok,
            AbsolutePath = absolutePath,
            DownloadFileName = downloadFileName
        };

    public static BackupArtifactDownloadPrepareResult Fail(BackupArtifactDownloadPrepareStatus status) =>
        new() { Status = status };
}

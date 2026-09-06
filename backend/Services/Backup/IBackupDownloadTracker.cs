using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupDownloadTrackRequest
{
    public required BackupRun Run { get; init; }
    public required Guid ArtifactId { get; init; }
    public required string FileName { get; init; }
    public long? FileSizeBytes { get; init; }
    public required string UserId { get; init; }
    public required string UserRole { get; init; }
    public Guid? AuditTenantId { get; init; }
    public string? CorrelationId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public interface IBackupDownloadTracker
{
    Task TrackAsync(BackupDownloadTrackRequest request, CancellationToken cancellationToken = default);
}

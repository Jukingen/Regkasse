using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Registers an operator-uploaded logical dump (and optional manifest) for a tenant without restoring the database.
/// </summary>
public interface IBackupArtifactImportService
{
    Task<BackupArtifactImportResponseDto> ImportAsync(
        BackupArtifactImportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BackupArtifactImportRequest
{
    public required Stream DumpStream { get; init; }

    public required string DumpFileName { get; init; }

    public long? DeclaredDumpLength { get; init; }

    public Stream? ManifestStream { get; init; }

    public string? ManifestFileName { get; init; }

    public required string RequestedByUserId { get; init; }

    public required string RequestedByRole { get; init; }

    public string? CorrelationId { get; init; }
}

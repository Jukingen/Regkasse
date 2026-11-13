using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Per-tenant acknowledgement of Backup/DR known gaps.
/// Stored on <c>tenant_settings</c> key <see cref="BackupKnownGapAcceptanceService.SettingsKey"/>.
/// Acceptance does not disable the underlying check and does not move restore-proof timestamps.
/// </summary>
public interface IBackupKnownGapAcceptanceService
{
    Task<BackupKnownGapAcceptanceDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the tenant's accepted id list. <paramref name="acceptedAtUtc"/> is the server clock supplied by the API.
    /// Unknown ids throw <see cref="BackupKnownGapUnknownException"/> (<c>BACKUP_KNOWN_GAP_UNKNOWN</c>).
    /// </summary>
    Task<BackupKnownGapAcceptanceDto> SaveAsync(
        Guid tenantId,
        IReadOnlyList<string> acceptedGapIds,
        string acceptedBy,
        DateTimeOffset acceptedAtUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>One or more accepted ids are not in <c>BackupKnownGaps.json</c>.</summary>
public sealed class BackupKnownGapUnknownException : Exception
{
    public const string ErrorCode = "BACKUP_KNOWN_GAP_UNKNOWN";

    public BackupKnownGapUnknownException(IReadOnlyList<string> invalidGapIds)
        : base("Unknown backup known-gap id.")
    {
        InvalidGapIds = invalidGapIds;
    }

    public IReadOnlyList<string> InvalidGapIds { get; }
}

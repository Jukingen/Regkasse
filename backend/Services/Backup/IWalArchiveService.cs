using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Inventory + retention of PostgreSQL WAL segments archived by host <c>archive_command</c>.
/// The API does not enable WAL archiving; it only reads and rotates files that Postgres wrote.
/// </summary>
public interface IWalArchiveService
{
    WalArchiveStatusDto GetStatus();

    /// <summary>
    /// Live archival observation from the existing segment inventory (file name + timestamps only).
    /// Does not read WAL payload bytes and does not include archive paths.
    /// </summary>
    WalArchiveObservation GetObservation();

    IReadOnlyList<WalArchiveFileInfo> ListFiles(int take = 200);

    /// <summary>Deletes files older than <c>Backup:WalArchiveRetentionDays</c>. Returns deleted count.</summary>
    int PurgeExpired();

    bool CoversWindow(DateTime fromUtc, DateTime toUtc);
}

public sealed record WalArchiveFileInfo(
    string FileName,
    long ByteSize,
    DateTime LastWriteUtc);

using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Inventory + retention of PostgreSQL WAL segments archived by host <c>archive_command</c>.
/// The API does not enable WAL archiving; it only reads and rotates files that Postgres wrote.
/// </summary>
public interface IWalArchiveService
{
    WalArchiveStatusDto GetStatus();

    IReadOnlyList<WalArchiveFileInfo> ListFiles(int take = 200);

    /// <summary>Deletes files older than <c>Backup:WalArchiveRetentionDays</c>. Returns deleted count.</summary>
    int PurgeExpired();

    bool CoversWindow(DateTime fromUtc, DateTime toUtc);
}

public sealed record WalArchiveFileInfo(
    string FileName,
    long ByteSize,
    DateTime LastWriteUtc);

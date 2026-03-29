namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// SHA-256 for backup artifacts (integrity only; not restore proof).
/// </summary>
/// <remarks>Yedek dosyaları için hash üretimi; restore doğrulaması değildir.</remarks>
public interface IBackupChecksumService
{
    Task<string> ComputeFileSha256HexAsync(string absoluteFilePath, CancellationToken cancellationToken = default);

    Task<bool> FileMatchesSha256Async(
        string absoluteFilePath,
        string expectedLowerHex,
        CancellationToken cancellationToken = default);

    string ComputeUtf8TextSha256Hex(string text);
}

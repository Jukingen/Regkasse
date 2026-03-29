using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupChecksumService : IBackupChecksumService
{
    public async Task<string> ComputeFileSha256HexAsync(string absoluteFilePath, CancellationToken cancellationToken = default)
    {
        await using var fs = File.OpenRead(absoluteFilePath);
        var hash = await SHA256.HashDataAsync(fs, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<bool> FileMatchesSha256Async(
        string absoluteFilePath,
        string expectedLowerHex,
        CancellationToken cancellationToken = default)
    {
        var actual = await ComputeFileSha256HexAsync(absoluteFilePath, cancellationToken);
        return string.Equals(actual, expectedLowerHex, StringComparison.Ordinal);
    }

    public string ComputeUtf8TextSha256Hex(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

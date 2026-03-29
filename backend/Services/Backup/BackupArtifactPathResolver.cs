using System.Diagnostics.CodeAnalysis;

namespace KasseAPI_Final.Services.Backup;

internal static class BackupArtifactPathResolver
{
    /// <summary>
    /// Resolves <paramref name="storageDescriptor"/> to an absolute path under <paramref name="stagingRootFull"/>.
    /// Supports legacy rows with rooted paths under the same staging root, or new relative file names.
    /// </summary>
    public static bool TryResolveStagingAbsolute(
        string? stagingRootFull,
        string storageDescriptor,
        [NotNullWhen(true)] out string? absolutePath)
    {
        absolutePath = null;
        if (string.IsNullOrWhiteSpace(stagingRootFull) || string.IsNullOrWhiteSpace(storageDescriptor))
            return false;

        var root = Path.GetFullPath(stagingRootFull.Trim());
        string candidate;
        if (Path.IsPathRooted(storageDescriptor))
            candidate = Path.GetFullPath(storageDescriptor);
        else
            candidate = Path.GetFullPath(Path.Combine(root, storageDescriptor));

        if (!BackupPathGuard.IsPathUnderStagingRoot(candidate, root))
            return false;

        absolutePath = candidate;
        return true;
    }
}

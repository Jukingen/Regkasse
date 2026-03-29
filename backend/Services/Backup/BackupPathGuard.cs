namespace KasseAPI_Final.Services.Backup;

internal static class BackupPathGuard
{
    /// <summary>True if <paramref name="path"/> is exactly <paramref name="stagingRootFull"/> or a file under it.</summary>
    public static bool IsPathUnderStagingRoot(string path, string stagingRootFull) =>
        IsPathUnderRoot(path, stagingRootFull);

    /// <summary>Same containment rule for staging or external archive roots.</summary>
    public static bool IsPathUnderRoot(string path, string rootFull)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(rootFull.Trim());
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;

        var sep = Path.DirectorySeparatorChar;
        var prefix = root.TrimEnd(sep) + sep;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}

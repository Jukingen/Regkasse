using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Local WORM archive under <see cref="BackupOptions.ExternalArchiveRoot"/>/cold.
/// Refuses overwrite; marks files read-only after write.
/// </summary>
public sealed class FilesystemWormCloudStorageProvider : ICloudStorageProvider
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<FilesystemWormCloudStorageProvider> _logger;

    public FilesystemWormCloudStorageProvider(
        IOptionsMonitor<BackupOptions> options,
        ILogger<FilesystemWormCloudStorageProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public CloudStorageProviderKind Kind => CloudStorageProviderKind.Filesystem;

    public bool IsConfigured
    {
        get
        {
            var root = _options.CurrentValue.ExternalArchiveRoot;
            return !string.IsNullOrWhiteSpace(root) && Path.IsPathRooted(root);
        }
    }

    public async Task<CloudStorageUploadResult> UploadAsync(
        CloudStorageUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveRoot(out var root))
        {
            return new CloudStorageUploadResult
            {
                Success = false,
                Provider = Kind,
                Error = "ExternalArchiveRoot is not configured or is not an absolute path."
            };
        }

        var dest = ResolvePath(root, request.ObjectKey);
        if (File.Exists(dest))
        {
            return new CloudStorageUploadResult
            {
                Success = false,
                Provider = Kind,
                Error = "WORM refuse: object already exists.",
                RedactedLocator = Redact(request.ObjectKey)
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        await File.WriteAllBytesAsync(dest, request.Content, cancellationToken).ConfigureAwait(false);
        try
        {
            File.SetAttributes(dest, File.GetAttributes(dest) | FileAttributes.ReadOnly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not mark cold archive file read-only: {Path}", dest);
        }

        return new CloudStorageUploadResult
        {
            Success = true,
            Provider = Kind,
            RedactedLocator = Redact(request.ObjectKey),
            ByteSize = request.Content.LongLength
        };
    }

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryResolveRoot(out var root))
            throw new InvalidOperationException("ExternalArchiveRoot is not configured.");

        var path = ResolvePath(root, objectKey);
        if (!File.Exists(path))
            throw new FileNotFoundException("Cold archive object not found.", objectKey);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryResolveRoot(out var root))
            return Task.FromResult(false);

        return Task.FromResult(File.Exists(ResolvePath(root, objectKey)));
    }

    private bool TryResolveRoot(out string root)
    {
        root = (_options.CurrentValue.ExternalArchiveRoot ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
            return false;
        return true;
    }

    private static string ResolvePath(string root, string objectKey)
    {
        var safe = objectKey.Replace('\\', '/').TrimStart('/');
        foreach (var segment in safe.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
                throw new InvalidOperationException("Invalid object key.");
        }

        return Path.GetFullPath(Path.Combine(root, "cold", safe.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string Redact(string objectKey) => "cold/" + objectKey.Replace('\\', '/').TrimStart('/');
}

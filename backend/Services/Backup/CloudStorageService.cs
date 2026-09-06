using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Dispatches cold-archive I/O to filesystem WORM, S3 Glacier, Azure Archive, or GCS Coldline.
/// </summary>
public sealed class CloudStorageService : ICloudStorageService
{
    public const string HttpClientName = "BackupCloudStorage";

    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly FilesystemWormCloudStorageProvider _filesystem;
    private readonly S3CompatibleCloudStorageProvider _s3;
    private readonly AzureArchiveCloudStorageProvider _azure;
    private readonly S3CompatibleCloudStorageProvider _gcs;

    public CloudStorageService(
        IOptionsMonitor<BackupOptions> options,
        FilesystemWormCloudStorageProvider filesystem,
        AzureArchiveCloudStorageProvider azure,
        IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _filesystem = filesystem;
        _azure = azure;
        var http = httpClientFactory.CreateClient(HttpClientName);
        _s3 = new S3CompatibleCloudStorageProvider(
            http,
            CloudStorageProviderKind.S3Glacier,
            () =>
            {
                var s = _options.CurrentValue.CloudStorage.S3;
                return (s.Bucket, s.Region, s.AccessKeyId, s.SecretAccessKey, s.Endpoint, s.StorageClass, s.ObjectLockEnabled);
            });
        _gcs = new S3CompatibleCloudStorageProvider(
            http,
            CloudStorageProviderKind.GcsColdline,
            () =>
            {
                var g = _options.CurrentValue.CloudStorage.Gcs;
                return (g.Bucket, "auto", g.AccessKeyId, g.SecretAccessKey, g.Endpoint, g.StorageClass, false);
            });
    }

    public CloudStorageProviderKind ResolvedProvider => ResolveActive().Kind;

    public bool IsConfigured => ResolveActive().IsConfigured;

    public Task<CloudStorageUploadResult> UploadAsync(
        CloudStorageUploadRequest request,
        CancellationToken cancellationToken = default) =>
        ResolveActive().UploadAsync(request, cancellationToken);

    public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default) =>
        ResolveActive().DownloadAsync(objectKey, cancellationToken);

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
        ResolveActive().ExistsAsync(objectKey, cancellationToken);

    public static CloudStorageProviderKind ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CloudStorageProviderKind.Filesystem;
        return Enum.TryParse<CloudStorageProviderKind>(value.Trim(), ignoreCase: true, out var kind)
            ? kind
            : CloudStorageProviderKind.Filesystem;
    }

    private ICloudStorageProvider ResolveActive()
    {
        var opts = _options.CurrentValue.CloudStorage;
        var kind = ParseProvider(opts.Provider);
        ICloudStorageProvider selected = kind switch
        {
            CloudStorageProviderKind.S3Glacier => _s3,
            CloudStorageProviderKind.AzureArchive => _azure,
            CloudStorageProviderKind.GcsColdline => _gcs,
            CloudStorageProviderKind.None => _filesystem,
            _ => _filesystem
        };

        if (!selected.IsConfigured && opts.FallbackToFilesystemWhenUnconfigured && _filesystem.IsConfigured)
            return _filesystem;

        return selected;
    }
}

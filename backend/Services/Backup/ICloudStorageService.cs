using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

public interface ICloudStorageService
{
    CloudStorageProviderKind ResolvedProvider { get; }

    bool IsConfigured { get; }

    Task<CloudStorageUploadResult> UploadAsync(
        CloudStorageUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}

public sealed class CloudStorageUploadRequest
{
    public required string ObjectKey { get; init; }
    public required byte[] Content { get; init; }
    public string? ContentSha256Hex { get; init; }
    public DateTime? ImmutableUntilUtc { get; init; }
    public string? ContentType { get; init; }
}

public sealed class CloudStorageUploadResult
{
    public bool Success { get; init; }
    public CloudStorageProviderKind Provider { get; init; }
    public string? RedactedLocator { get; init; }
    public string? Error { get; init; }
    public bool Deduplicated { get; init; }
    public long ByteSize { get; init; }
}

public interface ICloudStorageProvider
{
    CloudStorageProviderKind Kind { get; }

    bool IsConfigured { get; }

    Task<CloudStorageUploadResult> UploadAsync(
        CloudStorageUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);
}

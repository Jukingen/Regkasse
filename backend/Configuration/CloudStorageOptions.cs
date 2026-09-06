namespace KasseAPI_Final.Configuration;

/// <summary>
/// Cloud / WORM cold-archive credentials. Bound from <c>Backup:CloudStorage</c>.
/// Secrets must come from user-secrets / env — never from FA.
/// </summary>
public sealed class CloudStorageOptions
{
    /// <summary>None, Filesystem, S3Glacier, AzureArchive, GcsColdline.</summary>
    public string Provider { get; set; } = "Filesystem";

    /// <summary>
    /// When the selected cloud provider is missing credentials, use
    /// <see cref="BackupOptions.ExternalArchiveRoot"/> WORM copy instead.
    /// </summary>
    public bool FallbackToFilesystemWhenUnconfigured { get; set; } = true;

    public S3CloudStorageOptions S3 { get; set; } = new();

    public AzureCloudStorageOptions Azure { get; set; } = new();

    public GcsCloudStorageOptions Gcs { get; set; } = new();
}

public sealed class S3CloudStorageOptions
{
    public string? Bucket { get; set; }
    public string? Region { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string? Endpoint { get; set; }
    public string StorageClass { get; set; } = "DEEP_ARCHIVE";
    public bool ObjectLockEnabled { get; set; } = true;
}

public sealed class AzureCloudStorageOptions
{
    public string? AccountName { get; set; }
    public string? AccountKey { get; set; }
    public string? Container { get; set; }
    public string AccessTier { get; set; } = "Archive";
}

public sealed class GcsCloudStorageOptions
{
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public string StorageClass { get; set; } = "COLDLINE";
    public string Endpoint { get; set; } = "https://storage.googleapis.com";
}

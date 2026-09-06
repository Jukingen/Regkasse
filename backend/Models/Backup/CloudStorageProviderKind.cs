namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Cold-archive backend. Credentials stay in <c>Backup:CloudStorage</c> — never in FA payloads.
/// </summary>
public enum CloudStorageProviderKind
{
    None = 0,
    Filesystem = 1,
    S3Glacier = 2,
    AzureArchive = 3,
    GcsColdline = 4
}

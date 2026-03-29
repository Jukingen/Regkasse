namespace KasseAPI_Final.Services.Backup;

/// <summary>Serialized manifest + checksum of UTF-8 bytes (metadata integrity, not a substitute for the dump file).</summary>
public sealed record BackupManifestDocument(string JsonText, string ContentSha256LowerHex);

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Mantıksal pg_dump manifest girdileri. Parola yok; <see cref="DatabaseHost"/> yalnızca staging manifest dosyasına yazılır (admin DTO’larında dönülmez).
/// </summary>
public sealed record BackupLogicalManifestInput(
    Guid BackupRunId,
    string DatabaseName,
    string DatabaseHost,
    long ByteSize,
    string ContentHashSha256LowerHex,
    string DumpFileRelativeName,
    string ManifestFileRelativeName);

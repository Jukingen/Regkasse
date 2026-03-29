using System.Text.Json;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupManifestService : IBackupManifestService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IBackupChecksumService _checksum;

    public BackupManifestService(IBackupChecksumService checksum)
    {
        _checksum = checksum;
    }

    public BackupManifestDocument BuildLogicalPgDumpManifest(BackupLogicalManifestInput input)
    {
        var payload = new
        {
            schemaVersion = 1,
            kind = "pg_dump_logical_manifest",
            phase = 2,
            format = "custom_Fc",
            generatedAtUtc = DateTime.UtcNow.ToString("O"),
            runId = input.BackupRunId,
            database = input.DatabaseName,
            // Operasyonel manifest (staging dosyası); admin API DTO'larına sızmaz. Host adı gizli kabul edilmez — disk erişimi yetkilidir.
            host = input.DatabaseHost,
            byteSize = input.ByteSize,
            contentHashSha256 = input.ContentHashSha256LowerHex,
            stagingFiles = new
            {
                logicalDump = input.DumpFileRelativeName,
                manifest = input.ManifestFileRelativeName
            },
            notRestoreVerification = true,
            scope = "artifact_metadata_only",
            tseNote = "TSE/crypto backup remains vendor-specific and is not covered by this artifact."
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var checksum = _checksum.ComputeUtf8TextSha256Hex(json);
        return new BackupManifestDocument(json, checksum);
    }
}

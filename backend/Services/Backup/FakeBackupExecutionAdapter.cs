using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Development-safe adapter: no shell, no PostgreSQL binaries. Produces placeholder artifacts for pipeline testing.
/// </summary>
public sealed class FakeBackupExecutionAdapter : IBackupExecutionAdapter
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupChecksumService _checksum;

    public FakeBackupExecutionAdapter(IOptionsMonitor<BackupOptions> options, IBackupChecksumService checksum)
    {
        _options = options;
        _checksum = checksum;
    }

    public string AdapterKind => "Fake";

    public async Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context)
    {
        var configuredRoot = _options.CurrentValue.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            var descriptor = $"/tmp/regkasse-backup-stub/fake_logical_{context.BackupRunId:N}.dump";
            var meta = JsonSerializer.Serialize(new { phase = 1, note = "no real pg_dump", noStagingRoot = true });
            var hashInput = Encoding.UTF8.GetBytes(context.BackupRunId + descriptor + meta);
            var hash = Convert.ToHexString(SHA256.HashData(hashInput)).ToLowerInvariant();

            return new BackupExecutionResult
            {
                Success = true,
                Artifacts = new[]
                {
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = descriptor,
                        ByteSize = hashInput.Length,
                        ContentHashSha256 = hash,
                        MetadataJson = meta,
                        RequireOnDiskHashVerification = false
                    },
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.VerificationManifest,
                        StorageDescriptor = $"/tmp/regkasse-backup-stub/fake_manifest_{context.BackupRunId:N}.json",
                        ByteSize = meta.Length,
                        ContentHashSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(meta))).ToLowerInvariant(),
                        MetadataJson = "{\"kind\":\"fake-manifest\"}",
                        RequireOnDiskHashVerification = false
                    }
                }
            };
        }

        var root = Path.GetFullPath(configuredRoot.Trim());
        Directory.CreateDirectory(root);

        var logicalName = $"fake_logical_{context.BackupRunId:N}.dump";
        var manifestName = $"fake_manifest_{context.BackupRunId:N}.json";
        var logicalPath = Path.Combine(root, logicalName);
        var manifestPath = Path.Combine(root, manifestName);

        var payload = Encoding.UTF8.GetBytes($"fake-bytes-{context.BackupRunId:N}");
        await File.WriteAllBytesAsync(logicalPath, payload, context.CancellationToken);

        var metaOnDisk = JsonSerializer.Serialize(new { phase = 1, note = "no real pg_dump" });
        await File.WriteAllTextAsync(manifestPath, metaOnDisk, context.CancellationToken);

        var logicalHash = await _checksum.ComputeFileSha256HexAsync(logicalPath, context.CancellationToken);
        var manifestHash = await _checksum.ComputeFileSha256HexAsync(manifestPath, context.CancellationToken);

        return new BackupExecutionResult
        {
            Success = true,
            Artifacts = new[]
            {
                new BackupArtifactDescriptor
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = logicalName,
                    ByteSize = payload.Length,
                    ContentHashSha256 = logicalHash,
                    MetadataJson = metaOnDisk,
                    RequireOnDiskHashVerification = true
                },
                new BackupArtifactDescriptor
                {
                    ArtifactType = BackupArtifactType.VerificationManifest,
                    StorageDescriptor = manifestName,
                    ByteSize = metaOnDisk.Length,
                    ContentHashSha256 = manifestHash,
                    MetadataJson = "{\"kind\":\"fake-manifest\"}",
                    RequireOnDiskHashVerification = true
                }
            }
        };
    }
}

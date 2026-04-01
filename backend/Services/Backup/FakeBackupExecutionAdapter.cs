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

    /// <summary>Test-only: doluysa <see cref="ExecuteAsync"/> sonuç üretmeden bu exception’ı fırlatır.</summary>
    public Exception? ThrowOnExecuteForTests { get; set; }

    public async Task<BackupExecutionResult> ExecuteAsync(BackupExecutionContext context)
    {
        if (ThrowOnExecuteForTests != null)
            throw ThrowOnExecuteForTests;

        var configuredRoot = _options.CurrentValue.ArtifactStagingRoot;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            // Staging kökü yok: OS temp altında gerçek dosya üret (indirme / doğrulama ile uyumlu).
            var stubDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "regkasse-backup-stub"));
            Directory.CreateDirectory(stubDir);

            var stubLogicalName = $"fake_logical_{context.BackupRunId:N}.dump";
            var stubManifestName = $"fake_manifest_{context.BackupRunId:N}.json";
            var stubLogicalPath = Path.Combine(stubDir, stubLogicalName);
            var stubManifestPath = Path.Combine(stubDir, stubManifestName);

            var stubPayload = Encoding.UTF8.GetBytes($"fake-bytes-{context.BackupRunId:N}");
            await File.WriteAllBytesAsync(stubLogicalPath, stubPayload, context.CancellationToken);

            var stubMeta = JsonSerializer.Serialize(new { phase = 1, note = "no real pg_dump", noStagingRoot = true });
            await File.WriteAllTextAsync(stubManifestPath, stubMeta, context.CancellationToken);

            var stubLogicalHash = await _checksum.ComputeFileSha256HexAsync(stubLogicalPath, context.CancellationToken);
            var stubManifestHash = await _checksum.ComputeFileSha256HexAsync(stubManifestPath, context.CancellationToken);

            return new BackupExecutionResult
            {
                Success = true,
                Artifacts = new[]
                {
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = stubLogicalPath,
                        ByteSize = stubPayload.Length,
                        ContentHashSha256 = stubLogicalHash,
                        MetadataJson = stubMeta,
                        // Staging kökü yapılandırılmadığında doğrulama yalnızca metadata; dosya yine diskte (indirme için).
                        RequireOnDiskHashVerification = false
                    },
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.VerificationManifest,
                        StorageDescriptor = stubManifestPath,
                        ByteSize = stubMeta.Length,
                        ContentHashSha256 = stubManifestHash,
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

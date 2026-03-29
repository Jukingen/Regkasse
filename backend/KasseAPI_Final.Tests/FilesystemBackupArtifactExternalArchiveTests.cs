using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FilesystemBackupArtifactExternalArchiveTests
{
    [Fact]
    public void BackendDescriptor_documents_filesystem_limits_and_no_app_enforced_immutability()
    {
        var checksum = new BackupChecksumService();
        var archiveSvc = new FilesystemBackupArtifactExternalArchive(
            checksum,
            NullLogger<FilesystemBackupArtifactExternalArchive>.Instance);

        var d = archiveSvc.BackendDescriptor;
        Assert.Equal("Filesystem", d.BackendKind);
        Assert.False(d.ApplicationEnforcesStorageImmutability);
        Assert.False(d.ObjectStorageImmutabilityBackendImplemented);
        Assert.Equal(BackupExternalArchiveImmutabilityEnforcementKind.NotEnforcedByApplication, d.ImmutabilityEnforcement);
        Assert.Contains("SHA-256", d.CapabilitySummaryEnglish, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopyStagingArtifactsAsync_copies_and_post_copy_hash_matches()
    {
        var staging = Path.Combine(Path.GetTempPath(), "regkasse_staging_" + Guid.NewGuid().ToString("N"));
        var archive = Path.Combine(Path.GetTempPath(), "regkasse_archive_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(archive);
        var runId = Guid.NewGuid();

        try
        {
            var name = "blob.bin";
            var srcPath = Path.Combine(staging, name);
            await File.WriteAllTextAsync(srcPath, "hello-archive-test");

            var checksum = new BackupChecksumService();
            var hash = await checksum.ComputeFileSha256HexAsync(srcPath);

            var archiveSvc = new FilesystemBackupArtifactExternalArchive(
                checksum,
                NullLogger<FilesystemBackupArtifactExternalArchive>.Instance);

            var outcome = await archiveSvc.CopyStagingArtifactsAsync(
                runId,
                staging,
                archive,
                new[]
                {
                    new BackupArtifactDescriptor
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = name,
                        ContentHashSha256 = hash,
                        RequireOnDiskHashVerification = true
                    }
                });

            Assert.True(outcome.Success);
            var dest = Path.Combine(archive, runId.ToString("N"), name);
            Assert.True(File.Exists(dest));
            Assert.True(outcome.RedactedLocators.ContainsKey(BackupArtifactType.LogicalDump));
        }
        finally
        {
            try
            {
                Directory.Delete(staging, true);
                Directory.Delete(archive, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public async Task CopyStagingArtifactsAsync_when_archive_root_is_file_fails_closed()
    {
        var staging = Path.Combine(Path.GetTempPath(), "regkasse_staging_file_" + Guid.NewGuid().ToString("N"));
        var badRoot = Path.Combine(Path.GetTempPath(), "regkasse_not_a_dir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(badRoot, "not-a-directory");
        var runId = Guid.NewGuid();

        try
        {
            var checksum = new BackupChecksumService();
            var archiveSvc = new FilesystemBackupArtifactExternalArchive(
                checksum,
                NullLogger<FilesystemBackupArtifactExternalArchive>.Instance);

            var outcome = await archiveSvc.CopyStagingArtifactsAsync(
                runId,
                staging,
                badRoot,
                Array.Empty<BackupArtifactDescriptor>());

            Assert.False(outcome.Success);
            Assert.Equal("ARCHIVE_ROOT_NOT_A_DIRECTORY", outcome.ErrorCode);
        }
        finally
        {
            try
            {
                Directory.Delete(staging, true);
                File.Delete(badRoot);
            }
            catch
            {
                // ignore
            }
        }
    }
}

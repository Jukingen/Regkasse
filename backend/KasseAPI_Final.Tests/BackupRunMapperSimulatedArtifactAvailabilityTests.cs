using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Fake/ProductionStub çalıştırmalarında da disk üzerinde dosya varsa indirme DTO’su ve endpoint ile uyumlu olarak true olmalı.
/// </summary>
public sealed class BackupRunMapperSimulatedArtifactAvailabilityTests
{
    [Fact]
    public void Fake_succeeded_run_sets_IsFilePresentForDownload_true_when_file_resolves_on_disk()
    {
        var runId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var staging = Path.Combine(Path.GetTempPath(), "bk_map_fake_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        const string fileName = "stub.dump";
        var full = Path.Combine(staging, fileName);
        File.WriteAllText(full, "x");

        try
        {
            var run = new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                RequestedAt = DateTime.UtcNow,
                Artifacts =
                [
                    new BackupArtifact
                    {
                        Id = artifactId,
                        BackupRunId = runId,
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = fileName,
                        CreatedAt = DateTime.UtcNow,
                        LifecycleState = BackupArtifactLifecycleState.Staging,
                    }
                ]
            };

            var opts = new BackupOptions { ArtifactStagingRoot = staging };
            var env = new Mock<IHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
            var enrichment = new BackupDownloadEnrichment(opts, env.Object, NullLogger.Instance);

            var dto = BackupRunMapper.ToDto(
                run,
                includeChildren: true,
                pipelinePolicy: null,
                materializedChildren: true,
                automaticRetryMaxAttemptsBudget: null,
                downloadEnrichment: enrichment);

            Assert.NotNull(dto.Artifacts);
            var art = Assert.Single(dto.Artifacts);
            Assert.True(art.IsFilePresentForDownload);
        }
        finally
        {
            try
            {
                if (File.Exists(full))
                    File.Delete(full);
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    [Fact]
    public void Fake_succeeded_run_sets_IsFilePresentForDownload_false_when_file_missing_on_disk()
    {
        var runId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var staging = Path.Combine(Path.GetTempPath(), "bk_map_fake_miss_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var run = new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                RequestedAt = DateTime.UtcNow,
                Artifacts =
                [
                    new BackupArtifact
                    {
                        Id = artifactId,
                        BackupRunId = runId,
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = "missing.dump",
                        CreatedAt = DateTime.UtcNow,
                        LifecycleState = BackupArtifactLifecycleState.Staging,
                    }
                ]
            };

            var opts = new BackupOptions { ArtifactStagingRoot = staging };
            var env = new Mock<IHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
            var enrichment = new BackupDownloadEnrichment(opts, env.Object, NullLogger.Instance);

            var dto = BackupRunMapper.ToDto(
                run,
                includeChildren: true,
                pipelinePolicy: null,
                materializedChildren: true,
                automaticRetryMaxAttemptsBudget: null,
                downloadEnrichment: enrichment);

            Assert.NotNull(dto.Artifacts);
            var art = Assert.Single(dto.Artifacts);
            Assert.False(art.IsFilePresentForDownload);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}

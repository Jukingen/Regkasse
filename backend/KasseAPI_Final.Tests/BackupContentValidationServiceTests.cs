using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupContentValidationServiceTests
{
    [Fact]
    public async Task ValidateContent_Tenant_WithMatchingManifest_PersistsVerification()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regkasse-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tenantId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var counts = new Dictionary<string, int>
            {
                ["products.json"] = 0,
                ["categories.json"] = 0,
                ["customers.json"] = 0,
                ["payment_details.json"] = 2,
                ["receipts.json"] = 2,
            };
            var manifestName = "manifest.json";
            var manifestPath = Path.Combine(dir, manifestName);
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new { tableRowCounts = counts, kind = "tenant_logical_package" }));

            await using var db = CreateDb();
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantId,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "TenantLogical",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.VerificationManifest,
                        StorageDescriptor = manifestName,
                        ContentHashSha256 = new string('a', 64),
                        CreatedAt = DateTime.UtcNow,
                    }
                }
            });
            await db.SaveChangesAsync();

            var sut = CreateSut(db, dir);
            var result = await sut.ValidateContentAsync(runId);

            Assert.Equal(runId, result.RunId);
            Assert.Equal(nameof(BackupStrategyKind.Tenant), result.Strategy);
            Assert.Contains(
                result.OverallStatus,
                new[]
                {
                    BackupContentValidationStatuses.Passed,
                    BackupContentValidationStatuses.Partial,
                });
            Assert.Equal(result.OverallStatus, result.Status);
            Assert.NotEmpty(result.Tables);
            Assert.All(result.Tables, t => Assert.Equal(t.TableKey, t.TableName));
            Assert.NotEmpty(result.FiscalChecks);
            Assert.Contains(result.FiscalChecks, c => c.CheckName == "manifest_fiscal_presence");
            Assert.Contains(result.FiscalChecks, c => c.CheckName == "receipt_signature_chain");
            Assert.Contains(result.FiscalChecks, c => c.CheckName == "receipt_sequence_continuity");
            Assert.NotNull(result.VerificationId);

            var persisted = await db.BackupVerifications.SingleAsync(v => v.Id == result.VerificationId);
            Assert.Equal(IBackupContentValidationService.VerifierSourceContentValidation, persisted.VerifierSource);
            Assert.Equal(BackupVerificationStatus.Passed, persisted.Status);
            Assert.Contains("overallStatus", persisted.DetailsJson);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task ValidateContent_Tenant_DetectsSignatureChainBreak()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regkasse-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tenantId = Guid.NewGuid();
            var registerId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var ymd = DateTime.UtcNow.ToString("yyyyMMdd");
            var counts = new Dictionary<string, int>
            {
                ["products.json"] = 0,
                ["categories.json"] = 0,
                ["customers.json"] = 0,
                ["payment_details.json"] = 2,
                ["receipts.json"] = 2,
            };
            var manifestName = "manifest.json";
            await File.WriteAllTextAsync(
                Path.Combine(dir, manifestName),
                JsonSerializer.Serialize(new { tableRowCounts = counts }));

            await using var db = CreateDb();
            db.CashRegisters.Add(new CashRegister
            {
                Id = registerId,
                TenantId = tenantId,
                RegisterNumber = "KASSE1",
                Location = "Test",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                IsActive = true,
            });
            var paymentA = Guid.NewGuid();
            var paymentB = Guid.NewGuid();
            db.Receipts.AddRange(
                new Receipt
                {
                    ReceiptId = Guid.NewGuid(),
                    TenantId = tenantId,
                    PaymentId = paymentA,
                    CashRegisterId = registerId,
                    ReceiptNumber = $"AT-TSE-{ymd}-1",
                    IssuedAt = DateTime.UtcNow.AddMinutes(-2),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                    SubTotal = 1,
                    TaxTotal = 0,
                    GrandTotal = 1,
                    SignatureValue = "sig-a",
                    PrevSignatureValue = "",
                },
                new Receipt
                {
                    ReceiptId = Guid.NewGuid(),
                    TenantId = tenantId,
                    PaymentId = paymentB,
                    CashRegisterId = registerId,
                    ReceiptNumber = $"AT-TSE-{ymd}-2",
                    IssuedAt = DateTime.UtcNow.AddMinutes(-1),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                    SubTotal = 1,
                    TaxTotal = 0,
                    GrandTotal = 1,
                    SignatureValue = "sig-b",
                    PrevSignatureValue = "WRONG-PREV",
                });
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantId,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "TenantLogical",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.VerificationManifest,
                        StorageDescriptor = manifestName,
                        ContentHashSha256 = new string('b', 64),
                        CreatedAt = DateTime.UtcNow,
                    }
                }
            });
            await db.SaveChangesAsync();

            var sut = CreateSut(db, dir);
            var result = await sut.ValidateContentAsync(runId);

            Assert.Equal(BackupContentValidationStatuses.Failed, result.OverallStatus);
            Assert.Contains(result.FiscalChecks, c => c.CheckName == "receipt_signature_chain" && !c.Passed);
            Assert.NotNull(result.Fiscal);
            Assert.True(result.Fiscal!.ChainBreakCount > 0);

            var persisted = await db.BackupVerifications.SingleAsync();
            Assert.Equal(BackupVerificationStatus.Failed, persisted.Status);
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Validate_System_WithoutSectionCounts_ReturnsUnavailable()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "x.dump",
                    CreatedAt = DateTime.UtcNow,
                }
            }
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, Path.GetTempPath());
        var result = await sut.ValidateContentAsync(runId);

        Assert.Equal(BackupContentValidationStatuses.Unavailable, result.OverallStatus);
        Assert.Equal(nameof(BackupStrategyKind.System), result.Strategy);
        Assert.NotNull(result.VerificationId);
        var persisted = await db.BackupVerifications.SingleAsync();
        Assert.Equal(BackupVerificationStatus.Failed, persisted.Status);
    }

    [Fact]
    public async Task Validate_System_WithTenantSections_ReturnsPassed()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "CompositeSystem",
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.GlobalsDump,
                    StorageDescriptor = "system.zip",
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        sectionRowCounts = new Dictionary<string, int>
                        {
                            ["tenants/demo.tenant.zip"] = 100,
                            ["identity"] = 5,
                        }
                    }),
                    CreatedAt = DateTime.UtcNow,
                }
            }
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, Path.GetTempPath());
        var result = await sut.ValidateContentAsync(runId);

        Assert.Equal(BackupContentValidationStatuses.Passed, result.OverallStatus);
        Assert.Contains(result.Tables, t => t.TableKey == "tenants");
        Assert.NotNull(result.VerificationId);
    }

    [Fact]
    public async Task Validate_MissingRun_Throws()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db, Path.GetTempPath());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ValidateContentAsync(Guid.NewGuid()));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"content_val_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static BackupContentValidationService CreateSut(AppDbContext db, string stagingRoot)
    {
        var opts = new BackupOptions { ArtifactStagingRoot = stagingRoot, EncryptionEnabled = false };
        var monitor = new Mock<IOptionsMonitor<BackupOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(opts);
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        return new BackupContentValidationService(
            db,
            monitor.Object,
            new BackupEncryptionService(monitor.Object, NullLogger<BackupEncryptionService>.Instance),
            env.Object,
            NullLogger<BackupContentValidationService>.Instance);
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch
        {
            // ignore
        }
    }
}

using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportHistoryServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportHistory_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(LegacyDefaultTenantIds.Primary));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private sealed class NoOpValidationService : IDepExportValidationService
    {
        public Task<DepExportHistoryValidationResult> ValidateExportAsync(
            Guid exportId,
            string? exportJson = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DepExportHistoryValidationResult
            {
                ExportId = exportId,
                IsValid = true,
                ValidatedAt = DateTime.UtcNow,
            });

        public Task<DepExportValidationReport> GetValidationReportAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DepExportValidationReport { TenantId = tenantId });

        public Task<bool> IsExportValidAsync(Guid exportId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<DepExportHistoryValidationResult?> GetStoredValidationAsync(
            Guid exportId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DepExportHistoryValidationResult?>(new DepExportHistoryValidationResult
            {
                ExportId = exportId,
                IsValid = true,
                ValidatedAt = DateTime.UtcNow,
            });
    }

    private sealed class NoOpArchiveService : IDepExportArchiveService
    {
        public Task<DepExportArchiveResult> ArchiveExportAsync(
            Guid exportId,
            string? exportJson = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DepExportArchiveResult.Ok(
                exportId,
                archivePath: "noop",
                checksum: "abc",
                archivedAt: DateTime.UtcNow,
                retentionUntil: DateTime.UtcNow.AddYears(7)));

        public Task<DepExportArchiveReport> GetArchiveReportAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DepExportArchiveReport { TenantId = tenantId });

        public Task<DepExportPurgeResult> PurgeOldExportsAsync(
            int? retentionYears = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DepExportPurgeResult());

        public Task<int> ArchivePendingExportsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private static RksvDepExportRootDto SampleExport() =>
        new()
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    BelegeKompakt = ["a.b.c", "d.e.f"],
                },
            ],
        };

    private static DepExportHistoryService CreateService(AppDbContext db)
    {
        var archiveOpts = new Mock<IOptionsMonitor<DepExportArchiveOptions>>();
        archiveOpts.Setup(m => m.CurrentValue).Returns(new DepExportArchiveOptions
        {
            Enabled = false,
            AutoArchiveOnComplete = false,
        });

        return new(
            db,
            new FileNamingService(NullCurrentTenantAccessor.Instance),
            new DepExportRequirementService(db, TimeProvider.System),
            new NoOpValidationService(),
            new NoOpArchiveService(),
            Mock.Of<IDepExportPushNotificationService>(),
            Mock.Of<IDepExportAuditService>(),
            archiveOpts.Object,
            NullLogger<DepExportHistoryService>.Instance);
    }

    [Fact]
    public async Task RecordCompletedAsync_PersistsHistoryRow()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            FromUtc = from,
            ToUtc = to,
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        Assert.Equal(DepExportStatus.Completed.ToString(), row.Status);
        Assert.Equal(1, row.GroupCount);
        Assert.Equal(2, row.SignatureCount);
        Assert.True(row.FileSizeBytes > 0);
        Assert.StartsWith("dep-export_default_KASSE-01_", row.FileName);
        Assert.EndsWith(".json", row.FileName);

        var list = await service.ListAsync(LegacyDefaultTenantIds.Primary, regId);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal("KASSE-01", list.Items[0].RegisterNumber);
    }

    [Fact]
    public async Task RecordFailedAsync_PersistsFailedStatus()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var regId = Guid.NewGuid();

        var service = CreateService(db);
        var row = await service.RecordFailedAsync(
            LegacyDefaultTenantIds.Primary,
            regId,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            "system",
            "export failed");

        Assert.Equal(DepExportStatus.Failed.ToString(), row.Status);
        Assert.Equal("export failed", row.ErrorMessage);
    }
}

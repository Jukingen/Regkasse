using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
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
        return new AppDbContext(options, new FixedTenantAccessor(SystemTenantIds.Platform));
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

    private static DepExportHistoryService CreateService(AppDbContext db, string? storageRoot = null)
    {
        var archiveOpts = new Mock<IOptionsMonitor<DepExportArchiveOptions>>();
        archiveOpts.Setup(m => m.CurrentValue).Returns(new DepExportArchiveOptions
        {
            Enabled = false,
            AutoArchiveOnComplete = false,
        });

        var root = storageRoot ?? Path.Combine(Path.GetTempPath(), $"dep-export-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var storageOpts = new Mock<IOptionsMonitor<DepExportStorageOptions>>();
        storageOpts.Setup(m => m.CurrentValue).Returns(new DepExportStorageOptions
        {
            StorageRootRelativeDirectory = root,
            DownloadTokenTtlHours = 24,
            IssueDownloadTokenOnComplete = true,
        });

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        return new(
            db,
            new FileNamingService(NullCurrentTenantAccessor.Instance),
            new DepExportRequirementService(db, TimeProvider.System),
            new NoOpValidationService(),
            new NoOpArchiveService(),
            Mock.Of<IDepExportPushNotificationService>(),
            Mock.Of<IDepExportAuditService>(),
            Mock.Of<IRksvEnvironmentService>(),
            archiveOpts.Object,
            storageOpts.Object,
            env.Object,
            NullLogger<DepExportHistoryService>.Instance);
    }

    [Fact]
    public async Task RecordCompletedAsync_PersistsHistoryRow()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
            TenantId = SystemTenantIds.Platform,
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
        Assert.False(row.IsSimulated);
        Assert.Null(row.SimulationNote);
        Assert.StartsWith($"dep-export_{SystemTenantIds.PlatformSlug}_KASSE-01_", row.FileName);
        Assert.EndsWith(".json", row.FileName);
        Assert.False(string.IsNullOrWhiteSpace(row.StoragePath));
        Assert.True(File.Exists(row.StoragePath!));
        Assert.False(string.IsNullOrWhiteSpace(row.DownloadToken));
        Assert.NotNull(row.DownloadTokenExpiresAtUtc);
        Assert.True(row.DownloadTokenExpiresAtUtc > DateTime.UtcNow);

        var list = await service.ListAsync(SystemTenantIds.Platform, regId);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal("KASSE-01", list.Items[0].RegisterNumber);
        Assert.True(list.Items[0].HasStoredFile);
        Assert.False(list.Items[0].IsSimulated);

        var last = await service.GetLastExportAsync(SystemTenantIds.Platform, regId);
        Assert.True(last.HasExport);
        Assert.Equal(row.Id, last.ExportId);
        Assert.False(last.IsSimulated);
        Assert.Equal(row.FileName, last.FileName);
        Assert.True(list.Items[0].HasActiveDownloadToken);
        Assert.Equal(0, list.Items[0].DownloadCount);
    }

    [Fact]
    public async Task RecordCompletedAsync_StampsSimulationMetadata_WhenRequested()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "KASSE-02",
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
            IsSimulated = true,
            SimulationNote = RksvDepExportService.SimulationNoteEn,
        });

        Assert.True(row.IsSimulated);
        Assert.Equal(RksvDepExportService.SimulationNoteEn, row.SimulationNote);

        var last = await service.GetLastExportAsync(SystemTenantIds.Platform);
        Assert.True(last.HasExport);
        Assert.True(last.IsSimulated);
    }

    [Fact]
    public async Task GetLastExportAsync_ReturnsEmpty_WhenNoCompletedExports()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var service = CreateService(db);

        var last = await service.GetLastExportAsync(SystemTenantIds.Platform);
        Assert.False(last.HasExport);
        Assert.Null(last.LastExportAt);
    }

    [Fact]
    public async Task MarkDownloadedAsync_IncrementsDownloadCount()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        await service.MarkDownloadedAsync(row.Id);
        await service.MarkDownloadedAsync(row.Id);

        var detail = await service.GetByIdAsync(row.Id);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.DownloadCount);
        Assert.NotNull(detail.DownloadedAt);
    }

    [Fact]
    public async Task GetExportEntityByTokenAsync_ReturnsRow_WhenTokenValid()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var byToken = await service.GetExportEntityByTokenAsync(
            row.DownloadToken!,
            SystemTenantIds.Platform);
        Assert.NotNull(byToken);
        Assert.Equal(row.Id, byToken!.Id);

        var expired = await db.DepExportHistories.FirstAsync(h => h.Id == row.Id);
        expired.DownloadTokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        Assert.Null(await service.GetExportEntityByTokenAsync(
            row.DownloadToken!,
            SystemTenantIds.Platform));
    }

    [Fact]
    public async Task GetRecentExportsAsync_ReturnsNewestFirst()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        for (var i = 0; i < 3; i++)
        {
            await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
            {
                TenantId = SystemTenantIds.Platform,
                CashRegisterId = regId,
                FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                ExportedByUserId = "user-1",
                Export = SampleExport(),
                FileName = $"dep-export_default_KASSE-01_2026010{i + 1}T000000Z.json",
            });
        }

        var recent = await service.GetRecentExportsAsync(SystemTenantIds.Platform, limit: 2);
        Assert.Equal(2, recent.Count);
        Assert.True(recent[0].ExportedAt >= recent[1].ExportedAt);
    }

    [Fact]
    public async Task TryOpenDownloadAsync_ReturnsHotExpired_WhenPastExpiresAndNoFile()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var tracked = await db.DepExportHistories.FirstAsync(h => h.Id == row.Id);
        if (!string.IsNullOrWhiteSpace(tracked.StoragePath) && File.Exists(tracked.StoragePath))
            File.Delete(tracked.StoragePath);
        tracked.StoragePath = null;
        tracked.ArchivePath = null;
        tracked.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var attempt = await service.TryOpenDownloadAsync(row.Id, SystemTenantIds.Platform);
        Assert.Equal(DepExportDownloadFailureKind.HotExpired, attempt.Failure);
    }

    [Fact]
    public async Task TryOpenDownloadAsync_ReturnsStoredJsonStream()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var opened = await service.TryOpenDownloadAsync(row.Id, SystemTenantIds.Platform);
        Assert.Null(opened.Failure);
        Assert.NotNull(opened.Open);
        await using (opened.Open!.Stream)
        {
            using var reader = new StreamReader(opened.Open.Stream);
            var text = await reader.ReadToEndAsync();
            Assert.Contains("Belege", text, StringComparison.Ordinal);
            Assert.Equal(row.FileName, opened.Open.FileName);
            Assert.Equal("application/json", opened.Open.ContentType);
        }

        var byToken = await service.TryOpenDownloadByTokenAsync(
            row.DownloadToken!,
            SystemTenantIds.Platform);
        Assert.Null(byToken.Failure);
        Assert.NotNull(byToken.Open);
        await byToken.Open!.Stream.DisposeAsync();
    }

    [Fact]
    public async Task TryOpenDownloadByTokenAsync_ReturnsTokenExpired()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var tracked = await db.DepExportHistories.FirstAsync(h => h.Id == row.Id);
        tracked.DownloadTokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        var attempt = await service.TryOpenDownloadByTokenAsync(
            row.DownloadToken!,
            SystemTenantIds.Platform);
        Assert.Equal(DepExportDownloadFailureKind.TokenExpired, attempt.Failure);
        Assert.Null(attempt.Open);
    }

    [Fact]
    public async Task TryOpenDownloadAsync_RejectsWrongTenant()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var attempt = await service.TryOpenDownloadAsync(row.Id, Guid.NewGuid());
        Assert.Equal(DepExportDownloadFailureKind.ForbiddenTenant, attempt.Failure);
        Assert.Null(attempt.Open);
    }

    [Fact]
    public async Task CleanupExpiredStorageAsync_ClearsExpiredTokens()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var tracked = await db.DepExportHistories.FirstAsync(h => h.Id == row.Id);
        tracked.DownloadTokenExpiresAtUtc = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();

        var cleanup = await service.CleanupExpiredStorageAsync();
        Assert.True(cleanup.TokensCleared >= 1);

        var reloaded = await db.DepExportHistories.AsNoTracking().FirstAsync(h => h.Id == row.Id);
        Assert.Null(reloaded.DownloadToken);
        Assert.Null(reloaded.DownloadTokenExpiresAtUtc);
    }

    [Fact]
    public async Task IssueDownloadTokenAsync_RotatesToken()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var previous = row.DownloadToken;
        var issued = await service.IssueDownloadTokenAsync(row.Id);
        Assert.NotNull(issued);
        Assert.NotEqual(previous, issued!.Token);
        Assert.Equal(row.Id, issued.ExportId);
        Assert.Contains(issued.Token, issued.DownloadPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordFailedAsync_PersistsFailedStatus()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();

        var service = CreateService(db);
        var row = await service.RecordFailedAsync(
            SystemTenantIds.Platform,
            regId,
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            "system",
            "export failed");

        Assert.Equal(DepExportStatus.Failed.ToString(), row.Status);
        Assert.Equal("export failed", row.ErrorMessage);
    }

    [Fact]
    public async Task DeleteRecentExportAsync_SoftPurgesCompletedAndBlocksDownload()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        var deleted = await service.DeleteRecentExportAsync(row.Id, "user-1");
        Assert.True(deleted);

        var reloaded = await db.DepExportHistories.AsNoTracking().FirstAsync(h => h.Id == row.Id);
        Assert.NotNull(reloaded.PurgedAt);
        Assert.Null(reloaded.StoragePath);
        Assert.Null(reloaded.DownloadToken);

        var attempt = await service.TryOpenDownloadAsync(row.Id, SystemTenantIds.Platform);
        Assert.Equal(DepExportDownloadFailureKind.Purged, attempt.Failure);

        // Soft-purged rows are hidden from the recent-exports list.
        var list = await service.ListAsync(SystemTenantIds.Platform);
        Assert.Empty(list.Items);
        Assert.Equal(0, list.TotalCount);
    }

    [Fact]
    public async Task DeleteRecentExportAsync_HardDeletesFailedRow()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        var service = CreateService(db);
        var row = await service.RecordFailedAsync(
            SystemTenantIds.Platform,
            regId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            "user-1",
            "boom");

        var deleted = await service.DeleteRecentExportAsync(row.Id, "user-1");
        Assert.True(deleted);
        Assert.False(await db.DepExportHistories.AnyAsync(h => h.Id == row.Id));
    }

    [Fact]
    public async Task CleanupExpiredStorageAsync_HardDeletesStaleFailedMetadata()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        var service = CreateService(db);
        var row = await service.RecordFailedAsync(
            SystemTenantIds.Platform,
            regId,
            DateTime.UtcNow.AddDays(-40),
            DateTime.UtcNow.AddDays(-39),
            "user-1",
            "old failure");

        var tracked = await db.DepExportHistories.FirstAsync(h => h.Id == row.Id);
        tracked.ExportedAt = DateTime.UtcNow.AddDays(-40);
        await db.SaveChangesAsync();

        var cleanup = await service.CleanupExpiredStorageAsync();
        Assert.True(cleanup.MetadataRowsDeleted >= 1);
        Assert.False(await db.DepExportHistories.AnyAsync(h => h.Id == row.Id));
    }

    [Fact]
    public async Task RecordCompletedAsync_SetsExpiresAtFromHotRetention()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var before = DateTime.UtcNow;
        var row = await service.RecordCompletedAsync(new DepExportHistoryRecordRequest
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = regId,
            FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            Export = SampleExport(),
        });

        Assert.NotNull(row.ExpiresAt);
        Assert.True(row.ExpiresAt >= before.AddDays(6));
        Assert.True(row.ExpiresAt <= DateTime.UtcNow.AddDays(8));
    }
}

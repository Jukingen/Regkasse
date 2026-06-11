using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
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

        var service = new DepExportHistoryService(db, NullLogger<DepExportHistoryService>.Instance);
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

        var service = new DepExportHistoryService(db, NullLogger<DepExportHistoryService>.Instance);
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

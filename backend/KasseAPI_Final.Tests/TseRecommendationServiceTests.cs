using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseRecommendationServiceTests
{
    [Fact]
    public async Task GetRecommendationsAsync_FlagsMissingHealthyBackup()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithPrimaryAsync(db, score: 95, status: TseHealthStatus.Healthy);

        var svc = CreateService(db);
        var recs = await svc.GetRecommendationsAsync(tenantId);

        Assert.Contains(recs, r => r.Code == "ensure_healthy_backup");
        Assert.Contains(recs, r => r.Category == TseRecommendationCategories.Reliability);
        Assert.All(recs, r => Assert.True(r.DiagnosticOnly));
    }

    [Fact]
    public async Task GetRecommendationsAsync_FlagsExcessBackupsAsCost()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryReturningRegisterAsync(db);

        db.TseDevices.AddRange(
            Backup(tenantId, registerId, "B1"),
            Backup(tenantId, registerId, "B2"));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var recs = await svc.GetRecommendationsAsync(tenantId);

        Assert.Contains(recs, r => r.Code == "reduce_idle_backups" && r.Category == TseRecommendationCategories.Cost);
        Assert.Contains(recs, r => r.Code == "reduce_idle_backups" && r.EstimatedSavings > 0);
    }

    [Fact]
    public async Task ApplyAndRateRecommendation_UpdatesWorkflowState()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithPrimaryAsync(db, score: 40, status: TseHealthStatus.Degraded);

        var svc = CreateService(db);
        var recs = await svc.GetRecommendationsAsync(tenantId);
        var target = Assert.Single(recs, r => r.Code == "repair_degraded_devices");

        var applied = await svc.ApplyRecommendationAsync(target.Id, "actor-1");
        Assert.True(applied.Success);
        Assert.True(applied.Recommendation!.IsApplied);

        var rated = await svc.RateRecommendationAsync(target.Id, 4, "actor-1");
        Assert.True(rated.Success);
        Assert.Equal(4, rated.Rating);

        var dismissedOther = recs.First(r => r.Id != target.Id);
        var dismiss = await svc.DismissRecommendationAsync(dismissedOther.Id, "actor-1");
        Assert.True(dismiss.Success);

        var remaining = await svc.GetRecommendationsAsync(tenantId);
        Assert.DoesNotContain(remaining, r => r.Id == dismissedOther.Id);
        Assert.Contains(remaining, r => r.Id == target.Id && r.IsApplied && r.Rating == 4);
    }

    [Fact]
    public async Task RateRecommendationAsync_InvalidRating_Throws()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithPrimaryAsync(db, score: 95, status: TseHealthStatus.Healthy);
        var svc = CreateService(db);
        var recs = await svc.GetRecommendationsAsync(tenantId);
        var id = recs[0].Id;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            svc.RateRecommendationAsync(id, 0));
    }

    private static TseRecommendationService CreateService(AppDbContext db, TseOptions? opts = null)
    {
        return new TseRecommendationService(
            db,
            Options.Create(opts ?? new TseOptions
            {
                FailoverHealthyMinScore = 80,
                FailoverDegradedMinScore = 50,
                CertificateExpiringSoonDays = 30,
                CostMonthlyBackupDeviceEur = 5m,
                CostMonthlyPrimaryDeviceEur = 30m,
                CostPerFailoverEventEur = 2m,
                CostHighFailoverCountThreshold = 3,
                CostLowUtilizationDailyTxThreshold = 20,
            }).ToMonitor(),
            NullLogger<TseRecommendationService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_rec_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantWithPrimaryAsync(
        AppDbContext db,
        int score,
        TseHealthStatus status)
    {
        var (tenantId, _) = await SeedTenantWithPrimaryReturningRegisterAsync(db, score, status);
        return tenantId;
    }

    private static async Task<(Guid TenantId, Guid RegisterId)> SeedTenantWithPrimaryReturningRegisterAsync(
        AppDbContext db,
        int score = 95,
        TseHealthStatus status = TseHealthStatus.Healthy)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Rec Cafe",
            Slug = "rec-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-R1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "REC-P1",
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "rec-primary",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = status,
            HealthScore = score,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (tenantId, register.Id);
    }

    private static TseDevice Backup(Guid tenantId, Guid registerId, string serial) =>
        new()
        {
            SerialNumber = serial,
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = registerId,
            KassenId = registerId,
            DeviceId = serial.ToLowerInvariant(),
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
}

using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class TenantStatusLifecycleTests
{
    private static (TenantService Sut, AppDbContext Db) CreateSut(string dbName)
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options, tenantAccessor);
        var deletion = new Mock<ITenantDeletionService>();
        var audit = new Mock<IAuditLogService>();
        var sut = new TenantService(db, audit.Object, deletion.Object, NullLogger<TenantService>.Instance);
        return (sut, db);
    }

    [Fact]
    public void TenantStatuses_Normalize_maps_deleted_to_archived_and_enum_names()
    {
        Assert.Equal(TenantStatuses.Archived, TenantStatuses.Normalize("deleted"));
        Assert.Equal(TenantStatuses.Archived, TenantStatuses.Normalize("Archived"));
        Assert.Equal(TenantStatuses.InOnboarding, TenantStatuses.Normalize("InOnboarding"));
        Assert.Equal(TenantStatuses.InOnboarding, TenantStatuses.Normalize("in_onboarding"));
        Assert.Equal(TenantStatus.Archived, TenantStatuses.TryParse("deleted"));
        Assert.Equal(TenantStatus.Active, TenantStatuses.TryParse("active"));
    }

    [Fact]
    public async Task SoftDelete_sets_Cancelled_and_Restore_returns_Active()
    {
        var (sut, db) = CreateSut(nameof(SoftDelete_sets_Cancelled_and_Restore_returns_Active));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-lifecycle",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var (ok, err) = await sut.SoftDeleteAsync(tenantId, "actor-1");
        Assert.True(ok);
        Assert.Null(err);

        var row = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatuses.Cancelled, row.Status);
        Assert.False(row.IsActive);
        Assert.NotNull(row.DeletedAtUtc);

        var (restored, restoreErr) = await sut.RestoreAsync(tenantId, "actor-1");
        Assert.True(restored);
        Assert.Null(restoreErr);

        row = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatuses.Active, row.Status);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task CompleteOnboarding_InOnboarding_to_Active()
    {
        var (sut, db) = CreateSut(nameof(CompleteOnboarding_InOnboarding_to_Active));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "New",
            Slug = "new-onboard",
            Status = TenantStatuses.InOnboarding,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var (ok, err) = await sut.CompleteOnboardingAsync(tenantId, "actor-1");
        Assert.True(ok);
        Assert.Null(err);
        Assert.Equal(TenantStatuses.Active, (await db.Tenants.SingleAsync(t => t.Id == tenantId)).Status);
    }

    [Fact]
    public async Task SuspendForExpiredLicense_Active_to_Suspended()
    {
        var (sut, db) = CreateSut(nameof(SuspendForExpiredLicense_Active_to_Suspended));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Exp",
            Slug = "expiring",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var (ok, err) = await sut.SuspendForExpiredLicenseAsync(tenantId, "system");
        Assert.True(ok);
        Assert.Null(err);

        var row = await db.Tenants.SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TenantStatuses.Suspended, row.Status);
        Assert.False(row.IsActive);
    }

    [Fact]
    public async Task ArchiveExpiredCancellations_after_retention()
    {
        var (sut, db) = CreateSut(nameof(ArchiveExpiredCancellations_after_retention));
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = oldId,
                Name = "Old",
                Slug = "old-cancel",
                Status = TenantStatuses.Cancelled,
                IsActive = false,
                DeletedAtUtc = DateTime.UtcNow.AddDays(-31),
                CreatedAt = DateTime.UtcNow.AddDays(-60),
            },
            new Tenant
            {
                Id = recentId,
                Name = "Recent",
                Slug = "recent-cancel",
                Status = TenantStatuses.Cancelled,
                IsActive = false,
                DeletedAtUtc = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            });
        await db.SaveChangesAsync();

        var count = await sut.ArchiveExpiredCancellationsAsync(TimeSpan.FromDays(30), "system");
        Assert.Equal(1, count);
        Assert.Equal(TenantStatuses.Archived, (await db.Tenants.SingleAsync(t => t.Id == oldId)).Status);
        Assert.Equal(TenantStatuses.Cancelled, (await db.Tenants.SingleAsync(t => t.Id == recentId)).Status);
    }

    [Fact]
    public async Task SetStatusAsync_allows_any_known_status()
    {
        var (sut, db) = CreateSut(nameof(SetStatusAsync_allows_any_known_status));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Lead",
            Slug = "lead-co",
            Status = TenantStatuses.Lead,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var (ok, err) = await sut.SetStatusAsync(tenantId, TenantStatus.Suspended, "sa");
        Assert.True(ok);
        Assert.Null(err);
        Assert.Equal(TenantStatuses.Suspended, (await db.Tenants.SingleAsync(t => t.Id == tenantId)).Status);
    }
}

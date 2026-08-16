using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Trial;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TrialServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"trial_svc_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TrialService CreateService(
        AppDbContext db,
        TrialOptions? opts = null,
        ITenantService? tenantService = null,
        IEmailService? email = null)
    {
        var monitor = Mock.Of<IOptionsMonitor<TrialOptions>>(m => m.CurrentValue == (opts ?? new TrialOptions()));
        var conversion = new Mock<ITrialConversionService>();
        conversion
            .Setup(c => c.ConvertToPaidAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, Guid saleId, bool _, string? __, string? ___, string? ____, CancellationToken _____) =>
                (new TrialConversionResult(
                    true,
                    tenantId,
                    saleId,
                    DateTime.UtcNow.AddDays(365),
                    DateTime.UtcNow,
                    0,
                    "12_months",
                    "KEY"), null));

        var tenants = tenantService;
        if (tenants == null)
        {
            var tenantMock = new Mock<ITenantService>();
            tenantMock
                .Setup(t => t.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, (string?)null));
            tenants = tenantMock.Object;
        }

        return new TrialService(
            db,
            monitor,
            email ?? Mock.Of<IEmailService>(),
            Mock.Of<IActivityEventService>(),
            tenants,
            conversion.Object,
            Mock.Of<ILogger<TrialService>>());
    }

    [Fact]
    public void ResolveDurationDays_UsesAllowedOverrideOrDefault()
    {
        var service = CreateService(CreateDb());
        Assert.Equal(14, service.ResolveDurationDays(null));
        Assert.Equal(30, service.ResolveDurationDays(30));
        Assert.Equal(14, service.ResolveDurationDays(15)); // not allowed → default
    }

    [Fact]
    public async Task GrantOrRestartTrialAsync_SetsManagedTrialColumns()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial Cafe",
            Slug = "trial-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.GrantOrRestartTrialAsync(tenantId, 14, "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(TrialStatuses.Active, result!.TrialStatus);

        var reloaded = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TrialStatuses.Active, reloaded.TrialStatus);
        Assert.NotNull(reloaded.TrialStartedAtUtc);
        Assert.NotNull(reloaded.TrialEndsAtUtc);
        Assert.Equal(reloaded.TrialEndsAtUtc, reloaded.LicenseValidUntilUtc);
        Assert.Null(reloaded.LicenseKey);
    }

    [Fact]
    public async Task ProcessExpiryAndGraceAsync_MarksExpiredAndSetsGrace()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Expired Trial",
            Slug = "expired-trial",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Active,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(-1),
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new TrialOptions { GracePeriodDays = 7 });
        var count = await service.ProcessExpiryAndGraceAsync();

        Assert.Equal(1, count);
        var reloaded = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TrialStatuses.Expired, reloaded.TrialStatus);
        Assert.NotNull(reloaded.TrialGracePeriodEndsAtUtc);
    }

    [Fact]
    public async Task ConvertToPaidAsync_DelegatesToConversionService()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Convert Me",
            Slug = "convert-me",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Converted,
            TrialConvertedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ConvertToPaidAsync(tenantId, Guid.NewGuid(), "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(TrialStatuses.Converted, result!.TrialStatus);
    }

    [Fact]
    public async Task GrantOrRestartTrialAsync_UnknownTenant_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var (_, error) = await service.GrantOrRestartTrialAsync(Guid.NewGuid(), 14, "actor");
        Assert.Equal("Tenant not found.", error);
    }

    [Fact]
    public async Task ExtendTrialAsync_ExtendsFromCurrentEndWhenInFuture()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var currentEnd = DateTime.UtcNow.AddDays(10);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Extend Me",
            Slug = "extend-me",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Active,
            TrialStartedAtUtc = DateTime.UtcNow.AddDays(-4),
            TrialEndsAtUtc = currentEnd,
            LicenseValidUntilUtc = currentEnd,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ExtendTrialAsync(tenantId, 14, "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.TrialEndsAtUtc >= currentEnd.AddDays(13));
        var reloaded = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TrialStatuses.Active, reloaded.TrialStatus);
        Assert.False(reloaded.TrialReminder7dSent);
    }

    [Fact]
    public async Task GetDashboardAsync_GroupsActiveExpiringAndExpired()
    {
        await using var db = CreateDb();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Active Far",
                Slug = "active-far",
                Status = TenantStatuses.Active,
                TrialStatus = TrialStatuses.Active,
                TrialEndsAtUtc = DateTime.UtcNow.AddDays(20),
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Expiring",
                Slug = "expiring",
                Status = TenantStatuses.Active,
                TrialStatus = TrialStatuses.Active,
                TrialEndsAtUtc = DateTime.UtcNow.AddDays(3),
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Expired",
                Slug = "expired",
                Status = TenantStatuses.Active,
                TrialStatus = TrialStatuses.Expired,
                TrialEndsAtUtc = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var dashboard = await CreateService(db).GetDashboardAsync();

        Assert.Equal(2, dashboard.ActiveCount);
        Assert.Equal(1, dashboard.ExpiringSoonCount);
        Assert.Equal(1, dashboard.ExpiredCount);
        Assert.Contains(dashboard.ExpiringSoon, t => t.Slug == "expiring");
    }

    [Fact]
    public async Task ProcessRemindersAsync_SendsOnceAtSevenDayAnchor()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Remind Me",
            Slug = "remind-me",
            Email = "owner@example.com",
            Status = TenantStatuses.Active,
            TrialStatus = TrialStatuses.Active,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(6).AddHours(20),
            TrialReminder7dSent = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var email = new Mock<IEmailService>();
        email.Setup(e => e.TrySendHtmlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(db, email: email.Object);
        var sent = await service.ProcessRemindersAsync();

        Assert.Equal(1, sent);
        var reloaded = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.True(reloaded.TrialReminder7dSent);

        var sentAgain = await service.ProcessRemindersAsync();
        Assert.Equal(0, sentAgain);
        email.Verify(
            e => e.TrySendHtmlAsync("owner@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessExpiryAndGraceAsync_Disabled_ReturnsZero()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "X",
            Slug = "x",
            Status = TenantStatuses.Active,
            TrialStatus = TrialStatuses.Active,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var count = await CreateService(db, new TrialOptions { Enabled = false }).ProcessExpiryAndGraceAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ProcessCleanupAsync_SoftDeletesExpiredPastGrace()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cleanup",
            Slug = "cleanup",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Expired,
            TrialGracePeriodEndsAtUtc = DateTime.UtcNow.AddDays(-40),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var count = await CreateService(db, new TrialOptions { AutoDeleteAfterGraceDays = 30 })
            .ProcessCleanupAsync();

        Assert.Equal(1, count);
        var reloaded = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TrialStatuses.Deleted, reloaded.TrialStatus);
        Assert.NotNull(reloaded.TrialDeletedAtUtc);
    }

    [Fact]
    public async Task GetAnalyticsAsync_ComputesConversionRateFromTrialRows()
    {
        await using var db = CreateDb();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "A",
                Slug = "a",
                Status = TenantStatuses.Active,
                TrialStatus = TrialStatuses.Active,
                TrialStartedAtUtc = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-5),
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "C",
                Slug = "c",
                Status = TenantStatuses.Active,
                TrialStatus = TrialStatuses.Converted,
                TrialStartedAtUtc = DateTime.UtcNow.AddDays(-20),
                TrialConvertedAtUtc = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-20),
            });
        await db.SaveChangesAsync();

        var analytics = await CreateService(db).GetAnalyticsAsync();
        Assert.Equal(1, analytics.ActiveTrials);
        Assert.Equal(1, analytics.ConvertedTrials);
        Assert.Equal(50d, analytics.ConversionRatePercent);
    }
}

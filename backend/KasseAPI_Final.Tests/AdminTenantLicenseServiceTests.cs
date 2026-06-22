using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantLicenseServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLicense_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static AdminTenantLicenseService CreateService(
        AppDbContext db,
        ILicenseReminderEmailSender? reminderSender = null) =>
        new(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            reminderSender ?? Mock.Of<ILicenseReminderEmailSender>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<AdminTenantLicenseService>>(),
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production),
            Options.Create(new TseOptions { TseMode = "Device" }),
            Options.Create(new LicenseOptions { Enabled = true }),
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false));

    [Fact]
    public async Task GetOverviewAsync_InDevelopment_ReturnsActiveForExpiredTenant()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Expired Dev",
            Slug = "expired-dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(-120),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new AdminTenantLicenseService(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILicenseReminderEmailSender>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<AdminTenantLicenseService>>(),
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development),
            Options.Create(new TseOptions { TseMode = "Device" }),
            Options.Create(new LicenseOptions { Enabled = false }),
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false));

        var overview = await service.GetOverviewAsync(tenantId);

        Assert.NotNull(overview);
        Assert.Equal("active", overview!.Status.Kind);
        Assert.Equal(999, overview.Status.DaysRemaining);
    }

    [Fact]
    public async Task ActivateTrialAsync_Sets_30_Day_End()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial Co",
            Slug = "trial-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ActivateTrialAsync(tenantId, "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("active", result!.Status.Kind);
        Assert.NotNull(result.Status.ValidUntilUtc);
        Assert.True(result.Status.DaysRemaining is > 0 and <= 30);
    }

    [Fact]
    public async Task CheckDeploymentConsistencyAsync_TrialWithoutIssued_Returns_Warning_And_CanIssue()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var until = DateTime.UtcNow.AddDays(20);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial Co",
            Slug = "trial-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = until,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var (check, error) = await service.CheckDeploymentConsistencyAsync(tenantId);

        Assert.Null(error);
        Assert.NotNull(check);
        Assert.False(check!.IsConsistent);
        Assert.NotEmpty(check.Warnings);
        Assert.True(check.CanIssueDeploymentLicense);
        Assert.Null(check.MatchedIssuedLicenseId);
    }

    [Fact]
    public async Task CheckDeploymentConsistencyAsync_MatchingIssued_Is_Consistent()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var until = DateTime.UtcNow.AddDays(20);
        var key = "REGK-AAAAA-BBBBB-CCCCC";
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Linked Co",
            Slug = "linked-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = key,
            LicenseValidUntilUtc = until,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.IssuedLicenses.Add(new IssuedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = key,
            CustomerName = $"{tenant.Name} [tenant:{tenantId:D}]",
            ExpiryAtUtc = until,
            RequireFingerprint = false,
            SignedJwt = "eyJ.test",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var (check, error) = await service.CheckDeploymentConsistencyAsync(tenantId);

        Assert.Null(error);
        Assert.NotNull(check);
        Assert.True(check!.IsConsistent);
        Assert.Empty(check.Warnings);
        Assert.False(check.CanIssueDeploymentLicense);
    }

    [Fact]
    public async Task SendReminderEmailAsync_Sends_To_Owner_Email()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var userId = "owner-1";
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Reminder Co",
            Slug = "reminder-co",
            Email = "contact@reminder.test",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(12),
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "owner@reminder.test",
            Email = "owner@reminder.test",
            FirstName = "Owner",
            LastName = "Test",
            Role = "Manager",
            EmployeeNumber = "EMP-1",
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            IsActive = true,
            IsOwner = true,
        });
        await db.SaveChangesAsync();

        var reminderSender = new Mock<ILicenseReminderEmailSender>();
        reminderSender
            .Setup(x => x.TrySendTenantLicenseReminderAsync(
                "owner@reminder.test",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(db, reminderSender.Object);

        var (result, error) = await service.SendReminderEmailAsync(tenantId, "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("owner@reminder.test", result.RecipientEmail);
    }

    [Fact]
    public async Task ExtendAsync_WithKeyAndValidUntil_AppliesRequestedExpiry()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var key = "REGK-AAAAA-BBBBB-CCCCC";
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Extend Co",
            Slug = "extend-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var requestedUntil = DateTime.UtcNow.AddDays(90);
        var audit = new Mock<IAuditLogService>();
        var service = new AdminTenantLicenseService(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILicenseReminderEmailSender>(),
            audit.Object,
            Mock.Of<ILogger<AdminTenantLicenseService>>(),
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production),
            Options.Create(new TseOptions { TseMode = "Device" }),
            Options.Create(new LicenseOptions { Enabled = true }),
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false));

        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest { LicenseKey = key, ValidUntilUtc = requestedUntil },
            "manager-1",
            Roles.Manager);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(key, result!.Status.LicenseKey);
        Assert.NotNull(result.Status.ValidUntilUtc);
        Assert.Equal(
            requestedUntil.ToUniversalTime().Date,
            result.Status.ValidUntilUtc!.Value.ToUniversalTime().Date);

        audit.Verify(
            x => x.LogSystemOperationAsync(
                AuditLogActions.LICENSE_UPDATED,
                AuditLogEntityTypes.SYSTEM_CONFIG,
                "manager-1",
                Roles.Manager,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.LicenseUpdated,
                tenantId,
                tenantId),
            Times.Once);
    }

    [Fact]
    public async Task ExtendAsync_InvalidKeyFormat_ReturnsError()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Bad Key",
            Slug = "bad-key",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest { LicenseKey = "not-a-key", ValidUntilUtc = DateTime.UtcNow.AddDays(30) },
            "manager-1");

        Assert.Null(result);
        Assert.Equal(RegkTenantLicenseKeyFormat.InvalidFormatMessage, error);
    }
}

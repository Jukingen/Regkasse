using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
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
    private static readonly LicenseKeyGenerator BillingKeyGenerator = new();

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLicense_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TenantLicenseService CreateTenantLicenseService(AppDbContext db) =>
        new(db, BillingKeyGenerator);

    private static LicenseSale CreateBillingSale(
        Guid tenantId,
        string slug,
        DateTime validUntil,
        string? licenseKey = null,
        string plan = LicenseSalePlans.TwelveMonths,
        string status = LicenseSaleStatuses.Active)
    {
        licenseKey ??= BillingKeyGenerator.GenerateLicenseKey(slug, validUntil);
        var validFrom = plan switch
        {
            LicenseSalePlans.SixMonths => validUntil.AddDays(-183),
            _ => validUntil.AddDays(-365),
        };

        return new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = licenseKey,
            LicensePlan = plan,
            ValidFromUtc = validFrom,
            ValidUntilUtc = validUntil,
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            SoldByUserId = "super-admin-1",
            InvoiceNumber = $"RE{validUntil:yyyyMM}0001",
            Status = status,
        };
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
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false),
            CreateTenantLicenseService(db));

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
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false),
            CreateTenantLicenseService(db));

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
    public async Task PreviewLicenseAsync_WithValidBillingKey_ReturnsPreview()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        const string slug = "preview-co";
        var expiry = DateTime.UtcNow.AddDays(335);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, expiry);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Preview Co",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(CreateBillingSale(tenantId, slug, expiry, key));
        await db.SaveChangesAsync();

        var tenantLicenseService = CreateTenantLicenseService(db);
        var (result, error) = await tenantLicenseService.PreviewLicenseAsync(tenantId, key, isSuperAdmin: false);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Valid);
        Assert.Equal(key, result.LicenseKey);
        Assert.Equal("valid", result.Status);
        Assert.Equal("12-month license", result.PlanName);
        Assert.NotNull(result.DurationDays);
        Assert.True(result.DurationDays >= 360);
    }

    [Fact]
    public async Task PreviewLicenseAsync_WithExpiredBillingKey_ReturnsInvalidPreview()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        const string slug = "preview-co";
        var expiry = DateTime.UtcNow.AddDays(-1);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, expiry);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Preview Co",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(CreateBillingSale(tenantId, slug, expiry, key));
        await db.SaveChangesAsync();

        var tenantLicenseService = CreateTenantLicenseService(db);
        var (result, error) = await tenantLicenseService.PreviewLicenseAsync(tenantId, key, isSuperAdmin: false);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(result!.Valid);
        Assert.Equal("expired", result.ErrorCode);
        Assert.Equal("expired", result.Status);
    }

    [Fact]
    public async Task PreviewLicenseAsync_DoesNotModifyTenantLicense()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        const string slug = "preview-co";
        var expiry = DateTime.UtcNow.AddDays(365);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, expiry);
        var originalUntil = DateTime.UtcNow.AddDays(10);
        var originalKey = "REGK-OLDKY-OLDKY-OLDKY";
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Preview Co",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = originalKey,
            LicenseValidUntilUtc = originalUntil,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(CreateBillingSale(tenantId, slug, expiry, key));
        await db.SaveChangesAsync();

        var tenantLicenseService = CreateTenantLicenseService(db);
        var (result, error) = await tenantLicenseService.PreviewLicenseAsync(tenantId, key, isSuperAdmin: false);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Valid);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(originalKey, tenant.LicenseKey);
        Assert.Equal(originalUntil, tenant.LicenseValidUntilUtc);
    }

    [Fact]
    public async Task PreviewLicenseAsync_WhenSaleBelongsToOtherTenant_ReturnsWrongTenant()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        const string slug = "other-co";
        var expiry = DateTime.UtcNow.AddDays(365);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, expiry);
        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantId,
                Name = "Preview Co",
                Slug = "preview-co",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = otherTenantId,
                Name = "Other Co",
                Slug = slug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        db.LicenseSales.Add(CreateBillingSale(otherTenantId, slug, expiry, key));
        await db.SaveChangesAsync();

        var tenantLicenseService = CreateTenantLicenseService(db);
        var (result, error) = await tenantLicenseService.PreviewLicenseAsync(tenantId, key, isSuperAdmin: false);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(result!.Valid);
        Assert.Equal("wrong_tenant", result.ErrorCode);
    }

    [Fact]
    public async Task ExtendAsync_Manager_ResolvesExpiryFromBillingSale()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        const string slug = "lookup-co";
        var saleExpiry = DateTime.UtcNow.AddDays(365).Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, saleExpiry);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Lookup Co",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(CreateBillingSale(tenantId, slug, saleExpiry, key));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest { LicenseKey = key },
            "manager-1",
            Roles.Manager);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(key, result!.Status.LicenseKey);
        Assert.NotNull(result.Status.ValidUntilUtc);
        Assert.Equal(
            saleExpiry.ToUniversalTime(),
            result.Status.ValidUntilUtc!.Value.ToUniversalTime());
    }

    [Fact]
    public async Task ExtendAsync_ManagerCannotSetValidUntilUtc()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Manager Co",
            Slug = "manager-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest
            {
                LicenseKey = BillingKeyGenerator.GenerateLicenseKey("manager-co", DateTime.UtcNow.AddDays(30)),
                ValidUntilUtc = DateTime.UtcNow.AddDays(30),
            },
            "manager-1",
            Roles.Manager);

        Assert.Null(result);
        Assert.Equal("validUntilUtc is determined by the license key and cannot be set manually.", error);
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
            Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassLicense() == false),
            CreateTenantLicenseService(db));

        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest { LicenseKey = key, ValidUntilUtc = requestedUntil },
            "super-admin-1",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(key, result!.Status.LicenseKey);
        Assert.NotNull(result.Status.ValidUntilUtc);
        Assert.Equal(
            requestedUntil.ToUniversalTime().Date,
            result.Status.ValidUntilUtc!.Value.ToUniversalTime().Date);

        audit.Verify(
            x => x.LogSystemOperationAsync(
                AuditLogActions.LICENSE_EXTENDED,
                AuditLogEntityTypes.SYSTEM_CONFIG,
                "super-admin-1",
                Roles.SuperAdmin,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.LicenseExtended,
                tenantId,
                tenantId),
            Times.Once);
    }

    [Fact]
    public async Task ExtendAsync_WithExpiredBillingKey_ReturnsError()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        const string slug = "expired-co";
        var expiry = DateTime.UtcNow.AddDays(-1);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, expiry);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Expired Co",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(CreateBillingSale(tenantId, slug, expiry, key));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest { LicenseKey = key },
            "manager-1",
            Roles.Manager);

        Assert.Null(result);
        Assert.Equal("This license key has expired.", error);
    }

    [Fact]
    public async Task ExtendAsync_ManagerWithOtherTenantKey_ReturnsError()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        const string slug = "other-co";
        var expiry = DateTime.UtcNow.AddDays(90);
        var key = BillingKeyGenerator.GenerateLicenseKey(slug, expiry);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(CreateBillingSale(otherTenantId, slug, expiry, key));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.ExtendAsync(
            tenantId,
            new ExtendTenantLicenseRequest { LicenseKey = key },
            "manager-1",
            Roles.Manager);

        Assert.Null(result);
        Assert.Equal("This license key is not valid for this tenant.", error);
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
            new ExtendTenantLicenseRequest { LicenseKey = "not-a-key" },
            "manager-1",
            Roles.Manager);

        Assert.Null(result);
        Assert.Equal(LicenseKeyGenerator.InvalidFormatMessage, error);
    }
}

using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
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

        var service = new AdminTenantLicenseService(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILogger<AdminTenantLicenseService>>());
        var (result, error) = await service.ActivateTrialAsync(tenantId, "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("trial", result!.Status.Kind);
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

        var service = new AdminTenantLicenseService(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILogger<AdminTenantLicenseService>>());

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

        var service = new AdminTenantLicenseService(
            db,
            Mock.Of<ILicenseSyncService>(),
            Mock.Of<ILicenseIssuanceService>(),
            Mock.Of<ILogger<AdminTenantLicenseService>>());

        var (check, error) = await service.CheckDeploymentConsistencyAsync(tenantId);

        Assert.Null(error);
        Assert.NotNull(check);
        Assert.True(check!.IsConsistent);
        Assert.Empty(check.Warnings);
        Assert.False(check.CanIssueDeploymentLicense);
    }
}

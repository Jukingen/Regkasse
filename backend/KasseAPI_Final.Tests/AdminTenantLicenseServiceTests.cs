using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
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
            Mock.Of<ILogger<AdminTenantLicenseService>>());
        var (result, error) = await service.ActivateTrialAsync(tenantId, "actor");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("trial", result!.Status.Kind);
        Assert.NotNull(result.Status.ValidUntilUtc);
        Assert.True(result.Status.DaysRemaining is > 0 and <= 30);
    }
}

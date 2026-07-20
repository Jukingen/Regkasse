using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantServiceStatusModelTests
{
    [Fact]
    public void Defaults_enable_service_for_manager_preference()
    {
        var row = new TenantServiceStatus
        {
            TenantId = Guid.NewGuid(),
            ServiceType = TenantServiceTypes.Website,
        };

        Assert.True(row.IsEnabled);
        Assert.True(row.IsActive);
        Assert.Null(row.CustomPrice);
        Assert.True(row.IsAvailable);
        Assert.Equal(TenantDigitalServiceStatuses.None, row.Status);
        Assert.False(row.HasRequest);
    }

    [Fact]
    public void HasRequest_true_when_pending()
    {
        var row = new TenantServiceStatus
        {
            TenantId = Guid.NewGuid(),
            ServiceType = TenantServiceTypes.Website,
            Status = TenantDigitalServiceStatuses.Pending,
        };
        Assert.True(row.HasRequest);
    }

    [Fact]
    public void IsAvailable_requires_enabled_and_active()
    {
        var row = new TenantServiceStatus
        {
            TenantId = Guid.NewGuid(),
            ServiceType = TenantServiceTypes.App,
            IsEnabled = true,
            IsActive = false,
        };

        Assert.False(row.IsAvailable);

        row.IsActive = true;
        row.IsEnabled = false;
        Assert.False(row.IsAvailable);

        row.IsEnabled = true;
        Assert.True(row.IsAvailable);
    }

    [Fact]
    public void TenantServiceTypes_accepts_website_and_app_only()
    {
        Assert.True(TenantServiceTypes.IsValid("website"));
        Assert.True(TenantServiceTypes.IsValid("APP"));
        Assert.False(TenantServiceTypes.IsValid("pos"));
        Assert.False(TenantServiceTypes.IsValid(null));
    }

    [Fact]
    public async Task Persists_unique_per_tenant_and_service_type()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(Persists_unique_per_tenant_and_service_type) + Guid.NewGuid())
            .Options;

        await using var db = new AppDbContext(options);
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Status Cafe",
            Slug = "status-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.TenantServiceStatuses.Add(new TenantServiceStatus
        {
            TenantId = tenantId,
            ServiceType = TenantServiceTypes.Website,
            CustomPrice = 149.50m,
            ActivatedAt = DateTime.UtcNow,
        });
        db.TenantServiceStatuses.Add(new TenantServiceStatus
        {
            TenantId = tenantId,
            ServiceType = TenantServiceTypes.App,
            IsEnabled = true,
            IsActive = true,
        });

        await db.SaveChangesAsync();

        var rows = await db.TenantServiceStatuses
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.ServiceType)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(TenantServiceTypes.App, rows[0].ServiceType);
        Assert.Equal(TenantServiceTypes.Website, rows[1].ServiceType);
        Assert.Equal(149.50m, rows[1].CustomPrice);
    }

    [Fact]
    public void Ef_model_has_unique_tenant_service_type_index()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(Ef_model_has_unique_tenant_service_type_index) + Guid.NewGuid())
            .Options;

        using var db = new AppDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TenantServiceStatus));
        Assert.NotNull(entity);

        var unique = entity!.GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Count == 2);
        Assert.Equal(
            new[] { "TenantId", "ServiceType" },
            unique.Properties.Select(p => p.Name).ToArray());
        Assert.Equal("ux_tenant_service_statuses_tenant_type", unique.GetDatabaseName());
    }
}

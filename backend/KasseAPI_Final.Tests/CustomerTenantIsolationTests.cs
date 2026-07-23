using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Loyalty;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Tenant isolation for the (now tenant-scoped) <see cref="Customer"/> entity: EF query filter, system-guest exemption, and Super Admin cross-tenant visibility.</summary>
public sealed class CustomerTenantIsolationTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext(string databaseName, ICurrentTenantAccessor tenantAccessor)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, tenantAccessor);
    }

    private static Customer Regular(Guid tenantId, string name, string customerNumber, string email) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        CustomerNumber = customerNumber,
        Email = email,
        Phone = string.Empty,
        Address = string.Empty,
        TaxNumber = string.Empty,
        Notes = string.Empty,
        IsSystem = false,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static Customer Guest() => new()
    {
        Id = WalkInCustomerConstants.GuestCustomerId,
        TenantId = LegacyDefaultTenantIds.Primary,
        Name = "Walk-in Customer",
        CustomerNumber = "GUEST-000",
        Email = "walkin@system.local",
        Phone = string.Empty,
        Address = string.Empty,
        TaxNumber = string.Empty,
        Notes = "System guest",
        IsSystem = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static async Task SeedTwoTenantsAsync(string dbName)
    {
        // Seed with an unscoped accessor and explicit TenantIds so StampTenantIdsOnInsert leaves them intact.
        await using var seedDb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null));
        seedDb.Customers.AddRange(
            Guest(),
            Regular(TenantAId, "Alice A", "A-001", "alice@a.example"),
            Regular(TenantBId, "Bob B", "B-001", "bob@b.example"));
        await seedDb.SaveChangesAsync();
    }

    private static CustomerController CreateController(AppDbContext db, string role)
    {
        var repository = new GenericRepository<Customer>(db, Mock.Of<ILogger<GenericRepository<Customer>>>());
        var customerService = new CustomerService(db);
        var controller = new CustomerController(
            db,
            repository,
            Mock.Of<IPaymentService>(),
            customerService,
            Mock.Of<ILoyaltyService>(),
            Mock.Of<ICustomerExportService>(),
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>(),
            Mock.Of<ILogger<CustomerController>>());

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task EfFilter_TenantA_SeesOnlyOwnCustomersPlusSystemGuest()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        await SeedTwoTenantsAsync(dbName);

        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var visible = await db.Customers.AsNoTracking().ToListAsync();

        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, c => c.Name == "Alice A" && c.TenantId == TenantAId);
        Assert.Contains(visible, c => c.IsSystem); // guest visible cross-tenant
        Assert.DoesNotContain(visible, c => c.Name == "Bob B");
    }

    [Fact]
    public async Task EfFilter_TenantB_DoesNotSeeTenantACustomers()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        await SeedTwoTenantsAsync(dbName);

        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantBId));
        var visible = await db.Customers.AsNoTracking().ToListAsync();

        Assert.Contains(visible, c => c.Name == "Bob B" && c.TenantId == TenantBId);
        Assert.DoesNotContain(visible, c => c.Name == "Alice A");
    }

    [Fact]
    public async Task EfFilter_WithoutTenant_IsFailClosed_ButSystemGuestStaysVisible()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        await SeedTwoTenantsAsync(dbName);

        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null));
        var visible = await db.Customers.AsNoTracking().ToListAsync();

        // Fail-closed: no ambient tenant exposes no tenant-scoped rows, but the system guest remains resolvable.
        Assert.Single(visible);
        Assert.True(visible[0].IsSystem);
        Assert.Equal(3, await db.Customers.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task SystemGuest_IsResolvableUnderEveryTenant()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        await SeedTwoTenantsAsync(dbName);

        foreach (var tenantId in new[] { TenantAId, TenantBId })
        {
            await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(tenantId));
            var guest = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId);
            Assert.NotNull(guest);
            Assert.True(guest!.IsSystem);
        }
    }

    [Fact]
    public async Task GetAll_Manager_TenantA_ExcludesTenantBAndSystem()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        await SeedTwoTenantsAsync(dbName);

        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var controller = CreateController(db, Roles.Manager);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic payload = ok.Value!;
        var items = ((IEnumerable<Customer>)payload.data.items).ToList();

        Assert.Single(items);
        Assert.Equal("Alice A", items[0].Name);
    }

    [Fact]
    public async Task GetAll_SuperAdmin_SeesAllTenantsViaIgnoreQueryFilters()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        await SeedTwoTenantsAsync(dbName);

        // Ambient tenant is A, but Super Admin must retain cross-tenant visibility.
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var controller = CreateController(db, Roles.SuperAdmin);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic payload = ok.Value!;
        var items = ((IEnumerable<Customer>)payload.data.items).ToList();

        Assert.Contains(items, c => c.Name == "Alice A" && c.TenantId == TenantAId);
        Assert.Contains(items, c => c.Name == "Bob B" && c.TenantId == TenantBId);
    }

    [Fact]
    public void Model_DefinesPerTenantCompositeUniqueIndexes()
    {
        var dbName = $"CustomerIso_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var entity = db.Model.FindEntityType(typeof(Customer))!;
        var uniqueIndexes = entity.GetIndexes()
            .Where(ix => ix.IsUnique)
            .Select(ix => ix.Properties.Select(p => p.Name).ToArray())
            .ToList();

        Assert.Contains(uniqueIndexes, cols => cols.Length == 2 && cols[0] == nameof(Customer.TenantId) && cols[1] == nameof(Customer.CustomerNumber));
        Assert.Contains(uniqueIndexes, cols => cols.Length == 2 && cols[0] == nameof(Customer.TenantId) && cols[1] == nameof(Customer.Email));
        Assert.Contains(uniqueIndexes, cols => cols.Length == 2 && cols[0] == nameof(Customer.TenantId) && cols[1] == nameof(Customer.TaxNumber));
    }
}

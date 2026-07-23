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

public sealed class CustomerSystemProtectionTests
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CustomerSystem_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        // Customer is tenant-scoped; operate under an ambient tenant so non-system rows are visible.
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(TestTenantId));
    }

    private static CustomerController CreateController(AppDbContext db, string role)
    {
        var repository = new GenericRepository<Customer>(
            db,
            Mock.Of<ILogger<GenericRepository<Customer>>>());
        var customerService = new CustomerService(db);

        var controller = new CustomerController(
            db,
            repository,
            Mock.Of<IPaymentService>(),
            customerService,
            Mock.Of<ILoyaltyService>(),
            Mock.Of<ICustomerExportService>(),
            TenantTestDoubles.SettingsResolverReturning(TestTenantId),
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

    private static Customer CreateWalkInCustomer() => new()
    {
        Id = WalkInCustomerConstants.GuestCustomerId,
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

    private static Customer CreateRegularCustomer() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TestTenantId,
        Name = "Regular Customer",
        CustomerNumber = "CUST-001",
        Email = "regular@example.com",
        Phone = string.Empty,
        Address = string.Empty,
        TaxNumber = string.Empty,
        Notes = string.Empty,
        IsSystem = false,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task GetAll_Manager_ExcludesSystemCustomers()
    {
        await using var db = CreateDb();
        db.Customers.AddRange(CreateWalkInCustomer(), CreateRegularCustomer());
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.Manager);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic payload = ok.Value!;
        var data = payload.data;
        var items = (IEnumerable<Customer>)data.items;
        Assert.Single(items);
        Assert.Equal("Regular Customer", items.First().Name);
    }

    [Fact]
    public async Task GetAll_SuperAdmin_IncludesSystemCustomers()
    {
        await using var db = CreateDb();
        db.Customers.AddRange(CreateWalkInCustomer(), CreateRegularCustomer());
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.SuperAdmin);
        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic payload = ok.Value!;
        var data = payload.data;
        var items = ((IEnumerable<Customer>)data.items).ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, c => c.IsSystem);
    }

    [Fact]
    public async Task Delete_Manager_SystemCustomer_Returns403()
    {
        await using var db = CreateDb();
        var walkIn = CreateWalkInCustomer();
        db.Customers.Add(walkIn);
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.Manager);
        var result = await controller.Delete(walkIn.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.True(await db.Customers.AnyAsync(c => c.Id == walkIn.Id && c.IsActive));
    }

    [Fact]
    public async Task Update_Manager_SystemCustomer_Returns403()
    {
        await using var db = CreateDb();
        var walkIn = CreateWalkInCustomer();
        db.Customers.Add(walkIn);
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.Manager);
        walkIn.Name = "Renamed Walk-in";
        var result = await controller.Update(walkIn.Id, walkIn);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_Manager_RegularCustomer_Succeeds()
    {
        await using var db = CreateDb();
        var regular = CreateRegularCustomer();
        db.Customers.Add(regular);
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.Manager);
        var result = await controller.Delete(regular.Id);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.Customers.FirstAsync(c => c.Id == regular.Id);
        Assert.False(stored.IsActive);
    }
}

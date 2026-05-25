using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>GET api/admin/cash-registers tenant scoping (controller + management service).</summary>
public sealed class AdminCashRegistersListTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminCashRegList_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static AdminCashRegistersController CreateController(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        string actorRole)
    {
        var management = new CashRegisterManagementService(
            db,
            tenantResolver,
            Mock.Of<IAuditLogService>(),
            NullLogger<CashRegisterManagementService>.Instance);

        var controller = new AdminCashRegistersController(
            Mock.Of<ICashRegisterDecommissionService>(),
            management,
            NullLogger<AdminCashRegistersController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "actor-1"),
                        new Claim(ClaimTypes.Role, actorRole),
                    },
                    "Test")),
            },
        };

        return controller;
    }

    private static async Task SeedTwoTenantsWithRegistersAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Tenant A", Slug = "tenant-a", CreatedAt = now },
            new Tenant { Id = TenantBId, Name = "Tenant B", Slug = "tenant-b", CreatedAt = now });
        db.CashRegisters.AddRange(
            new CashRegister
            {
                TenantId = TenantAId,
                RegisterNumber = "A-1",
                Location = "A",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
            },
            new CashRegister
            {
                TenantId = TenantBId,
                RegisterNumber = "B-1",
                Location = "B",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
            });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ListCashRegisters_SuperAdmin_ReturnsAllTenantsRegisters()
    {
        // Super Admin tüm tenant'ların kasalarını görebilmeli
        await using var db = CreateDb();
        await SeedTwoTenantsWithRegistersAsync(db);

        var controller = CreateController(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            Roles.SuperAdmin);

        var result = await controller.List(null, null, page: 1, pageSize: 20, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<CashRegisterDto>>(ok.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, r => r.TenantId == TenantAId && r.RegisterNumber == "A-1");
        Assert.Contains(page.Items, r => r.TenantId == TenantBId && r.RegisterNumber == "B-1");
    }

    [Fact]
    public async Task ListCashRegisters_Manager_ReturnsOnlyOwnTenantRegisters()
    {
        // Manager sadece kendi tenant'ının kasalarını görebilmeli
        await using var db = CreateDb();
        await SeedTwoTenantsWithRegistersAsync(db);

        var controller = CreateController(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            Roles.Manager);

        var result = await controller.List(null, null, page: 1, pageSize: 20, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<CashRegisterDto>>(ok.Value);
        Assert.Equal(1, page.TotalCount);
        Assert.All(page.Items, r => Assert.Equal(TenantAId, r.TenantId));
        Assert.Equal("A-1", page.Items.First().RegisterNumber);
    }

    [Fact]
    public async Task ListCashRegisters_Manager_TriesToAccessOtherTenant_ReturnsForbid()
    {
        // Manager başka tenant'ın kasalarına erişememeli -> 403
        await using var db = CreateDb();
        await SeedTwoTenantsWithRegistersAsync(db);

        var controller = CreateController(
            db,
            TenantTestDoubles.SettingsResolverReturning(TenantAId),
            Roles.Manager);

        var result = await controller.List(
            TenantBId,
            null,
            page: 1,
            pageSize: 20,
            cancellationToken: CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}

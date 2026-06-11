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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CashRegisterControllerByTenantTests
{
    private static readonly Guid PrimaryTenantId = LegacyDefaultTenantIds.Primary;

    private static AppDbContext CreateContext(ICurrentTenantAccessor? tenantAccessor = null)
    {
        var accessor = tenantAccessor ?? TenantTestDoubles.TenantAccessorReturning(PrimaryTenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegByTenant_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, accessor);
        ctx.Tenants.Add(new Tenant { Id = PrimaryTenantId, Name = "Primary", Slug = "primary" });
        ctx.SaveChanges();
        return ctx;
    }

    private static CashRegisterController CreateController(
        AppDbContext ctx,
        ICurrentTenantAccessor tenantAccessor,
        ICashRegisterListEnrichmentService? enrichment = null)
    {
        var controller = new CashRegisterController(
            Mock.Of<ILogger<CashRegisterController>>(),
            ctx,
            new UserManager<ApplicationUser>(
                Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!),
            Mock.Of<ICashRegisterShiftService>(),
            TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId),
            tenantAccessor,
            Mock.Of<ICashRegisterManagementService>(),
            enrichment ?? CashRegisterTestDoubles.NoOpListEnrichment());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "viewer-1"),
                        new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CashRegisterView),
                    },
                    "Test")),
            },
        };

        return controller;
    }

    [Fact]
    public async Task GetCashRegistersByTenant_WithoutTenantContext_ReturnsBadRequest()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(null);
        await using var ctx = CreateContext(tenantAccessor);
        var controller = CreateController(ctx, tenantAccessor);

        var result = await controller.GetCashRegistersByTenant(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value) as string;
        Assert.Equal("No tenant selected", message);
    }

    [Fact]
    public async Task GetCashRegistersByTenant_OrdersDefaultRegisterFirst()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(PrimaryTenantId);
        await using var ctx = CreateContext(tenantAccessor);
        var defaultRegisterId = Guid.NewGuid();
        var otherRegisterId = Guid.NewGuid();
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                Id = otherRegisterId,
                TenantId = PrimaryTenantId,
                RegisterNumber = "KASSE-002",
                Location = "Bar",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                IsActive = true,
                IsDefaultForTenant = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(5),
            },
            new CashRegister
            {
                Id = defaultRegisterId,
                TenantId = PrimaryTenantId,
                RegisterNumber = "KASSE-001",
                Location = "Theke",
                StartingBalance = 0m,
                CurrentBalance = 0m,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                IsActive = true,
                IsDefaultForTenant = true,
                CreatedAt = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, tenantAccessor);

        var result = await controller.GetCashRegistersByTenant(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var registers = Assert.IsAssignableFrom<IReadOnlyList<CashRegisterDto>>(ok.Value);
        Assert.Equal(2, registers.Count);
        Assert.Equal(defaultRegisterId, registers[0].Id);
        Assert.True(registers[0].IsDefaultForTenant);
        Assert.Equal(otherRegisterId, registers[1].Id);
    }
}

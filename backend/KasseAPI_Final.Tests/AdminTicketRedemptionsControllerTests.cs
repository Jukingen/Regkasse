using System.Security.Claims;
using System.Runtime.CompilerServices;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTicketRedemptionsControllerTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task List_ReturnsHashedDisplayCode_NotPlaintext()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        var plaintext = "TKT-ADMINLIST01";
        var hash = TicketCodeHasher.HashRaw(plaintext);
        db.TicketRedemptions.Add(new TicketRedemption
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketCode = TicketCodeHasher.DisplayPrefix(hash),
            TicketCodeHash = hash,
            Status = TicketRedemptionStatus.Valid,
            ValidUntilUtc = DateTime.UtcNow.AddDays(10),
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.List(status: null, tenantId: null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TicketRedemptionListResponse>(ok.Value);
        var row = Assert.Single(body.Items);
        Assert.Equal(TicketCodeHasher.DisplayPrefix(hash), row.DisplayCode);
        Assert.DoesNotContain(plaintext, row.DisplayCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_RequiresProductView()
    {
        var permission = Assert.Single(
            typeof(AdminTicketRedemptionsController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.ProductView, permission.Permission);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminTickets_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static AdminTicketRedemptionsController CreateController(AppDbContext db)
    {
        var accessor = Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("Tenant accessor was not registered for the test context."));
        return new AdminTicketRedemptionsController(new TicketRedemptionService(db), accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "manager-1"),
                            new Claim(ClaimTypes.Role, Roles.Manager),
                        ],
                        authenticationType: "test")),
                },
            },
        };
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
        if (db.Tenants.IgnoreQueryFilters().Any(tenant => tenant.Id == tenantId))
            return;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"t-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
    }
}

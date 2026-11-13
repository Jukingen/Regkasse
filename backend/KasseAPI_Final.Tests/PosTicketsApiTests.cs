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

public sealed class PosTicketsApiTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task ValidateThenRedeem_Succeeds_ThenDoubleRedeemReturns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        var plaintext = "TKT-ABCD2345EFGH";
        SeedTicket(db, tenantId, plaintext, TicketRedemptionStatus.Valid, DateTime.UtcNow.AddDays(30));
        await db.SaveChangesAsync();

        var controller = CreateController(db, "cashier-1");
        var validated = await controller.Validate(plaintext, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(validated.Result);
        var dto = Assert.IsType<TicketValidationDto>(ok.Value);
        Assert.True(dto.IsValid);
        Assert.True(dto.CanRedeem);

        var redeemed = await controller.Redeem(plaintext, CancellationToken.None);
        var redeemedOk = Assert.IsType<OkObjectResult>(redeemed.Result);
        var redeemedDto = Assert.IsType<TicketValidationDto>(redeemedOk.Value);
        Assert.Equal(nameof(TicketRedemptionStatus.Redeemed), redeemedDto.Status);
        Assert.False(redeemedDto.CanRedeem);

        var second = await controller.Redeem(plaintext, CancellationToken.None);
        var conflict = Assert.IsType<ObjectResult>(second.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var error = Assert.IsType<TicketErrorDto>(conflict.Value);
        Assert.Equal(TicketErrorCodes.AlreadyRedeemed, error.Code);
    }

    [Fact]
    public async Task Redeem_ExpiredTicket_Returns400()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        var plaintext = "TKT-EXPIRED12AB";
        SeedTicket(db, tenantId, plaintext, TicketRedemptionStatus.Valid, DateTime.UtcNow.AddDays(-1));
        await db.SaveChangesAsync();

        var controller = CreateController(db, "cashier-1");
        var result = await controller.Redeem(plaintext, CancellationToken.None);
        var bad = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var error = Assert.IsType<TicketErrorDto>(bad.Value);
        Assert.Equal(TicketErrorCodes.Expired, error.Code);
    }

    [Fact]
    public async Task Validate_CrossTenant_Returns404()
    {
        var ownerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        await using var db = await CreateDbAsync(ownerTenant);
        SeedTenant(db, ownerTenant);
        SeedTenant(db, otherTenant);
        var plaintext = "TKT-CROSSTENANT1";
        SeedTicket(db, ownerTenant, plaintext, TicketRedemptionStatus.Valid, DateTime.UtcNow.AddDays(10));
        await db.SaveChangesAsync();

        var controller = CreateController(db, "cashier-1", otherTenant);
        var result = await controller.Validate(plaintext, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Controller_RequiresCartView()
    {
        var permission = Assert.Single(
            typeof(PosTicketsController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.CartView, permission.Permission);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Tickets_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PosTicketsController CreateController(
        AppDbContext db,
        string actorId,
        Guid? tenantId = null)
    {
        var accessor = Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("Tenant accessor was not registered for the test context."));
        if (accessor is TenantTestDoubles.MutableTenantAccessor mutable)
            mutable.TenantId = tenantId ?? mutable.TenantId;

        var controller = new PosTicketsController(new TicketRedemptionService(db), accessor, new PermissiveVerticalProfileGuard())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, actorId),
                            new Claim(ClaimTypes.Role, Roles.Cashier),
                        ],
                        authenticationType: "test")),
                },
            },
        };
        return controller;
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

    private static void SeedTicket(
        AppDbContext db,
        Guid tenantId,
        string plaintext,
        TicketRedemptionStatus status,
        DateTime validUntilUtc)
    {
        var hash = TicketCodeHasher.HashRaw(plaintext);
        db.TicketRedemptions.Add(new TicketRedemption
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketCode = TicketCodeHasher.DisplayPrefix(hash),
            TicketCodeHash = hash,
            Status = status,
            ValidFromUtc = DateTime.UtcNow.AddDays(-1),
            ValidUntilUtc = validUntilUtc,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}

using System.Runtime.CompilerServices;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TicketPriceCategoryServiceTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task CreateUpdateDeactivate_RoundTrip_WritesPriceCategoryAudit()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var audit = TicketAuditTestDoubles.Loose();
        var events = CreateEvents(db, audit);

        var created = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.CreateCategoryAsync(
                tenantId,
                new UpsertTicketPriceCategoryRequest { Code = "VIP", Name = "VIP", SortOrder = 1 },
                "manager-1",
                CancellationToken.None));
        Assert.Equal("vip", created.Category.Code);
        Assert.True(created.Category.IsActive);

        var updated = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.UpdateCategoryAsync(
                created.Category.Id,
                new UpsertTicketPriceCategoryRequest { Code = "vip", Name = "VIP Parkett", SortOrder = 2, IsActive = true },
                "manager-1",
                CancellationToken.None));
        Assert.Equal("VIP Parkett", updated.Category.Name);

        audit.Verify(a => a.LogSystemOperationAsync(
            "TICKET_PRICE_CATEGORY_CHANGED",
            "TicketPriceCategory",
            "manager-1",
            "Manager",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.TicketPriceCategoryChanged,
            created.Category.Id,
            tenantId,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Exactly(2));
        Assert.Equal(146, (int)AuditEventType.TicketPriceCategoryChanged);

        var deactivated = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.DeactivateCategoryAsync(created.Category.Id, "manager-1", CancellationToken.None));
        Assert.False(deactivated.Category.IsActive);
        Assert.False((await db.TicketPriceCategories.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Create_DuplicateCode_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var controller = CreateController(db, tenantId);
        var body = new UpsertTicketPriceCategoryRequest { Code = "standard", Name = "Standard" };
        var first = await controller.Create(body, CancellationToken.None);
        Assert.IsType<CreatedResult>(first);

        var second = await controller.Create(body, CancellationToken.None);
        var conflict = Assert.IsType<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var error = Assert.IsType<TicketErrorDto>(conflict.Value);
        Assert.Equal(TicketSeatErrorCodes.DuplicateCategoryCode, error.Code);
    }

    [Fact]
    public async Task Get_CrossTenant_Returns404()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = await CreateDbAsync(owner);
        SeedTenant(db, owner);
        SeedTenant(db, other);
        await db.SaveChangesAsync();
        var events = CreateEvents(db);
        var created = Assert.IsType<TicketCatalogResult.CategoryOk>(
            await events.CreateCategoryAsync(
                owner,
                new UpsertTicketPriceCategoryRequest { Code = "student", Name = "Student" },
                "manager-1",
                CancellationToken.None));

        var controller = CreateController(db, other);
        var result = await controller.Get(created.Category.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static TicketEventService CreateEvents(AppDbContext db, Mock<IAuditLogService>? audit = null) =>
        new(db, (audit ?? TicketAuditTestDoubles.Loose()).Object, NullLogger<TicketEventService>.Instance);

    private static async Task<AppDbContext> CreateDbAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TicketCategories_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static AdminTicketPriceCategoriesController CreateController(AppDbContext db, Guid tenantId)
    {
        var accessor = Accessors.GetValue(db, _ => throw new InvalidOperationException("missing accessor"));
        if (accessor is TenantTestDoubles.MutableTenantAccessor mutable)
            mutable.TenantId = tenantId;
        return new AdminTicketPriceCategoriesController(CreateEvents(db), accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "manager-1"), new Claim(ClaimTypes.Role, Roles.Manager)],
                        "test")),
                },
            },
        };
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
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

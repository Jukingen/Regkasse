using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosStaffControllerTests
{
    [Fact]
    public async Task List_ReturnsCashierWaiterManager_ForAmbientTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsureTenant(db, tenantId);

        db.Users.AddRange(
            User("cashier-1", "Anna", "Kasse", Roles.Cashier),
            User("kitchen-1", "Dana", "Küche", Roles.Kitchen));
        db.UserTenantMemberships.AddRange(
            Membership("cashier-1", tenantId),
            Membership("kitchen-1", tenantId));
        await db.SaveChangesAsync();

        var result = await new PosStaffController(db).List(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<PosStaffMemberDto>>(ok.Value);

        var member = Assert.Single(members);
        Assert.Equal("cashier-1", member.Id);
        Assert.Equal("Anna Kasse", member.Name);
        Assert.Equal(Roles.Cashier, member.Role);

        var permission = Assert.Single(
            typeof(PosStaffController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.CartView, permission.Permission);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosStaff_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static ApplicationUser User(string id, string first, string last, string role) =>
        new()
        {
            Id = id,
            UserName = id,
            Email = $"{id}@test.local",
            FirstName = first,
            LastName = last,
            Role = role,
            IsActive = true,
        };

    private static UserTenantMembership Membership(string userId, Guid tenantId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
}

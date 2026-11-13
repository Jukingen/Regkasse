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

public sealed class AdminStaffControllerTests
{
    [Fact]
    public async Task List_ReturnsCashierWaiterManager_ForAmbientTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        TenantTestDoubles.EnsureTenant(db, tenantId);

        db.Users.AddRange(
            User("cashier-1", "Anna", "Kasse", Roles.Cashier),
            User("waiter-1", "Ben", "Service", Roles.Waiter),
            User("manager-1", "Carla", "Leitung", Roles.Manager),
            User("kitchen-1", "Dana", "Küche", Roles.Kitchen),
            User("inactive-1", "Eva", "Alt", Roles.Cashier, isActive: false));
        db.UserTenantMemberships.AddRange(
            Membership("cashier-1", tenantId),
            Membership("waiter-1", tenantId),
            Membership("manager-1", tenantId),
            Membership("kitchen-1", tenantId),
            Membership("inactive-1", tenantId));
        await db.SaveChangesAsync();

        var result = await new AdminStaffController(db).List(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<AdminStaffMemberDto>>(ok.Value);

        Assert.Equal(3, members.Count);
        Assert.Equal(
            ["Anna Kasse", "Carla Leitung", "Ben Service"],
            members.Select(m => m.Name).ToArray());
        Assert.DoesNotContain(members, m => m.Id == "kitchen-1");
        Assert.DoesNotContain(members, m => m.Id == "inactive-1");

        var permission = Assert.Single(
            typeof(AdminStaffController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.ProductView, permission.Permission);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminStaff_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static ApplicationUser User(
        string id,
        string first,
        string last,
        string role,
        bool isActive = true) =>
        new()
        {
            Id = id,
            UserName = id,
            Email = $"{id}@test.local",
            FirstName = first,
            LastName = last,
            Role = role,
            IsActive = isActive,
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

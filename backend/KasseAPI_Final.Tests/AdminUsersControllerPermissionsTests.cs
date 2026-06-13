using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminUsersControllerPermissionsTests
{
    [Fact]
    public async Task GetUserPermissions_Self_ReturnsEffectivePermissionsSorted()
    {
        var userId = Guid.NewGuid().ToString("D");
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = "cashier1",
            Email = "cashier1@test.local",
            IsActive = true,
            Role = Roles.Cashier,
        });

        var resolver = new Mock<IEffectivePermissionResolver>();
        resolver
            .Setup(r => r.GetEffectivePermissionsAsync(
                userId,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AppPermissions.ReportView,
                AppPermissions.ProductView,
            });

        var controller = AdminUsersControllerPermissionsTestsSupport.CreateController(
            db,
            userManager,
            actorId: userId,
            actorRole: Roles.Cashier);

        var result = await controller.GetUserPermissions(userId, resolver.Object, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var permissions = Assert.IsType<List<string>>(ok.Value);
        Assert.Equal(new[] { AppPermissions.ProductView, AppPermissions.ReportView }, permissions);
    }

    [Fact]
    public async Task GetUserPermissions_OtherUser_NonSuperAdmin_ReturnsForbid()
    {
        var actorId = Guid.NewGuid().ToString("D");
        var otherId = Guid.NewGuid().ToString("D");
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        await userManager.CreateAsync(new ApplicationUser
        {
            Id = otherId,
            UserName = "other",
            Email = "other@test.local",
            IsActive = true,
            Role = Roles.Manager,
        });

        var resolver = new Mock<IEffectivePermissionResolver>();
        var controller = AdminUsersControllerPermissionsTestsSupport.CreateController(
            db,
            userManager,
            actorId: actorId,
            actorRole: Roles.Manager);

        var result = await controller.GetUserPermissions(otherId, resolver.Object, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        resolver.Verify(
            r => r.GetEffectivePermissionsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetUserPermissions_SuperAdmin_CanReadOtherUserPermissions()
    {
        var otherId = Guid.NewGuid().ToString("D");
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        await userManager.CreateAsync(new ApplicationUser
        {
            Id = otherId,
            UserName = "manager1",
            Email = "manager1@test.local",
            IsActive = true,
            Role = Roles.Manager,
        });

        var resolver = new Mock<IEffectivePermissionResolver>();
        resolver
            .Setup(r => r.GetEffectivePermissionsAsync(
                otherId,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AppPermissions.UserView });

        var controller = AdminUsersControllerPermissionsTestsSupport.CreateController(
            db,
            userManager,
            actorId: Guid.NewGuid().ToString("D"),
            actorRole: Roles.SuperAdmin);

        var result = await controller.GetUserPermissions(otherId, resolver.Object, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var permissions = Assert.IsType<List<string>>(ok.Value);
        Assert.Contains(AppPermissions.UserView, permissions);
    }

    [Fact]
    public async Task GetUserPermissions_UserNotFound_Returns404()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        var missingId = Guid.NewGuid().ToString("D");
        var resolver = new Mock<IEffectivePermissionResolver>();

        var controller = AdminUsersControllerPermissionsTestsSupport.CreateController(
            db,
            userManager,
            actorId: missingId,
            actorRole: Roles.Cashier);

        var result = await controller.GetUserPermissions(missingId, resolver.Object, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminUsersPerm_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }
}

file static class AdminUsersControllerPermissionsTestsSupport
{
    internal static AdminUsersController CreateController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        string actorId,
        string actorRole)
    {
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(context),
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);

        var controller = new AdminUsersController(
            context,
            userManager,
            roleManager,
            Mock.Of<IAuditLogService>(),
            Mock.Of<IUserSessionInvalidation>(),
            Mock.Of<IUserUniquenessValidationService>(),
            Mock.Of<IUserCreationService>(),
            Mock.Of<ILogger<AdminUsersController>>(),
            TenantTestDoubles.NoOpProvisioner(),
            Mock.Of<ITenantUserService>(),
            NullCurrentTenantAccessor.Instance,
            Mock.Of<IUsernameChangeEmailService>(),
            Mock.Of<IUserUsernameHistoryService>(),
            ActivityEventTestSupport.CreateRecorder());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorId),
                        new Claim(ClaimTypes.Role, actorRole),
                    },
                    "Test")),
            },
        };

        return controller;
    }
}

using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Unit tests for role management: permissions catalog, roles with permissions, set permissions (custom only), delete role (custom only).
/// </summary>
public class RoleManagementTests
{
    [Fact]
    public void GetPermissionsCatalog_ReturnsAllKeysWithGroupResourceAction()
    {
        var roleMgmt = new Mock<IRoleManagementService>();
        var items = PermissionCatalogMetadata.GetAll();
        roleMgmt.Setup(x => x.GetPermissionsCatalog()).Returns(items);

        var catalog = roleMgmt.Object.GetPermissionsCatalog();
        Assert.NotEmpty(catalog);
        Assert.Contains(catalog, c => c.Key == AppPermissions.UserView && c.Resource == "user" && c.Action == "view");
        Assert.Contains(catalog, c => c.Key == AppPermissions.ProductView && c.Group == "Product");
    }

    [Fact]
    public void PermissionCatalogMetadata_IsValidPermissionKey_AcceptsCatalogKeys()
    {
        Assert.True(PermissionCatalogMetadata.IsValidPermissionKey(AppPermissions.UserView));
        Assert.True(PermissionCatalogMetadata.IsValidPermissionKey(AppPermissions.RoleManage));
        Assert.False(PermissionCatalogMetadata.IsValidPermissionKey("invalid.key"));
        Assert.False(PermissionCatalogMetadata.IsValidPermissionKey(""));
        Assert.False(PermissionCatalogMetadata.IsValidPermissionKey(null!));
    }

    [Fact]
    public async Task SetRolePermissions_WhenNotSuperAdmin_Returns403()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.Manager) }, "Test")) }
        };

        var result = await controller.SetRolePermissions("CustomRole", new UpdateRolePermissionsRequest { Permissions = new List<string> { AppPermissions.ProductView } }, CancellationToken.None);

        var status = result as ObjectResult;
        Assert.NotNull(status);
        Assert.Equal(403, status.StatusCode);
        roleMgmt.Verify(x => x.SetRolePermissionsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteRole_WhenNotSuperAdmin_Returns403()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.Manager) }, "Test")) }
        };

        var result = await controller.DeleteRole("CustomRole", CancellationToken.None);

        var status = result as ObjectResult;
        Assert.NotNull(status);
        Assert.Equal(403, status.StatusCode);
        roleMgmt.Verify(x => x.DeleteRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetRolePermissions_WhenSuperAdmin_AndServiceReturnsSuccess_Returns200()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.SetRolePermissionsAsync("CustomRole", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(SetRolePermissionsResult.Success);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.SetRolePermissions("CustomRole", new UpdateRolePermissionsRequest { Permissions = new List<string> { AppPermissions.ProductView } }, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.NotNull(ok);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task DeleteRole_WhenSuperAdmin_AndServiceReturnsRoleHasUsers_Returns409_WithMessageAndUserCount()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.DeleteRoleAsync("CustomRole", It.IsAny<CancellationToken>())).ReturnsAsync(DeleteRoleResult.RoleHasAssignedUsers);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.DeleteRole("CustomRole", CancellationToken.None);

        var status = result as ObjectResult;
        Assert.NotNull(status);
        Assert.Equal(409, status.StatusCode);
        Assert.NotNull(status.Value);
        var valueType = status.Value.GetType();
        Assert.Equal("ROLE_HAS_ASSIGNED_USERS", valueType.GetProperty("code")?.GetValue(status.Value) as string);
        Assert.NotNull(valueType.GetProperty("userCount"));
        Assert.Contains("Reassign", (valueType.GetProperty("message")?.GetValue(status.Value) as string) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteRole_WhenSuperAdmin_AndServiceReturnsSystemRoleNotDeletable_Returns400()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.DeleteRoleAsync(Roles.Manager, It.IsAny<CancellationToken>())).ReturnsAsync(DeleteRoleResult.SystemRoleNotDeletable);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.DeleteRole(Roles.Manager, CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task DeleteRole_WhenSuperAdmin_AndServiceReturnsRoleNotFound_Returns404()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.DeleteRoleAsync("NonExistent", It.IsAny<CancellationToken>())).ReturnsAsync(DeleteRoleResult.RoleNotFound);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.DeleteRole("NonExistent", CancellationToken.None);

        var notFound = result as NotFoundObjectResult;
        Assert.NotNull(notFound);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task SetRolePermissions_WhenSuperAdmin_AndServiceReturnsRoleNotFound_Returns404()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.SetRolePermissionsAsync("NonExistent", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(SetRolePermissionsResult.RoleNotFound);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.SetRolePermissions("NonExistent", new UpdateRolePermissionsRequest { Permissions = new List<string> { AppPermissions.ProductView } }, CancellationToken.None);

        var notFound = result as NotFoundObjectResult;
        Assert.NotNull(notFound);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task SetRolePermissions_WhenSuperAdmin_AndServiceReturnsInvalidPermissionKeys_Returns400()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.SetRolePermissionsAsync("CustomRole", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(SetRolePermissionsResult.InvalidPermissionKeys);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.SetRolePermissions("CustomRole", new UpdateRolePermissionsRequest { Permissions = new List<string> { "invalid.key" } }, CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task SetRolePermissions_WhenSuperAdmin_AndServiceReturnsSystemRoleNotEditable_Returns400()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        // SuperAdmin role name remains non-editable at service layer (matrix-only).
        roleMgmt.Setup(x => x.SetRolePermissionsAsync(Roles.SuperAdmin, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(SetRolePermissionsResult.SystemRoleNotEditable);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.SetRolePermissions(Roles.SuperAdmin, new UpdateRolePermissionsRequest { Permissions = new List<string> { AppPermissions.ProductView } }, CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WhenNameIsSystemRole_Returns400WithROLE_NAME_RESERVED()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>().Object;
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.CreateRole(new CreateRoleRequest { Name = "Admin" }); // ReservedRoleNames

        var badRequest = result as BadRequestObjectResult;
        Assert.NotNull(badRequest);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.NotNull(badRequest.Value);
        var valueType = badRequest.Value.GetType();
        var codeProp = valueType.GetProperty("code");
        Assert.NotNull(codeProp);
        Assert.Equal("ROLE_NAME_RESERVED", codeProp.GetValue(badRequest.Value) as string);
    }

    [Fact]
    public async Task SetRolePermissions_WhenSuperAdmin_AndEmptyPermissionSet_Returns200()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.SetRolePermissionsAsync("CustomRole", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(SetRolePermissionsResult.Success);
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "a1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin) }, "Test")) }
        };

        var result = await controller.SetRolePermissions("CustomRole", new UpdateRolePermissionsRequest { Permissions = new List<string>() }, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.NotNull(ok);
        Assert.Equal(200, ok.StatusCode);
        roleMgmt.Verify(x => x.SetRolePermissionsAsync("CustomRole", It.Is<IReadOnlyList<string>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPermissionsCatalog_ReturnsOkWithItems()
    {
        var (context, userManager, roleManager) = await CreateInMemorySetupAsync();
        var roleMgmt = new Mock<IRoleManagementService>();
        roleMgmt.Setup(x => x.GetPermissionsCatalog()).Returns(PermissionCatalogMetadata.GetAll());
        var audit = new Mock<IAuditLogService>().Object;
        var session = new Mock<IUserSessionInvalidation>().Object;
        var uniqueness = new Mock<IUserUniquenessValidationService>().Object;
        var logger = new Mock<ILogger<UserManagementController>>().Object;
        var controller = new UserManagementController(context, userManager, roleManager, audit, session, uniqueness, roleMgmt.Object, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "a1"),
                    new Claim(ClaimTypes.Role, Roles.Manager),
                    new Claim(KasseAPI_Final.Authorization.PermissionCatalog.PermissionClaimType, AppPermissions.UserView),
                }, "Test"))
            }
        };

        var result = controller.GetPermissionsCatalog();

        var ok = result.Result as OkObjectResult;
        Assert.NotNull(ok);
        var list = (ok!.Value as IEnumerable<PermissionCatalogItemDto>)!.ToList();
        Assert.NotNull(list);
        Assert.NotEmpty(list);
    }

    private static async Task<(AppDbContext, UserManager<ApplicationUser>, RoleManager<IdentityRole>)> CreateInMemorySetupAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, IdentityRole, AppDbContext>(context, null);
        var userManager = new UserManager<ApplicationUser>(store, null!, null!, null!, null!, null!, null!, null!, null!);
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(context);
        var roleManager = new RoleManager<IdentityRole>(roleStore, null!, null!, null!, null!);
        return (context, userManager, roleManager);
    }
}

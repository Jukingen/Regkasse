using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// HTTP integration tests for PUT /api/admin/tenants/{tenantId}/users/{userId}/role
/// including preservePreviousPermissions.
/// </summary>
public sealed class AdminTenantUsersRoleIntegrationTests : IClassFixture<AdminUsersCrossTenantWebApplicationFactory>
{
    private readonly AdminUsersCrossTenantWebApplicationFactory _factory;

    public AdminTenantUsersRoleIntegrationTests(AdminUsersCrossTenantWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task UpdateRole_PreserveFalse_Returns200_AndUpdatesRole()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserAId}/role",
            new { role = Roles.Manager, preservePreviousPermissions = false });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 but got {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        Assert.Equal(Roles.Manager, json.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task UpdateRole_PreserveTrue_Returns200_AndCreatesGrantOverrides()
    {
        var client = await CreateAuthenticatedClientAsync();

        var promote = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserAId}/role",
            new { role = Roles.Manager, preservePreviousPermissions = false });
        promote.EnsureSuccessStatusCode();

        var demote = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserAId}/role",
            new { role = Roles.Cashier, preservePreviousPermissions = true });
        demote.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == AdminUsersCrossTenantWebApplicationFactory.UserAId && o.IsGranted)
            .Select(o => o.Permission)
            .ToListAsync();

        Assert.Contains(AppPermissions.AuditExport, overrides);
    }

    [Fact]
    public async Task UpdateRole_CrossTenantUser_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/role",
            new { role = Roles.Manager, preservePreviousPermissions = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_SuperAdminPreviousRole_PreserveTrue_DoesNotCreateOverrides()
    {
        var client = await CreateAuthenticatedClientAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingOverrides = await db.UserPermissionOverrides
                .Where(o => o.UserId == AdminUsersCrossTenantWebApplicationFactory.UserAId)
                .ToListAsync();
            db.UserPermissionOverrides.RemoveRange(existingOverrides);
            await db.SaveChangesAsync();

            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(AdminUsersCrossTenantWebApplicationFactory.UserAId);
            Assert.NotNull(user);
            var previousRoles = await userManager.GetRolesAsync(user!);
            if (previousRoles.Count > 0)
                await userManager.RemoveFromRolesAsync(user!, previousRoles);
            await userManager.AddToRoleAsync(user!, Roles.SuperAdmin);
            user!.Role = Roles.SuperAdmin;
            await userManager.UpdateAsync(user);
        }

        var response = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserAId}/role",
            new { role = Roles.Manager, preservePreviousPermissions = true });
        response.EnsureSuccessStatusCode();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var overrides = await verifyDb.UserPermissionOverrides
            .Where(o => o.UserId == AdminUsersCrossTenantWebApplicationFactory.UserAId)
            .ToListAsync();
        Assert.Empty(overrides);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier = AdminUsersCrossTenantWebApplicationFactory.AdminAEmail,
            password = AdminUsersCrossTenantWebApplicationFactory.AdminAPassword,
            clientApp = "admin",
        });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response missing token.");
    }
}

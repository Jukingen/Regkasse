using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// HTTP integration tests: AdminUsers mutation endpoints reject cross-tenant targets with 404.
/// </summary>
public sealed class AdminUsersCrossTenantIntegrationTests : IClassFixture<AdminUsersCrossTenantWebApplicationFactory>
{
    private readonly AdminUsersCrossTenantWebApplicationFactory _factory;

    public AdminUsersCrossTenantIntegrationTests(AdminUsersCrossTenantWebApplicationFactory factory) =>
        _factory = factory;

    private async Task<HttpClient> CreateAuthenticatedTenantAClientAsync()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var token = await LoginAsTenantAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedManagerTenantAClientAsync()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var token = await LoginAsync(client, AdminUsersCrossTenantWebApplicationFactory.ManagerAEmail, AdminUsersCrossTenantWebApplicationFactory.ManagerAPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsync(HttpClient client, string loginIdentifier, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier,
            password,
            clientApp = "admin",
        });

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response missing token.");
    }

    private static async Task<string> LoginAsTenantAdminAsync(HttpClient client) =>
        await LoginAsync(client, AdminUsersCrossTenantWebApplicationFactory.AdminAEmail, AdminUsersCrossTenantWebApplicationFactory.AdminAPassword);

    private static async Task AssertNotFoundNotForbidden(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 404 Not Found but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_AsTenantAdmin_ReturnsToken()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier = AdminUsersCrossTenantWebApplicationFactory.AdminAEmail,
            password = AdminUsersCrossTenantWebApplicationFactory.AdminAPassword,
            clientApp = "admin",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Login failed: {(int)response.StatusCode} {body}");
        Assert.Contains("token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PatchUser_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PatchAsJsonAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}",
            new { firstName = "Hacked" });

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task UpdateUsername_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PatchAsJsonAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/username",
            new { newUsername = "hacked-name", reason = "cross-tenant attempt" });

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task DeactivateUser_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/deactivate",
            new { reason = "Cross-tenant attempt" });

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task ReactivateUser_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/reactivate",
            new { reason = "Cross-tenant attempt" });

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task ForcePasswordReset_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/force-password-reset",
            new { newPassword = "NewPass123!" });

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task GenerateTemporaryPassword_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PostAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/generate-temporary-password",
            null);

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task ListTenantUsers_AsManager_ReturnsOnlyOwnTenantUsers()
    {
        var client = await CreateAuthenticatedManagerTenantAClientAsync();

        var response = await client.GetAsync(
            $"/api/admin/users?type=tenant&tenantId={AdminUsersCrossTenantWebApplicationFactory.TenantBId}");

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToList();
        Assert.All(rows, row => Assert.Equal(AdminUsersCrossTenantWebApplicationFactory.TenantAId.ToString(), row.GetProperty("tenantId").GetString()));
        Assert.Contains(rows, row => row.GetProperty("userId").GetString() == AdminUsersCrossTenantWebApplicationFactory.UserAId);
        Assert.DoesNotContain(rows, row => row.GetProperty("userId").GetString() == AdminUsersCrossTenantWebApplicationFactory.UserBId);
    }

    [Fact]
    public async Task ListUserManagement_AsManager_ReturnsOnlyOwnTenantUsers()
    {
        var client = await CreateAuthenticatedManagerTenantAClientAsync();

        var response = await client.GetAsync("/api/UserManagement?page=1&pageSize=100");

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, item => item.GetProperty("id").GetString() == AdminUsersCrossTenantWebApplicationFactory.UserAId);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetString() == AdminUsersCrossTenantWebApplicationFactory.UserBId);
    }

    [Fact]
    public async Task GetUser_FromDifferentTenant_Returns404()
    {
        var client = await CreateAuthenticatedManagerTenantAClientAsync();

        var response = await client.GetAsync(
            $"/api/UserManagement/{AdminUsersCrossTenantWebApplicationFactory.UserBId}");

        await AssertNotFoundNotForbidden(response);
    }

    [Fact]
    public async Task ListTenantUsersRoute_AsManager_ForOtherTenant_Returns403WithoutUserManage()
    {
        var client = await CreateAuthenticatedManagerTenantAClientAsync();

        var response = await client.GetAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantBId}/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchUser_InSameTenant_StillSucceeds()
    {
        var client = await CreateAuthenticatedTenantAClientAsync();

        var response = await client.PatchAsJsonAsync(
            $"/api/admin/users/{AdminUsersCrossTenantWebApplicationFactory.UserAId}",
            new { firstName = "Updated" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("OpenApiExportWebHost")]
public sealed class AdminTenantUsersRoleIntegrationTests : IClassFixture<AdminUsersCrossTenantWebApplicationFactory>
{
    private readonly AdminUsersCrossTenantWebApplicationFactory _factory;

    public AdminTenantUsersRoleIntegrationTests(AdminUsersCrossTenantWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task UpdateRole_Returns200_AndUpdatesRole()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserAId}/role",
            new { role = Roles.Manager });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 but got {(int)response.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        Assert.Equal(Roles.Manager, json.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task UpdateRole_CrossTenantUser_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/admin/tenants/{AdminUsersCrossTenantWebApplicationFactory.TenantAId}/users/{AdminUsersCrossTenantWebApplicationFactory.UserBId}/role",
            new { role = Roles.Manager });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

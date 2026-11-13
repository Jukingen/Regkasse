using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KasseAPI_Final.Tests.Integration;

/// <summary>
/// HTTP integration coverage for <c>POST /api/Auth/login</c> (valid + invalid credentials).
/// </summary>
[Collection("OpenApiExportWebHost")]
public sealed class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            loginIdentifier = TestWebApplicationFactory.AdminEmail,
            password = TestWebApplicationFactory.AdminPassword,
            clientApp = "admin",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(json.RootElement.TryGetProperty("refreshToken", out var refresh));
        Assert.False(string.IsNullOrWhiteSpace(refresh.GetString()));
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            loginIdentifier = "invalid@test.com",
            password = "wrongpassword",
            clientApp = "admin",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_CREDENTIALS", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized_WithSameGenericCode()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            loginIdentifier = TestWebApplicationFactory.AdminEmail,
            password = "DefinitelyWrongPass1!",
            clientApp = "admin",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_CREDENTIALS", json.RootElement.GetProperty("code").GetString());
    }
}

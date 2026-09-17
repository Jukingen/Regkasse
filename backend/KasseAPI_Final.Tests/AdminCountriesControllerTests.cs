using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminCountriesControllerTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Controller_RequiresSuperAdminRole()
    {
        var attr = typeof(AdminCountriesController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Roles.SuperAdmin, attr.Roles);
    }

    [Fact]
    public void List_ReturnsAtDeCh_ExcludesEuDefault()
    {
        var controller = new AdminCountriesController(Registry);

        var result = controller.List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<CountryProfileSummaryDto>>(ok.Value);
        Assert.Equal(["AT", "DE", "CH"], items.Select(i => i.Code).ToArray());
        Assert.DoesNotContain(items, i => i.Code == CountryProfileCodes.EuDefault);
    }

    [Fact]
    public void List_AllowedVatRegimes_MatchRegistry()
    {
        var controller = new AdminCountriesController(Registry);

        var result = controller.List();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<CountryProfileSummaryDto>>(ok.Value);

        foreach (var item in items)
        {
            var seed = Registry.Get(item.Code);
            Assert.Equal(seed.AllowedVatRegimes, item.AllowedVatRegimes);
            Assert.Equal(seed.FiscalSystem, item.FiscalSystem);
            Assert.Equal(seed.EInvoicingStandards, item.EInvoicingStandards);
            Assert.Equal(seed.Currency, item.Currency);
            Assert.Equal(seed.DefaultLocale, item.DefaultLocale);
        }
    }

    [Fact]
    public void List_DoesNotExposeRegistryOnlyFields()
    {
        var controller = new AdminCountriesController(Registry);
        var result = controller.List();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(ok.Value, JsonOptions);

        Assert.DoesNotContain("vatIdPattern", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isTenantSelectable", json, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultTimeZone", json, StringComparison.Ordinal);
        Assert.Contains("eInvoicingStandards", json, StringComparison.Ordinal);
        Assert.DoesNotContain("eInvoicingStandard\"", json.Replace("eInvoicingStandards", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }
}

[Collection("OpenApiExportWebHost")]
public sealed class AdminCountriesControllerAuthorizationTests : IClassFixture<AdminUsersCrossTenantWebApplicationFactory>
{
    private readonly AdminUsersCrossTenantWebApplicationFactory _factory;

    public AdminCountriesControllerAuthorizationTests(AdminUsersCrossTenantWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var response = await client.GetAsync("/api/admin/countries");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Manager_Returns403()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var token = await LoginAsync(
            client,
            AdminUsersCrossTenantWebApplicationFactory.ManagerAEmail,
            AdminUsersCrossTenantWebApplicationFactory.ManagerAPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/countries");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_SuperAdmin_ReturnsAtDeCh()
    {
        var client = _factory.CreateTenantClient(AdminUsersCrossTenantWebApplicationFactory.TenantASlug);
        var token = await LoginAsync(
            client,
            AdminUsersCrossTenantWebApplicationFactory.AdminAEmail,
            AdminUsersCrossTenantWebApplicationFactory.AdminAPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/countries");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 but got {(int)response.StatusCode}. Body: {body}");

        using var json = JsonDocument.Parse(body);
        var codes = json.RootElement.EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToArray();
        Assert.Equal(["AT", "DE", "CH"], codes);
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
}

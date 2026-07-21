using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// HTTP integration tests: Manager admin JWT can read oversight APIs but cannot take POS payments.
/// </summary>
[Collection("OpenApiExportWebHost")]
public sealed class ManagerOversightApiTests : IClassFixture<ManagerOversightWebApplicationFactory>
{
    private readonly ManagerOversightWebApplicationFactory _factory;

    public ManagerOversightApiTests(ManagerOversightWebApplicationFactory factory) =>
        _factory = factory;

    private async Task<HttpClient> CreateAuthenticatedManagerClientAsync()
    {
        var client = _factory.CreateTenantClient(ManagerOversightWebApplicationFactory.TenantASlug);
        var token = await LoginAsManagerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsManagerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier = ManagerOversightWebApplicationFactory.ManagerEmail,
            password = ManagerOversightWebApplicationFactory.ManagerPassword,
            clientApp = "admin",
        });

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response missing token.");
    }

    private static async Task<JsonElement> LoginAsManagerWithPayloadAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier = ManagerOversightWebApplicationFactory.ManagerEmail,
            password = ManagerOversightWebApplicationFactory.ManagerPassword,
            clientApp = "admin",
        });

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task Manager_AdminLogin_IncludesAllOversightViewPermissions()
    {
        var client = _factory.CreateTenantClient(ManagerOversightWebApplicationFactory.TenantASlug);
        var payload = await LoginAsManagerWithPayloadAsync(client);

        var permissions = payload.GetProperty("user").GetProperty("permissions")
            .EnumerateArray()
            .Select(p => p.GetString())
            .Where(p => p is not null)
            .Select(p => p!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var oversight in AdminAppPermissionProfile.ManagerOversightViewPermissions)
        {
            var present = permissions.Contains(oversight)
                || (string.Equals(oversight, AppPermissions.UserResetPassword, StringComparison.OrdinalIgnoreCase)
                    && permissions.Contains(AppPermissions.UserManage))
                || (string.Equals(oversight, AppPermissions.CashRegisterView, StringComparison.OrdinalIgnoreCase)
                    && (permissions.Contains(AppPermissions.CashRegisterManage)
                        || permissions.Contains(AppPermissions.CashRegisterDecommission)));
            Assert.True(
                present,
                $"Manager admin login should include oversight permission '{oversight}' (or an implying manage permission).");
        }
    }

    [Fact]
    public async Task Manager_GetAdminPaymentDetail_OtherTenant_Returns404()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync(
            $"/api/admin/payments/{ManagerOversightWebApplicationFactory.PaymentBId}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("not found", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_AdminLogin_IncludesPaymentView_ExcludesPaymentTake()
    {
        var client = _factory.CreateTenantClient(ManagerOversightWebApplicationFactory.TenantASlug);
        var payload = await LoginAsManagerWithPayloadAsync(client);

        var permissions = payload.GetProperty("user").GetProperty("permissions")
            .EnumerateArray()
            .Select(p => p.GetString())
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        Assert.Contains(AppPermissions.PaymentView, permissions, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(AppPermissions.PaymentTake, permissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AppPermissions.SaleView, permissions, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AppPermissions.ReportExport, permissions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_GetSignatureDebug_OwnTenant_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync(
            $"/api/pos/payment/{ManagerOversightWebApplicationFactory.PaymentAId}/signature-debug");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        Assert.Contains("steps", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_GetAdminPayments_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync("/api/admin/payments?page=1&pageSize=10");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetReceiptsList_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync("/api/Receipts/list?page=1&pageSize=10");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetLegacyCashRegisterList_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync("/api/CashRegister");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetAdminCashRegistersList_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync("/api/admin/cash-registers?page=1&pageSize=20");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetAdminCashRegistersByTenant_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync("/api/admin/cash-registers/by-tenant");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetDepExport_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();
        var from = DateTime.UtcNow.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.ToString("O");

        var response = await client.GetAsync(
            $"/api/admin/rksv/dep-export?cashRegisterId={ManagerOversightWebApplicationFactory.CashRegisterAId}&fromUtc={Uri.EscapeDataString(from)}&toUtc={Uri.EscapeDataString(to)}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetPaymentById_OwnTenant_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync(
            $"/api/pos/payment/{ManagerOversightWebApplicationFactory.PaymentAId}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_PostVerifySignature_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.PostAsJsonAsync("/api/pos/payment/verify-signature", new
        {
            compactJws = "a.b.c",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_GetAdminPaymentDetail_OwnTenant_Returns200()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync(
            $"/api/admin/payments/{ManagerOversightWebApplicationFactory.PaymentAId}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 200 but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task Manager_PostPayment_Returns403()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.PostAsJsonAsync("/api/pos/payment", new
        {
            items = Array.Empty<object>(),
            payment = new { method = "cash", tseRequired = false },
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_GetPayment_OtherTenant_Returns404()
    {
        var client = await CreateAuthenticatedManagerClientAsync();

        var response = await client.GetAsync(
            $"/api/pos/payment/{ManagerOversightWebApplicationFactory.PaymentBId}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("not found", body, StringComparison.OrdinalIgnoreCase);
    }
}

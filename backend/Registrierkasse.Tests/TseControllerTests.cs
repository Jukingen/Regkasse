using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Registrierkasse_API;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using Xunit;

namespace Registrierkasse.Tests;

public class TseControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly AppDbContext _context;
    private readonly ITseService _tseService;

    public TseControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _tseService = scope.ServiceProvider.GetRequiredService<ITseService>();
    }

    [Fact]
    public async Task GetTseStatus_ReturnsOkResult()
    {
        // Arrange
        var token = await GetAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/tse/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<TseStatusResponse>(content);
        Assert.NotNull(status);
        Assert.True(status.IsConnected);
    }

    [Fact]
    public async Task GenerateDailyReport_ReturnsOkResult()
    {
        // Arrange
        var token = await GetAuthToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsync("/api/tse/daily-report", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var report = JsonSerializer.Deserialize<DailyReportResponse>(content);
        Assert.NotNull(report);
        Assert.NotNull(report.Signature);
        Assert.NotNull(report.ReportData);
    }

    private async Task<string> GetAuthToken()
    {
        var loginRequest = new
        {
            Email = "admin@example.com",
            Password = "Admin123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent);
        return tokenResponse.Token;
    }
}

// Mock TSE Service
public class MockTseService : ITseService
{
    public Task<bool> IsConnectedAsync() => Task.FromResult(true);
    public Task<string> SignDataAsync(string data) => Task.FromResult("MOCK_SIGNATURE");
    public Task<bool> VerifySignatureAsync(string data, string signature) => Task.FromResult(true);
    public Task<DailyReport> GenerateDailyReportAsync() => Task.FromResult(new DailyReport
    {
        Signature = "MOCK_SIGNATURE",
        ReportData = "MOCK_REPORT_DATA",
        GeneratedAt = DateTime.UtcNow
    });
}

// Response models
public class TseStatusResponse
{
    public bool IsConnected { get; set; }
    public string DeviceInfo { get; set; }
}

public class DailyReportResponse
{
    public string Signature { get; set; }
    public string ReportData { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; }
} 
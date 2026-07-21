using System.Net;
using System.Text.Json;
using KasseAPI_Final.Tests.Integration;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>HTTP probes for HealthController live/ready/deps endpoints.</summary>
[Collection("OpenApiExportWebHost")]
public sealed class HealthControllerEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthControllerEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_ReturnsPlainOk()
    {
        var response = await _client.GetAsync("/api/health/live");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected 2xx for /api/health/live, got {(int)response.StatusCode}: {body}");
        Assert.Equal("OK", body.Trim());
    }

    [Fact]
    public async Task HealthLiveAlias_ReturnsPlainOk()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Ready_ReturnsJsonWithDatabaseEntry()
    {
        var response = await _client.GetAsync("/api/health/ready");
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status {response.StatusCode}");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.True(doc.RootElement.TryGetProperty("status", out _));
        Assert.True(doc.RootElement.TryGetProperty("entries", out var entries));
        Assert.True(entries.TryGetProperty("database", out _));
        Assert.False(entries.TryGetProperty("tse", out _), "ready must not run TSE check");
        Assert.False(entries.TryGetProperty("ntp", out _), "ready must not run NTP check");
    }

    [Fact]
    public async Task Get_IncludesDatabaseTseAndNtp()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status {response.StatusCode}");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.True(doc.RootElement.TryGetProperty("entries", out var entries));
        Assert.True(entries.TryGetProperty("database", out _));
        Assert.True(entries.TryGetProperty("tse", out _));
        Assert.True(entries.TryGetProperty("ntp", out _));
    }

    [Fact]
    public async Task RootHealth_RemainsCheapLiveness()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", (await response.Content.ReadAsStringAsync()).Trim());
    }
}

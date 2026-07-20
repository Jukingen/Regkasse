using System.Text.Json;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services.Token;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TokenValidationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_allows_request_when_token_not_blacklisted()
    {
        var nextCalled = false;
        var blacklist = CreateBlacklist();
        var middleware = new TokenValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer valid-token";

        await middleware.InvokeAsync(context, blacklist);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_rejects_blacklisted_token_with_401()
    {
        var nextCalled = false;
        var blacklist = CreateBlacklist();
        blacklist.BlacklistToken("revoked-token", DateTime.UtcNow.AddHours(1));

        var middleware = new TokenValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = "Bearer revoked-token";

        await middleware.InvokeAsync(context, blacklist);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("TOKEN_REVOKED", doc.RootElement.GetProperty("code").GetString());
        Assert.Contains("revoked", doc.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_allows_requests_without_authorization_header()
    {
        var nextCalled = false;
        var middleware = new TokenValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext(), CreateBlacklist());

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("Bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("  Bearer   abc.def.ghi  ", "abc.def.ghi")]
    public void ExtractBearerToken_strips_scheme(string header, string expected)
    {
        Assert.Equal(expected, TokenValidationMiddleware.ExtractBearerToken(header));
    }

    private static TokenBlacklistService CreateBlacklist() =>
        new(new MemoryCache(new MemoryCacheOptions()), NullLogger<TokenBlacklistService>.Instance);
}

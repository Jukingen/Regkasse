using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class JwtAccessTokenIssuerValidationTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long!!";
    private const string Issuer = "KasseAPI-Test";
    private const string Audience = "KasseAPIUsers-Test";

    [Fact]
    public async Task ValidateTokenAsync_returns_true_for_issued_token()
    {
        var issuer = CreateIssuer();
        var token = issuer.IssueToken(
            new[] { new Claim("sub", "user-1"), new Claim("role", "Cashier") },
            jti: Guid.NewGuid().ToString("N"),
            sessionId: Guid.NewGuid(),
            expiresAtUtc: DateTime.UtcNow.AddHours(1));

        Assert.True(await issuer.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task ValidateTokenAsync_returns_false_for_empty_token()
    {
        var issuer = CreateIssuer();
        Assert.False(await issuer.ValidateTokenAsync(""));
        Assert.False(await issuer.ValidateTokenAsync("   "));
    }

    [Fact]
    public async Task ValidateTokenAsync_returns_false_for_tampered_token()
    {
        var issuer = CreateIssuer();
        var token = issuer.IssueToken(
            new[] { new Claim("sub", "user-1") },
            jti: Guid.NewGuid().ToString("N"),
            sessionId: Guid.NewGuid(),
            expiresAtUtc: DateTime.UtcNow.AddHours(1));

        var tampered = token[..^4] + "XXXX";
        Assert.False(await issuer.ValidateTokenAsync(tampered));
    }

    [Fact]
    public async Task ValidateTokenAsync_returns_false_for_expired_token()
    {
        var issuer = CreateIssuer();
        var token = issuer.IssueToken(
            new[] { new Claim("sub", "user-1") },
            jti: Guid.NewGuid().ToString("N"),
            sessionId: Guid.NewGuid(),
            expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        Assert.False(await issuer.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task ValidateTokenAsync_returns_false_for_wrong_audience()
    {
        var issuer = CreateIssuer();
        var foreign = IssueForeignToken(audience: "other-audience");
        Assert.False(await issuer.ValidateTokenAsync(foreign));
    }

    [Fact]
    public async Task ValidateTokenAsync_returns_false_for_wrong_issuer()
    {
        var issuer = CreateIssuer();
        var foreign = IssueForeignToken(issuer: "other-issuer");
        Assert.False(await issuer.ValidateTokenAsync(foreign));
    }

    private static JwtAccessTokenIssuer CreateIssuer()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = Secret,
                ["JwtSettings:Issuer"] = Issuer,
                ["JwtSettings:Audience"] = Audience,
            })
            .Build();
        return new JwtAccessTokenIssuer(config);
    }

    private static string IssueForeignToken(string? issuer = null, string? audience = null)
    {
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret));
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: new[] { new Claim("sub", "user-1") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Services;

/// <summary>Issues and validates signed JWT access tokens (same shape as <see cref="Controllers.AuthController"/> login).</summary>
public interface IJwtAccessTokenIssuer
{
    string IssueToken(IReadOnlyList<Claim> claims, string jti, Guid sessionId, DateTime expiresAtUtc);

    /// <summary>
    /// Validates signature, issuer, audience, and lifetime (zero clock skew).
    /// Does not check session revocation — that remains in JwtBearer OnTokenValidated.
    /// </summary>
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

public sealed class JwtAccessTokenIssuer : IJwtAccessTokenIssuer
{
    private readonly IConfiguration _configuration;

    public JwtAccessTokenIssuer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string IssueToken(IReadOnlyList<Claim> claims, string jti, Guid sessionId, DateTime expiresAtUtc)
    {
        var secretKey = RequireSecretKey();
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var tokenClaims = claims.ToList();
        tokenClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        tokenClaims.Add(new Claim("sid", sessionId.ToString()));

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: tokenClaims,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult(false);

        try
        {
            var secretKey = RequireSecretKey();
            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var validationParameters = CreateValidationParameters(secretKey);
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return Task.FromResult(principal != null);
        }
        catch (SecurityTokenException)
        {
            return Task.FromResult(false);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(false);
        }
    }

    private TokenValidationParameters CreateValidationParameters(string secretKey) =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["JwtSettings:Audience"],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            // Matches ApplicationHost JwtBearer RoleClaimType mapping.
            RoleClaimType = "role"
        };

    private string RequireSecretKey() =>
        _configuration["JwtSettings:SecretKey"]
        ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
}

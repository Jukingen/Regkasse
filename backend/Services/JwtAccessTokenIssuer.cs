using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KasseAPI_Final.Services;

/// <summary>Issues signed JWT access tokens (same shape as <see cref="Controllers.AuthController"/> login).</summary>
public interface IJwtAccessTokenIssuer
{
    string IssueToken(IReadOnlyList<Claim> claims, string jti, Guid sessionId, DateTime expiresAtUtc);
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
        var secretKey = _configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
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
}

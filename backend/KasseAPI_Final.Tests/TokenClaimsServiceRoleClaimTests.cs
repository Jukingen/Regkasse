using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KasseAPI_Final.Tests;

public class TokenClaimsServiceRoleClaimTests
{
    [Fact]
    public async Task BuildClaimsAsync_Emits_Role_Claim_Per_Assigned_Role()
    {
        var resolver = new MockRolePermissionResolver(Array.Empty<string>());
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "a@b.c",
            UserName = "a@b.c",
            FirstName = "A",
            LastName = "B",
            Role = Roles.SuperAdmin,
        };

        var claims = await svc.BuildClaimsAsync(user, new List<string> { Roles.Manager, Roles.SuperAdmin });

        var roleClaims = claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains(Roles.Manager, roleClaims);
        Assert.Contains(Roles.SuperAdmin, roleClaims);
        Assert.Equal(2, roleClaims.Count);
    }

    [Fact]
    public void ResolvePrimaryRole_Prefers_SuperAdmin_Over_Manager()
    {
        var canonical = TokenClaimsService.CollectCanonicalRoles(
            new List<string> { Roles.Manager, Roles.SuperAdmin },
            userRoleColumn: null);

        var primary = TokenClaimsService.ResolvePrimaryRole(canonical);

        Assert.Equal(Roles.SuperAdmin, primary);
    }

    [Fact]
    public async Task BuildClaimsAsync_Includes_User_Role_Column_When_Identity_Roles_Empty()
    {
        var resolver = new MockRolePermissionResolver(Array.Empty<string>());
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "a@b.c",
            UserName = "a@b.c",
            FirstName = "A",
            LastName = "B",
            Role = Roles.SuperAdmin,
        };

        var claims = await svc.BuildClaimsAsync(user, new List<string>());

        Assert.Contains(claims, c => c.Type == "role" && c.Value == Roles.SuperAdmin);
    }

    [Fact]
    public async Task BuildClaimsAsync_Jwt_RoundTrip_Preserves_SuperAdmin_Role_For_Authorization()
    {
        var resolver = new MockRolePermissionResolver(Array.Empty<string>());
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser { Id = "u1", Email = "a@b.c", UserName = "a@b.c", FirstName = "A", LastName = "B" };
        var claims = await svc.BuildClaimsAsync(user, new List<string> { Roles.SuperAdmin });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-must-be-at-least-32-bytes-long!!"));
        var token = new JwtSecurityToken(
            issuer: "Test",
            audience: "Test",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var jwt = handler.WriteToken(token);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Test",
            ValidateAudience = true,
            ValidAudience = "Test",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            RoleClaimType = "role",
        };

        var principal = handler.ValidateToken(jwt, parameters, out _);

        Assert.Contains(Roles.SuperAdmin, principal.FindAll("role").Select(c => c.Value));
        Assert.True(principal.IsInRole(Roles.SuperAdmin));
    }

    private sealed class MockRolePermissionResolver : IRolePermissionResolver
    {
        private readonly IReadOnlySet<string> _perms;

        public MockRolePermissionResolver(IEnumerable<string> perms) =>
            _perms = perms.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(
            IEnumerable<string> roleNames,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_perms);
    }
}

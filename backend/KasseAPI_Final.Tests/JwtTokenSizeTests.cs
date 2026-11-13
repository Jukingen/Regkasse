using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class JwtTokenSizeTests
{
    private const string Secret = "test-secret-key-at-least-32-characters-long!!";

    [Fact]
    public async Task Manager_Admin_Token_Fits_Cookie_Budget()
    {
        var token = await IssueForRoleAsync(Roles.Manager, ClientAppPolicy.Admin);
        var size = JwtCookieBudget.Utf8ByteCount(token);
        Assert.True(size <= JwtCookieBudget.DefaultMaxUtf8Bytes, $"Manager admin JWT is {size} bytes");
        AssertNoLongSoapClaimTypes(token);
    }

    [Fact]
    public async Task SuperAdmin_Admin_Token_Fits_Cookie_Budget()
    {
        var token = await IssueForRoleAsync(Roles.SuperAdmin, ClientAppPolicy.Admin);
        var size = JwtCookieBudget.Utf8ByteCount(token);
        Assert.True(size <= JwtCookieBudget.DefaultMaxUtf8Bytes, $"SuperAdmin JWT is {size} bytes");
        AssertNoLongSoapClaimTypes(token);
    }

    [Fact]
    public async Task Manager_Admin_Token_Omits_Permission_Catalog()
    {
        var token = await IssueForRoleAsync(Roles.Manager, ClientAppPolicy.Admin);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var jwt = handler.ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == PermissionCatalog.PermissionClaimType);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == Roles.Manager);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Contains(jwt.Claims, c => c.Type == ClientAppPolicy.AppContextClaimType && c.Value == ClientAppPolicy.Admin);
    }

    private static async Task<string> IssueForRoleAsync(string role, string appContext)
    {
        var perms = RolePermissionMatrix.GetPermissionsForRoles(new[] { role });
        var svc = new TokenClaimsService(new StaticPermissionResolver(perms));
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            Email = $"{role.ToLowerInvariant()}@example.com",
            UserName = $"{role.ToLowerInvariant()}@example.com",
            FirstName = "Test",
            LastName = role,
            Role = role,
        };
        var claims = await svc.BuildClaimsAsync(
            user,
            new List<string> { role },
            tenantId: Guid.NewGuid().ToString("D"),
            appContext: appContext);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var token = new JwtSecurityToken(
            issuer: "KasseAPI",
            audience: "KasseAPIUsers",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void AssertNoLongSoapClaimTypes(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var jwt = handler.ReadJwtToken(token);
        Assert.DoesNotContain(
            jwt.Claims,
            c => c.Type.Contains("schemas.xmlsoap.org", StringComparison.OrdinalIgnoreCase)
                 || c.Type.Contains("schemas.microsoft.com", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StaticPermissionResolver : IEffectivePermissionResolver
    {
        private readonly IReadOnlySet<string> _perms;

        public StaticPermissionResolver(IEnumerable<string> perms) =>
            _perms = perms.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
            string userId,
            IEnumerable<string> roleNames,
            Guid? tenantId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_perms);

        public Task<IReadOnlySet<string>> GetEffectivePermissionsWithRoleOverrideAsync(
            string userId,
            IReadOnlySet<string> rolePermissions,
            Guid? tenantId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_perms);
    }
}

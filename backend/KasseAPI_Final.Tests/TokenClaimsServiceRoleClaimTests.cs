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
        var resolver = new MockEffectivePermissionResolver(Array.Empty<string>());
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
        var resolver = new MockEffectivePermissionResolver(Array.Empty<string>());
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
        var resolver = new MockEffectivePermissionResolver(Array.Empty<string>());
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

    [Fact]
    public async Task BuildClaimsAsync_SuperAdmin_Emits_Only_SystemCritical_Permission()
    {
        var fullCatalog = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.SuperAdmin });
        Assert.True(fullCatalog.Count > 10, "SuperAdmin catalog should be large enough to overflow cookie limits if fully emitted");

        var resolver = new MockEffectivePermissionResolver(fullCatalog);
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "admin@admin.com",
            UserName = "admin@admin.com",
            FirstName = "Super",
            LastName = "Admin",
            Role = Roles.SuperAdmin,
        };

        var claims = await svc.BuildClaimsAsync(
            user,
            new List<string> { Roles.SuperAdmin },
            appContext: ClientAppPolicy.Admin);

        var permClaims = claims
            .Where(c => c.Type == PermissionCatalog.PermissionClaimType)
            .Select(c => c.Value)
            .ToList();

        Assert.Single(permClaims);
        Assert.Equal(AppPermissions.SystemCritical, permClaims[0]);
        Assert.Contains(claims, c => c.Type == "role" && c.Value == Roles.SuperAdmin);
        Assert.True(
            PermissionImplication.IsSatisfied(AppPermissions.UserView, permClaims),
            "Compact system.critical must satisfy catalog permissions via implication");
    }

    [Fact]
    public async Task BuildClaimsAsync_Manager_Still_Emits_Filtered_Permission_Catalog()
    {
        var managerPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager });
        var resolver = new MockEffectivePermissionResolver(managerPerms);
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser
        {
            Id = "u2",
            Email = "manager@example.com",
            UserName = "manager@example.com",
            FirstName = "Man",
            LastName = "Ager",
            Role = Roles.Manager,
        };

        var claims = await svc.BuildClaimsAsync(
            user,
            new List<string> { Roles.Manager },
            appContext: ClientAppPolicy.Admin);

        var permClaims = claims
            .Where(c => c.Type == PermissionCatalog.PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(permClaims.Count > 1);
        Assert.DoesNotContain(AppPermissions.SystemCritical, permClaims);
        Assert.Contains(AppPermissions.UserView, permClaims);
    }

    [Fact]
    public async Task BuildClaimsAsync_Admin_Cashier_StripsPosPermissionsFromJwt()
    {
        var cashierPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier });
        var resolver = new MockEffectivePermissionResolver(cashierPerms);
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser
        {
            Id = "u1",
            Email = "c@b.c",
            UserName = "c@b.c",
            FirstName = "C",
            LastName = "H",
            Role = Roles.Cashier,
        };

        var claims = await svc.BuildClaimsAsync(
            user,
            new List<string> { Roles.Cashier },
            appContext: ClientAppPolicy.Admin);

        var permClaims = claims.Where(c => c.Type == PermissionCatalog.PermissionClaimType).Select(c => c.Value).ToList();
        Assert.Contains(AppPermissions.ProductView, permClaims);
        Assert.Contains(AppPermissions.ReportView, permClaims);
        Assert.DoesNotContain(AppPermissions.TableView, permClaims);
        Assert.DoesNotContain(AppPermissions.TseSign, permClaims);
    }

    private sealed class MockEffectivePermissionResolver : IEffectivePermissionResolver
    {
        private readonly IReadOnlySet<string> _perms;

        public MockEffectivePermissionResolver(IEnumerable<string> perms) =>
            _perms = perms.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
            string userId,
            IEnumerable<string> roleNames,
            Guid? tenantId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_perms);
    }
}

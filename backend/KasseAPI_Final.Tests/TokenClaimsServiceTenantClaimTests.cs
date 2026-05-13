using System.Linq;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class TokenClaimsServiceTenantClaimTests
{
    [Fact]
    public async Task BuildClaimsAsync_Includes_UserId_Claim()
    {
        var resolver = new MockRolePermissionResolver(Array.Empty<string>());
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser { Id = "user-guid-1", Email = "a@b.c", UserName = "a@b.c", FirstName = "A", LastName = "B" };

        var claims = await svc.BuildClaimsAsync(user, new List<string> { "Cashier" });

        var userIdClaim = claims.FirstOrDefault(c => c.Type == "userId");
        Assert.NotNull(userIdClaim);
        Assert.Equal(user.Id, userIdClaim!.Value);
    }

    [Fact]
    public async Task BuildClaimsAsync_Includes_Tenant_Id_When_Provided()
    {
        var resolver = new MockRolePermissionResolver(Array.Empty<string>());
        var svc = new TokenClaimsService(resolver);
        var user = new ApplicationUser { Id = "u1", Email = "a@b.c", UserName = "a@b.c", FirstName = "A", LastName = "B" };
        var tid = "9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c";

        var claims = await svc.BuildClaimsAsync(user, new List<string> { "Cashier" }, tenantId: tid);

        var tenantClaim = claims.FirstOrDefault(c => c.Type == ScopeCheckService.TenantIdClaim);
        Assert.NotNull(tenantClaim);
        Assert.Equal(tid, tenantClaim!.Value);
    }

    private sealed class MockRolePermissionResolver : IRolePermissionResolver
    {
        private readonly IReadOnlySet<string> _perms;
        public MockRolePermissionResolver(IEnumerable<string> perms) =>
            _perms = perms.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleNames, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(_perms);
    }
}

using System.Security.Claims;
using KasseAPI_Final.Logging;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LogIdFormattingTests
{
    [Fact]
    public void ShortGuid_returns_first_eight_hex_chars()
    {
        var id = Guid.Parse("b0000001-0001-4001-8001-000000000001");
        Assert.Equal("b0000001", LogIdFormatting.ShortGuid(id));
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData("", "-")]
    [InlineData("  ", "-")]
    [InlineData("abc", "abc")]
    [InlineData("abcdefghij", "abcdefgh")]
    [InlineData("19e6176d-5c6a-44fd-8042-ef9c53622002", "19e6176d")]
    public void ShortId_handles_null_short_and_guid(string? input, string expected)
    {
        Assert.Equal(expected, LogIdFormatting.ShortId(input));
    }

    [Fact]
    public void FormatUser_uses_email_and_short_id()
    {
        Assert.Equal(
            "pos@dev2.regkasse.at (cfcee0a9)",
            LogIdFormatting.FormatUser("pos@dev2.regkasse.at", "cfcee0a9-11c1-44cc-978f-e676908580d1"));
    }

    [Fact]
    public void FormatUser_system_when_user_id_missing()
    {
        Assert.Equal("system", LogIdFormatting.FormatUser("x", null));
        Assert.Equal("system", LogIdFormatting.FormatUser(null, ""));
    }

    [Fact]
    public void FormatUser_unknown_when_label_missing()
    {
        Assert.Equal(
            "unknown (cfcee0a9)",
            LogIdFormatting.FormatUser(null, "cfcee0a9-11c1-44cc-978f-e676908580d1"));
    }

    [Fact]
    public void FormatTenant_uses_slug_and_short_id()
    {
        var id = Guid.Parse("b0000001-0001-4001-8001-000000000001");
        Assert.Equal("dev (b0000001)", LogIdFormatting.FormatTenant("dev", id));
        Assert.Equal("dev (b0000001)", LogIdFormatting.FormatTenant("dev", id.ToString("D")));
    }
}

public sealed class RequestLoggingScopeMiddlewareTests
{
    [Fact]
    public void BuildScope_includes_tenant_user_role_and_correlation()
    {
        var tenantId = Guid.Parse("b0000001-0001-4001-8001-000000000001");
        var userId = "19e6176d-5c6a-44fd-8042-ef9c53622002";
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("userId", userId),
                new Claim(ClaimTypes.Email, "admin@admin.com"),
                new Claim("role", "SuperAdmin"),
                new Claim(ScopeCheckService.TenantIdClaim, tenantId.ToString("D")),
            ], authenticationType: "Test"))
        };
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = "ae9b5c2098464432aaad0f5468445dc7";

        var accessor = new TestTenantAccessor
        {
            TenantId = tenantId,
            TenantSlug = "dev"
        };

        var scope = RequestLoggingScopeMiddleware.BuildScope(context, accessor);

        Assert.Equal("dev", scope[LogContextKeys.Tenant]);
        Assert.Equal("b0000001", scope[LogContextKeys.TenantId]);
        Assert.Equal("admin@admin.com", scope[LogContextKeys.User]);
        Assert.Equal("19e6176d", scope[LogContextKeys.UserId]);
        Assert.Equal("SuperAdmin", scope[LogContextKeys.Role]);
        Assert.Equal("ae9b5c20", scope[LogContextKeys.CorrelationId]);
    }

    [Fact]
    public void BuildScope_empty_when_anonymous_and_no_tenant()
    {
        var context = new DefaultHttpContext();
        var scope = RequestLoggingScopeMiddleware.BuildScope(context, new TestTenantAccessor());
        Assert.Empty(scope);
    }

    private sealed class TestTenantAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; }
        public string? TenantSlug { get; set; }
    }
}

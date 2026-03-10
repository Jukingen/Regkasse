using System.Linq;
using System.Security.Claims;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PaymentSecurityMiddleware: permission-first, path-based. No role list; AppPermissions only.
/// Refund → RefundCreate; Cancel/Modify/Void/Reverse → PaymentCancel; Update-status → PaymentTake.
/// Fail-closed: no claims or missing required permission → 403.
/// </summary>
public class PaymentSecurityMiddlewareTests
{
    private static readonly string ValidRefundBody = "{\"paymentId\":\"00000000-0000-0000-0000-000000000001\",\"refundAmount\":1.00,\"refundReason\":\"Test\"}";
    private static readonly string ValidCancelBody = "{\"paymentId\":\"00000000-0000-0000-0000-000000000001\",\"cancelReason\":\"Test\"}";

    private static DefaultHttpContext CreateContextWithPermissions(string[]? permissionClaims, string path = "/api/payment/refund", string requestBody = null!, bool authenticated = true)
    {
        var body = requestBody ?? ValidRefundBody;
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "POST";
        context.Request.Headers["User-Agent"] = "TestAgent";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        if (authenticated)
        {
            var identity = new ClaimsIdentity("Test");
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));
            if (permissionClaims != null)
            {
                foreach (var p in permissionClaims)
                    identity.AddClaim(new Claim(PermissionCatalog.PermissionClaimType, p));
            }
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }

    [Fact]
    public async Task InvokeAsync_RefundEndpoint_WhenUserHasRefundCreate_AllowsRequest()
    {
        var context = CreateContextWithPermissions(new[] { AppPermissions.RefundCreate }, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RefundEndpoint_WhenUserHasOnlyPaymentTake_Returns403()
    {
        var context = CreateContextWithPermissions(new[] { AppPermissions.PaymentTake }, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UpdateStatusEndpoint_WhenUserHasPaymentTake_AllowsRequest()
    {
        var context = CreateContextWithPermissions(new[] { AppPermissions.PaymentTake }, path: "/api/payment/update-status", requestBody: "{}");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CancelEndpoint_WhenUserHasPaymentCancel_AllowsRequest()
    {
        var context = CreateContextWithPermissions(new[] { AppPermissions.PaymentCancel }, path: "/api/payment/cancel", requestBody: ValidCancelBody);
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserHasAllPaymentPermissions_AllowsRefundRequest()
    {
        var context = CreateContextWithPermissions(new[] { AppPermissions.PaymentTake, AppPermissions.PaymentCancel, AppPermissions.RefundCreate }, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserHasNoPaymentPermission_Returns403()
    {
        var context = CreateContextWithPermissions(new[] { AppPermissions.OrderView, AppPermissions.ProductView });
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var logger = new Mock<ILogger<PaymentSecurityMiddleware>>().Object;
        var mw = new PaymentSecurityMiddleware(next, logger);

        await mw.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserHasNoPermissionClaims_Returns403()
    {
        var context = CreateContextWithPermissions(Array.Empty<string>());
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var logger = new Mock<ILogger<PaymentSecurityMiddleware>>().Object;
        var mw = new PaymentSecurityMiddleware(next, logger);

        await mw.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenNotAuthenticated_Returns403()
    {
        var context = CreateContextWithPermissions(null, authenticated: false);
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var logger = new Mock<ILogger<PaymentSecurityMiddleware>>().Object;
        var mw = new PaymentSecurityMiddleware(next, logger);

        await mw.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    // --- Role-to-permission: Admin/Manager/Cashier have payment permissions; Waiter has no RefundCreate ---

    [Fact]
    public async Task InvokeAsync_RefundEndpoint_WithAdminMatrixPermissions_AllowsRequest()
    {
        var adminPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.SuperAdmin }).ToArray();
        var context = CreateContextWithPermissions(adminPerms, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RefundEndpoint_WithCashierMatrixPermissions_AllowsRequest()
    {
        var cashierPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Cashier }).ToArray();
        var context = CreateContextWithPermissions(cashierPerms, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RefundEndpoint_WithManagerMatrixPermissions_AllowsRequest()
    {
        var managerPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Manager }).ToArray();
        var context = CreateContextWithPermissions(managerPerms, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RefundEndpoint_WithWaiterMatrixPermissions_Returns403()
    {
        var waiterPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Waiter }).ToArray();
        var context = CreateContextWithPermissions(waiterPerms, path: "/api/payment/refund");
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var mw = new PaymentSecurityMiddleware(next, new Mock<ILogger<PaymentSecurityMiddleware>>().Object);
        await mw.InvokeAsync(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}

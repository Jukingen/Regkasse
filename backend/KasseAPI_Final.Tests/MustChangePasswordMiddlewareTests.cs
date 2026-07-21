using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public class MustChangePasswordMiddlewareTests
{
    [Theory]
    [InlineData("/api/Auth/me", true)]
    [InlineData("/api/auth/login", true)]
    [InlineData("/api/UserManagement/me/password", true)]
    [InlineData("/api/health", true)]
    [InlineData("/api/license/status", true)]
    [InlineData("/api/system/development-mode", true)]
    [InlineData("/swagger/index.html", true)]
    [InlineData("/api/admin/products", false)]
    [InlineData("/api/companysettings", false)]
    public void IsExemptPath_classifies_allowed_routes(string path, bool expected)
    {
        Assert.Equal(expected, MustChangePasswordMiddleware.IsExemptPath(path));
    }

    [Fact]
    public async Task InvokeAsync_WhenMustChangePassword_blocks_protected_api()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "temp-user",
            Email = "temp@example.com",
            MustChangePasswordOnNextLogin = true,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var context = CreateAuthenticatedContext(userId, "/api/admin/products");
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);
        await middleware.InvokeAsync(context, db, LocalizationTestDoubles.ApiMessageLocalizer());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Passwortänderung erforderlich", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenMustChangePassword_allows_password_change_endpoint()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "temp-user",
            Email = "temp@example.com",
            MustChangePasswordOnNextLogin = true,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var context = CreateAuthenticatedContext(userId, "/api/UserManagement/me/password");
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);
        await middleware.InvokeAsync(context, db, LocalizationTestDoubles.ApiMessageLocalizer());

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenMustChangePassword_allows_license_status_for_pos_gate()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "temp-user",
            Email = "temp@example.com",
            MustChangePasswordOnNextLogin = true,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var context = CreateAuthenticatedContext(userId, "/api/license/status");
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);
        await middleware.InvokeAsync(context, db, LocalizationTestDoubles.ApiMessageLocalizer());

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenPasswordAlreadyChanged_allows_protected_api()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "regular-user",
            Email = "regular@example.com",
            MustChangePasswordOnNextLogin = false,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var context = CreateAuthenticatedContext(userId, "/api/admin/products");
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);
        await middleware.InvokeAsync(context, db, LocalizationTestDoubles.ApiMessageLocalizer());

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MustChangePassword_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string userId, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        context.User = new ClaimsPrincipal(identity);

        return context;
    }
}

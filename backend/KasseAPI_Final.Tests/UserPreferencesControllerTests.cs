using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserPreferencesControllerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserPrefs_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserPreferencesController CreateController(AppDbContext db, string userId = "user-a")
    {
        var controller = new UserPreferencesController(db, Mock.Of<ILogger<UserPreferencesController>>());
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, Roles.Manager),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) },
        };
        return controller;
    }

    [Fact]
    public async Task GetPreferences_WhenNoneStored_ReturnsDefaults()
    {
        await using var db = CreateContext();
        var controller = CreateController(db);

        var result = await controller.GetPreferences(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<UserPreferencesResponseDto>(ok.Value);

        Assert.Equal("system", body.ThemeMode);
        Assert.Equal("standard", body.DensityMode);
        Assert.Equal("/dashboard", body.DefaultPage);
        Assert.Null(body.UpdatedAtUtc);
    }

    [Fact]
    public async Task PutPreferences_PersistsPerUser()
    {
        await using var db = CreateContext();
        var controllerA = CreateController(db, "user-a");
        var controllerB = CreateController(db, "user-b");

        var saveA = await controllerA.SavePreferences(new SaveUserPreferencesRequestDto
        {
            ThemeMode = "dark",
            DensityMode = "compact",
            DefaultPage = "/admin/users",
            DateFormat = "DD.MM.YYYY",
            TimeFormat = "24h",
            ReducedAnimations = true,
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(saveA.Result);

        var saveB = await controllerB.SavePreferences(new SaveUserPreferencesRequestDto
        {
            ThemeMode = "light",
            DensityMode = "comfortable",
            DefaultPage = "/users",
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(saveB.Result);

        var getA = Assert.IsType<OkObjectResult>((await controllerA.GetPreferences(CancellationToken.None)).Result);
        var prefsA = Assert.IsType<UserPreferencesResponseDto>(getA.Value);
        Assert.Equal("dark", prefsA.ThemeMode);
        Assert.Equal("compact", prefsA.DensityMode);
        Assert.Equal("/admin/users", prefsA.DefaultPage);
        Assert.True(prefsA.ReducedAnimations);

        var getB = Assert.IsType<OkObjectResult>((await controllerB.GetPreferences(CancellationToken.None)).Result);
        var prefsB = Assert.IsType<UserPreferencesResponseDto>(getB.Value);
        Assert.Equal("light", prefsB.ThemeMode);
        Assert.Equal("comfortable", prefsB.DensityMode);

        Assert.Equal(2, await db.UserPreferences.CountAsync());
    }

    [Fact]
    public async Task PutPreferences_NormalizesInvalidValues()
    {
        await using var db = CreateContext();
        var controller = CreateController(db);

        var save = await controller.SavePreferences(new SaveUserPreferencesRequestDto
        {
            ThemeMode = "neon",
            DensityMode = "huge",
            DefaultPage = "/unknown",
            TimeFormat = "48h",
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(save.Result);
        var body = Assert.IsType<UserPreferencesResponseDto>(ok.Value);
        Assert.Equal("system", body.ThemeMode);
        Assert.Equal("standard", body.DensityMode);
        Assert.Equal("/dashboard", body.DefaultPage);
        Assert.Null(body.TimeFormat);
    }
}

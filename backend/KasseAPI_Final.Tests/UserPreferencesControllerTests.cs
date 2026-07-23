using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
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
        var service = new UserPreferencesService(db, Mock.Of<ILogger<UserPreferencesService>>());
        var controller = new UserPreferencesController(service);
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
        Assert.Equal("DD.MM.YYYY", body.DateFormat);
        Assert.Equal("24h", body.TimeFormat);
        Assert.Equal("Europe/Vienna", body.TimeZone);
        Assert.Equal("de", body.Language);
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
            DateFormat = "YYYY-MM-DD",
            TimeFormat = "12h",
            TimeZone = "Europe/Berlin",
            Language = "en",
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
        Assert.Equal("YYYY-MM-DD", prefsA.DateFormat);
        Assert.Equal("12h", prefsA.TimeFormat);
        Assert.Equal("Europe/Berlin", prefsA.TimeZone);
        Assert.Equal("en", prefsA.Language);
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
            TimeZone = "Mars/Phobos",
            Language = "xx",
            DateFormat = "invalid",
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(save.Result);
        var body = Assert.IsType<UserPreferencesResponseDto>(ok.Value);
        Assert.Equal("system", body.ThemeMode);
        Assert.Equal("standard", body.DensityMode);
        Assert.Equal("/dashboard", body.DefaultPage);
        Assert.Equal("24h", body.TimeFormat);
        Assert.Equal("Europe/Vienna", body.TimeZone);
        Assert.Equal("de", body.Language);
        Assert.Equal("DD.MM.YYYY", body.DateFormat);
    }
}

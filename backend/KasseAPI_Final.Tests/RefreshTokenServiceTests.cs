using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RefreshTokenServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"refresh_token_tests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static RefreshTokenService CreateService(AppDbContext db)
    {
        var options = Options.Create(new AuthOptions
        {
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7
        });
        return new RefreshTokenService(db, options, NullLogger<RefreshTokenService>.Instance);
    }

    private static Task<string> BuildAccessToken(string userId, string jti, Guid sessionId, DateTime expiresAtUtc, string clientApp) =>
        Task.FromResult($"{userId}:{jti}:{sessionId:N}:{expiresAtUtc:O}:{clientApp}");

    [Fact]
    public async Task LoginThenRefreshThenRefresh_SucceedsWithRotation()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var r1 = await service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var r2 = await service.RotateAsync(r1.Tokens!.RefreshToken, BuildAccessToken);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
    }

    [Fact]
    public async Task RevokedRefreshToken_IsRejected()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var revoked = await service.RevokeRefreshTokenAsync(login.RefreshToken, "manual");
        var refresh = await service.RotateAsync(login.RefreshToken, BuildAccessToken);

        Assert.True(revoked);
        Assert.False(refresh.Success);
    }

    [Fact]
    public async Task ReusedRefreshToken_DetectsReuse()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var first = await service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var second = await service.RotateAsync(login.RefreshToken, BuildAccessToken);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.True(second.ReuseDetected);
    }

    [Fact]
    public async Task ParallelRefreshRace_OnlyOneSucceeds()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var t1 = service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var t2 = service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var results = await Task.WhenAll(t1, t2);

        Assert.Single(results, x => x.Success);
        Assert.Single(results, x => !x.Success);
    }

    [Fact]
    public async Task LogoutInvalidation_DisablesSession()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        await service.LogoutSessionAsync(login.SessionId, "logout");
        var active = await service.IsSessionActiveAsync("u1", login.SessionId);

        Assert.False(active);
    }

    [Fact]
    public async Task LogoutInvalidation_RejectsFurtherRefresh()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        await service.LogoutSessionAsync(login.SessionId, "logout");
        var refresh = await service.RotateAsync(login.RefreshToken, BuildAccessToken);

        Assert.False(refresh.Success);
        Assert.Equal("session_revoked", refresh.ErrorCode);
    }
}

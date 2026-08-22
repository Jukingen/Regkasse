using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SessionManagementServiceTests
{
    [Fact]
    public async Task GetActiveSessionsAsync_ReturnsOnlyNonRevoked_WithUserIdentity()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "u1", "cashier1");
        var active = AddSession(db, user.Id, "admin");
        AddSession(db, user.Id, "pos", revoked: true);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var list = await sut.GetActiveSessionsAsync();

        var row = Assert.Single(list);
        Assert.Equal(active.Id, row.Id);
        Assert.Equal("cashier1", row.UserName);
        Assert.Equal("Cashier", row.Role);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task TerminateSessionAsync_RevokesSessionAndRefreshTokens()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "u1", "cashier1");
        var session = AddSession(db, user.Id, "admin");
        AddRefreshToken(db, user.Id, session.Id);
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        var sut = CreateSut(db, audit.Object);
        var ok = await sut.TerminateSessionAsync(session.Id, "sa-1", Roles.SuperAdmin);

        Assert.True(ok);
        Assert.NotNull((await db.AuthSessions.SingleAsync(s => s.Id == session.Id)).RevokedAtUtc);
        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAtUtc);
        audit.Verify(
            a => a.LogUserLifecycleAsync(
                AuditEventType.UserSessionTerminated,
                "sa-1",
                Roles.SuperAdmin,
                user.Id,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()),
            Times.Once);
    }

    [Fact]
    public async Task TerminateSessionAsync_UnknownOrAlreadyRevoked_ReturnsFalse()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        Assert.False(await sut.TerminateSessionAsync(Guid.NewGuid(), "sa-1", Roles.SuperAdmin));
    }

    [Fact]
    public async Task ForceLogoutAsync_RotatesSecurityStamp_AndRevokesAllSessions()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        var user = await SeedUserAsync(db, "u1", "cashier1", userManager);
        var stampBefore = user.SecurityStamp;
        var sessionA = AddSession(db, user.Id, "admin");
        var sessionB = AddSession(db, user.Id, "pos");
        AddRefreshToken(db, user.Id, sessionA.Id);
        AddRefreshToken(db, user.Id, sessionB.Id);
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        var sut = CreateSut(db, audit.Object, userManager);
        var ok = await sut.ForceLogoutAsync(user.Id, "sa-1", Roles.SuperAdmin);

        Assert.True(ok);
        var persisted = await userManager.FindByIdAsync(user.Id);
        Assert.NotNull(persisted);
        Assert.NotEqual(stampBefore, persisted!.SecurityStamp);
        Assert.Equal(2, await db.AuthSessions.CountAsync(s => s.UserId == user.Id && s.RevokedAtUtc != null));
        Assert.Equal(0, await db.AuthSessions.CountAsync(s => s.UserId == user.Id && s.RevokedAtUtc == null));
        audit.Verify(
            a => a.LogUserLifecycleAsync(
                AuditEventType.UserForceLogout,
                "sa-1",
                Roles.SuperAdmin,
                user.Id,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<UserCreatedAuditDetails?>()),
            Times.Once);
    }

    [Fact]
    public async Task ForceLogoutAsync_UnknownUser_ReturnsFalse()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        Assert.False(await sut.ForceLogoutAsync("missing", "sa-1", Roles.SuperAdmin));
    }

    [Fact]
    public async Task TerminateAllSessionsAsync_KeepsExceptSession()
    {
        await using var db = CreateDb();
        var keep = AddSession(db, "sa-1", "admin");
        AddSession(db, "u2", "pos");
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var count = await sut.TerminateAllSessionsAsync("sa-1", Roles.SuperAdmin, keep.Id);

        Assert.Equal(1, count);
        Assert.Null((await db.AuthSessions.SingleAsync(s => s.Id == keep.Id)).RevokedAtUtc);
        Assert.Equal(1, await db.AuthSessions.CountAsync(s => s.RevokedAtUtc != null));
    }

    [Fact]
    public async Task IsSessionValidAsync_RejectsInactiveUser_MismatchedStamp_AndRevokedSession()
    {
        await using var db = CreateDb();
        var userManager = CreateUserManager(db);
        var user = await SeedUserAsync(db, "u1", "cashier1", userManager);
        var session = AddSession(db, user.Id, "admin");
        await db.SaveChangesAsync();

        var sut = CreateSut(db, userManager: userManager);
        Assert.True(await sut.IsSessionValidAsync(user.Id, session.Id, user.SecurityStamp));

        Assert.False(await sut.IsSessionValidAsync(user.Id, session.Id, "other-stamp"));

        user.IsActive = false;
        await userManager.UpdateAsync(user);
        Assert.False(await sut.IsSessionValidAsync(user.Id, session.Id, user.SecurityStamp));

        user.IsActive = true;
        await userManager.UpdateAsync(user);
        await sut.TerminateSessionAsync(session.Id, "sa-1", Roles.SuperAdmin);
        Assert.False(await sut.IsSessionValidAsync(user.Id, session.Id, user.SecurityStamp));
    }

    [Fact]
    public async Task IsSessionValidAsync_WithoutSessionId_AllowsActiveUserWhenStampMatches()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, "u1", "cashier1");
        await db.SaveChangesAsync();
        var sut = CreateSut(db);
        Assert.True(await sut.IsSessionValidAsync(user.Id, sessionId: null, user.SecurityStamp));
        Assert.False(await sut.IsSessionValidAsync("missing", sessionId: null, null));
    }

    private static SessionManagementService CreateSut(
        AppDbContext db,
        IAuditLogService? audit = null,
        UserManager<ApplicationUser>? userManager = null)
    {
        var refresh = new RefreshTokenService(
            db,
            Options.Create(new AuthOptions { AccessTokenLifetimeMinutes = 15, RefreshTokenLifetimeDays = 7 }),
            NullLogger<RefreshTokenService>.Instance);
        return new SessionManagementService(
            db,
            userManager ?? CreateUserManager(db),
            refresh,
            audit ?? Mock.Of<IAuditLogService>(),
            NullLogger<SessionManagementService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"session_mgmt_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static async Task<ApplicationUser> SeedUserAsync(
        AppDbContext db,
        string id,
        string userName,
        UserManager<ApplicationUser>? userManager = null)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.test",
            NormalizedEmail = $"{userName}@example.test".ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            Role = Roles.Cashier,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("D"),
        };
        if (userManager != null)
        {
            var result = await userManager.CreateAsync(user);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
            return (await userManager.FindByIdAsync(id))!;
        }

        db.Users.Add(user);
        return user;
    }

    private static AuthSession AddSession(AppDbContext db, string userId, string clientApp, bool revoked = false)
    {
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientApp = clientApp,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            LastActivityAtUtc = DateTime.UtcNow.AddMinutes(-1),
            RevokedAtUtc = revoked ? DateTime.UtcNow.AddMinutes(-2) : null,
            UserAgent = "Mozilla/5.0 Chrome/120.0 Windows",
            IpAddress = "127.0.0.1",
        };
        db.AuthSessions.Add(session);
        return session;
    }

    private static void AddRefreshToken(AppDbContext db, string userId, Guid sessionId)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            TokenHash = Guid.NewGuid().ToString("N"),
            AccessJti = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
        });
    }
}

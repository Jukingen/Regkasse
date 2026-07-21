using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RefreshTokenServiceTests
{
    private sealed class MissingAuthSchemaContext : AppDbContext
    {
        public MissingAuthSchemaContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var inner = new Exception("42P01: relation \"auth_sessions\" does not exist");
            throw new DbUpdateException("Simulated missing auth table", inner);
        }
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"refresh_token_tests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
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

    private static Task<string> BuildAccessToken(
        string userId,
        string jti,
        Guid sessionId,
        DateTime expiresAtUtc,
        string clientApp,
        string? persistedSessionTenantId = null) =>
        Task.FromResult($"{userId}:{jti}:{sessionId:N}:{expiresAtUtc:O}:{clientApp}:{persistedSessionTenantId ?? ""}");

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
    public async Task Login_WhenAuthSchemaPresent_CreatesSessionAndRefreshTokenRows()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.Equal(1, await db.AuthSessions.CountAsync());
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Login_WhenAuthSchemaMissing_ThrowsExplicitDiagnostic()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"refresh_token_schema_missing_{Guid.NewGuid()}")
            .Options;
        using var db = new MissingAuthSchemaContext(options);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken));

        Assert.Contains("Critical auth schema is missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AddAuthSessionsAndRefreshTokens", ex.Message, StringComparison.Ordinal);
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
    public async Task ReusedRefreshToken_IsRejectedWithoutSessionInvalidation()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var first = await service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var second = await service.RotateAsync(login.RefreshToken, BuildAccessToken);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.False(second.ReuseDetected);
        Assert.Equal("refresh_token_already_used", second.ErrorCode);
        Assert.True(await service.IsSessionActiveAsync("u1", login.SessionId));
    }

    [Fact]
    public async Task ParallelRefreshRace_OnlyOneSucceedsWithoutSessionInvalidation()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var t1 = service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var t2 = service.RotateAsync(login.RefreshToken, BuildAccessToken);
        var results = await Task.WhenAll(t1, t2);

        Assert.Single(results, x => x.Success);
        var loser = results.Single(x => !x.Success);
        Assert.False(loser.ReuseDetected);
        Assert.True(
            loser.ErrorCode is "refresh_token_parallel_conflict" or "refresh_token_already_used",
            $"Unexpected error code: {loser.ErrorCode}");
        Assert.True(await service.IsSessionActiveAsync("u1", login.SessionId));
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

    [Fact]
    public async Task Login_Persists_Session_TenantId_When_Provided()
    {
        using var db = CreateContext();
        var tid = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tid, Name = "T", Slug = "t" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken, sessionTenantId: tid);

        var session = await db.AuthSessions.SingleAsync(s => s.Id == login.SessionId);
        Assert.Equal(tid, session.TenantId);
    }

    /// <summary>Session <c>tenant_id</c> is set at login from <see cref="Tenancy.ILoginTenantResolver"/>; refresh must keep passing it to token issuance.</summary>
    [Fact]
    public async Task RotateAsync_Passes_Persisted_Session_Tenant_To_BuildAccessToken()
    {
        using var db = CreateContext();
        var tid = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tid, Name = "T", Slug = "t" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        string? capturedOnRotate = null;
        Task<string> CaptureOnRotate(
            string userId,
            string jti,
            Guid sessionId,
            DateTime expiresAtUtc,
            string clientApp,
            string? persistedSessionTenantId)
        {
            capturedOnRotate = persistedSessionTenantId;
            return BuildAccessToken(userId, jti, sessionId, expiresAtUtc, clientApp, persistedSessionTenantId);
        }

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken, sessionTenantId: tid);
        var r1 = await service.RotateAsync(login.RefreshToken, CaptureOnRotate);

        Assert.True(r1.Success);
        Assert.Equal(tid.ToString("D"), capturedOnRotate);
        Assert.Contains(tid.ToString("D"), r1.Tokens!.AccessToken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RotateAsync_Preserves_Non_Default_Tenant_As_Would_Come_From_Membership()
    {
        using var db = CreateContext();
        var tid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        db.Tenants.Add(new Tenant { Id = tid, Name = "Member", Slug = "member" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        string? captured = null;
        Task<string> Capture(
            string userId,
            string jti,
            Guid sessionId,
            DateTime expiresAtUtc,
            string clientApp,
            string? persistedSessionTenantId)
        {
            captured = persistedSessionTenantId;
            return BuildAccessToken(userId, jti, sessionId, expiresAtUtc, clientApp, persistedSessionTenantId);
        }

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken, sessionTenantId: tid);
        var r1 = await service.RotateAsync(login.RefreshToken, Capture);

        Assert.True(r1.Success);
        Assert.Equal(tid.ToString("D"), captured);
    }

    [Fact]
    public async Task RotateAsync_Legacy_Session_Without_Tenant_Passes_Null_To_BuildAccessToken()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        string? captured = null;
        Task<string> Capture(
            string userId,
            string jti,
            Guid sessionId,
            DateTime expiresAtUtc,
            string clientApp,
            string? persistedSessionTenantId)
        {
            captured = persistedSessionTenantId;
            return BuildAccessToken(userId, jti, sessionId, expiresAtUtc, clientApp, persistedSessionTenantId);
        }

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken);
        var r1 = await service.RotateAsync(login.RefreshToken, Capture);

        Assert.True(r1.Success);
        Assert.Null(captured);
    }

    [Fact]
    public async Task RotateAsync_With_Tenant_Override_Updates_Session_And_Passes_To_BuildAccessToken()
    {
        using var db = CreateContext();
        var loginTenant = LegacyDefaultTenantIds.Primary;
        var switchTenant = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        db.Tenants.Add(new Tenant { Id = loginTenant, Name = "Default", Slug = "default" });
        db.Tenants.Add(new Tenant { Id = switchTenant, Name = "Dev", Slug = "dev" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        string? captured = null;
        Task<string> Capture(
            string userId,
            string jti,
            Guid sessionId,
            DateTime expiresAtUtc,
            string clientApp,
            string? persistedSessionTenantId)
        {
            captured = persistedSessionTenantId;
            return BuildAccessToken(userId, jti, sessionId, expiresAtUtc, clientApp, persistedSessionTenantId);
        }

        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken, sessionTenantId: loginTenant);
        var allowed = false;
        var r1 = await service.RotateAsync(
            login.RefreshToken,
            Capture,
            sessionTenantIdOverride: switchTenant,
            canAssignTenant: (_, tid, _) =>
            {
                allowed = tid == switchTenant;
                return Task.FromResult(true);
            });

        Assert.True(r1.Success);
        Assert.True(allowed);
        Assert.Equal(switchTenant.ToString("D"), captured);

        var session = await db.AuthSessions.SingleAsync(s => s.Id == login.SessionId);
        Assert.Equal(switchTenant, session.TenantId);
    }

    [Fact]
    public async Task RotateAsync_Tenant_Override_Rejected_Does_Not_Consume_Refresh_Token()
    {
        using var db = CreateContext();
        var loginTenant = LegacyDefaultTenantIds.Primary;
        var switchTenant = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        db.Tenants.Add(new Tenant { Id = loginTenant, Name = "Default", Slug = "default" });
        db.Tenants.Add(new Tenant { Id = switchTenant, Name = "Other", Slug = "other" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var login = await service.IssueLoginTokensAsync("u1", "admin", BuildAccessToken, sessionTenantId: loginTenant);
        var r1 = await service.RotateAsync(
            login.RefreshToken,
            BuildAccessToken,
            sessionTenantIdOverride: switchTenant,
            canAssignTenant: (_, _, _) => Task.FromResult(false));

        Assert.False(r1.Success);
        Assert.Equal("tenant_switch_forbidden", r1.ErrorCode);

        var refreshRow = await db.RefreshTokens.SingleAsync(t => t.SessionId == login.SessionId);
        Assert.Null(refreshRow.ConsumedAtUtc);

        var session = await db.AuthSessions.SingleAsync(s => s.Id == login.SessionId);
        Assert.Equal(loginTenant, session.TenantId);
    }
}

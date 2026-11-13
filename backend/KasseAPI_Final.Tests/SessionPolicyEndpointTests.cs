using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Session;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SessionPolicyEndpointTests
{
    private static AppDbContext CreateContext(ICurrentTenantAccessor? accessor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SessionPolicy_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, accessor ?? NullCurrentTenantAccessor.Instance);
    }

    private static SessionPolicyOptions DefaultOptions() => new()
    {
        MaxConcurrentSessions = 1,
        SessionTimeoutMinutes = 30,
        AllowMultipleDevices = false,
    };

    private static ITenantSessionPolicyService CreateService(
        AppDbContext db,
        SessionPolicyOptions? options = null,
        ICurrentTenantAccessor? accessor = null)
    {
        return new TenantSessionPolicyService(
            db,
            accessor ?? NullCurrentTenantAccessor.Instance,
            Options.Create(options ?? DefaultOptions()));
    }

    [Fact]
    public async Task GetSessionPolicy_ReturnsPlatformDefaults_WhenNoTenant()
    {
        await using var db = CreateContext();
        var policy = CreateService(db);
        var controller = new UserSessionsController(
            Mock.Of<ISessionService>(),
            Mock.Of<IDeviceSessionService>(),
            policy,
            Mock.Of<ILogger<UserSessionsController>>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        var result = await controller.GetSessionPolicy(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TenantSessionPolicyDto>(ok.Value);

        Assert.Equal(1, body.MaxConcurrentSessions);
        Assert.Equal(30, body.SessionTimeoutMinutes);
        Assert.False(body.AllowMultipleDevices);
    }

    [Fact]
    public async Task GetPolicyAsync_UsesAppsettingsConcurrentLimits_AndTenantIdleTimeout()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new FixedTenantAccessor(tenantId);
        await using var db = CreateContext(accessor);
        db.SystemSettings.Add(new SystemSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyName = "Test",
            CompanyAddress = "Addr",
            CompanyTaxNumber = "ATU00000000",
            DefaultLanguage = "de",
            DefaultCurrency = "EUR",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            SessionTimeoutMinutes = 60,
            SessionWarningBeforeTimeoutMinutes = 10,
            KeepCartAfterTimeout = false,
            SessionIdleTimeoutEnabled = true,
        });
        await db.SaveChangesAsync();

        var options = new SessionPolicyOptions
        {
            MaxConcurrentSessions = 3,
            SessionTimeoutMinutes = 30,
            AllowMultipleDevices = true,
        };
        var service = CreateService(db, options, accessor);

        var body = await service.GetPolicyAsync(tenantId);

        Assert.Equal(3, body.MaxConcurrentSessions);
        Assert.True(body.AllowMultipleDevices);
        Assert.Equal(60, body.SessionTimeoutMinutes);
        Assert.Equal(10, body.WarningBeforeTimeoutMinutes);
        Assert.False(body.KeepCartAfterTimeout);
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    public string? TenantSlug { get; set; }
    }
}

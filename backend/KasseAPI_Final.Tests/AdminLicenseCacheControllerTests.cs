using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminLicenseCacheControllerTests
{
    [Fact]
    public async Task Clear_ByTenantId_InvalidatesCacheAndReturnsOk()
    {
        await using var db = await CreateDbAsync();
        var tenant = await SeedTenantAsync(db, "cafe");
        var memory = CreateMemory();
        var licenseCache = new LicenseStatusCache(memory, Microsoft.Extensions.Options.Options.Create(new KasseAPI_Final.Configuration.CacheSettings()), NullLogger<LicenseStatusCache>.Instance);

        await licenseCache.GetOrCreateAsync(
            tenant.Id,
            _ => Task.FromResult(new TenantLicenseStatus { Status = "none", IsValid = false }));
        Assert.True(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));

        var audit = new Mock<IAuditLogService>();
        var controller = CreateController(db, licenseCache, audit.Object);

        var result = await controller.Clear(
            new ClearLicenseCacheRequest { TenantId = tenant.Id },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ClearLicenseCacheResponse>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal(tenant.Id, payload.TenantId);
        Assert.Equal("cafe", payload.TenantSlug);
        Assert.Equal(LicenseStatusCache.BuildKey(tenant.Id), payload.CacheKey);
        Assert.False(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));

        audit.Verify(
            a => a.LogSystemOperationAsync(
                AuditLogActions.SYSTEM_CACHE_CLEARED,
                "LicenseStatusCache",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.SystemCacheCleared,
                It.IsAny<Guid?>(),
                tenant.Id,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task Clear_ByTenantSlug_InvalidatesCache()
    {
        await using var db = await CreateDbAsync();
        var tenant = await SeedTenantAsync(db, "bistro");
        var memory = CreateMemory();
        var licenseCache = new LicenseStatusCache(memory, Microsoft.Extensions.Options.Options.Create(new KasseAPI_Final.Configuration.CacheSettings()), NullLogger<LicenseStatusCache>.Instance);

        await licenseCache.GetOrCreateAsync(
            tenant.Id,
            _ => Task.FromResult(new TenantLicenseStatus { Status = "valid", IsValid = true }));

        var controller = CreateController(db, licenseCache, Mock.Of<IAuditLogService>());
        var result = await controller.Clear(
            new ClearLicenseCacheRequest { TenantSlug = "bistro" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ClearLicenseCacheResponse>(ok.Value);
        Assert.Equal(tenant.Id, payload.TenantId);
        Assert.False(await memory.ExistsAsync(LicenseStatusCache.BuildKey(tenant.Id)));
    }

    [Fact]
    public async Task Clear_WithoutTarget_ReturnsBadRequest()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(
            db,
            Mock.Of<ILicenseStatusCache>(),
            Mock.Of<IAuditLogService>());

        var result = await controller.Clear(
            new ClearLicenseCacheRequest(),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Clear_UnknownTenant_ReturnsNotFound()
    {
        await using var db = await CreateDbAsync();
        var controller = CreateController(
            db,
            Mock.Of<ILicenseStatusCache>(),
            Mock.Of<IAuditLogService>());

        var result = await controller.Clear(
            new ClearLicenseCacheRequest { TenantSlug = "missing-tenant" },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static AdminLicenseCacheController CreateController(
        AppDbContext db,
        ILicenseStatusCache licenseStatusCache,
        IAuditLogService auditLogService)
    {
        var controller = new AdminLicenseCacheController(
            db,
            licenseStatusCache,
            auditLogService,
            NullLogger<AdminLicenseCacheController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                        new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    ],
                    authenticationType: "Test")),
                },
            },
        };
        return controller;
    }

    private static MemoryCacheService CreateMemory() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService());

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminLicenseCache_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        return db;
    }

    private static async Task<Tenant> SeedTenantAsync(AppDbContext db, string slug)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Test {slug}",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return tenant;
    }
}

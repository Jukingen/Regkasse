using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// P0: unknown host/header slugs must not fall back to the platform sentinel tenant.
/// </summary>
public sealed class UnknownSlugReturnsNullNotPlatformTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UnknownSlug_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static TenantContextService CreateService(
        AppDbContext db,
        ICurrentTenantAccessor accessor,
        bool isDevelopment)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        return new TenantContextService(
            db,
            accessor,
            environment.Object,
            Mock.Of<ITenantDomainService>(),
            NullLogger<TenantContextService>.Instance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApplyFromHostAsync_UnknownSlug_LeavesAmbientNull_NotPlatform(bool isDevelopment)
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor { TenantId = SystemTenantIds.Platform };
        var service = CreateService(db, accessor, isDevelopment);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("unknown-mandant.regkasse.at");

        await service.ApplyFromHostAsync(httpContext);

        Assert.Null(accessor.TenantId);
        Assert.Null(accessor.TenantSlug);
        Assert.NotEqual(SystemTenantIds.Platform, accessor.TenantId);
    }

    [Fact]
    public async Task ApplyFromHostAsync_Production_AdminReservedHost_StillBindsPlatformSentinel()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(db);
        await db.SaveChangesAsync();

        var accessor = new CurrentTenantAccessor();
        var service = CreateService(db, accessor, isDevelopment: false);

        var httpContext = new DefaultHttpContext();
        // Loopback maps to slug "admin" → platform sentinel (reserved host, not unknown slug).
        httpContext.Request.Host = new HostString("localhost");

        await service.ApplyFromHostAsync(httpContext);

        Assert.Equal(SystemTenantIds.Platform, accessor.TenantId);
        Assert.Equal(SystemTenantIds.PlatformSlug, accessor.TenantSlug);
    }
}

using System.Security.Claims;
using System.Text.Json;
using System.Runtime.CompilerServices;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class VerticalProfileCatalogTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task Create_BlankProfile_IsCustomWithFeaturesOff()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);

        var (result, error) = await catalog.CreateAsync(
            new UpsertVerticalProfileRequest { Id = "bakery", Name = "Bakery" },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("bakery", result!.Profile.Id);
        Assert.Equal(VerticalProfileSources.Custom, result.Profile.Source);
        Assert.Equal(0, result.Profile.FeatureCount);
        Assert.False(result.Profile.PosFeatures.GetProperty("kitchenDisplay").GetBoolean());
        Assert.Equal(VerticalProfileLayouts.Standard, result.Profile.PosLayout);
    }

    [Fact]
    public async Task Clone_CopiesSourceFeaturesUnderNewSlug()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);

        var (result, error) = await catalog.CloneAsync(
            VerticalProfileIds.Gastronomy,
            new CloneVerticalProfileRequest { Id = "gastronomy-copy", Name = "Gastronomy copy" },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("gastronomy-copy", result!.Profile.Id);
        Assert.Equal(VerticalProfileSources.Custom, result.Profile.Source);
        Assert.True(result.Profile.PosFeatures.GetProperty("kitchenDisplay").GetBoolean());
        Assert.Equal(VerticalProfileLayouts.Standard, result.Profile.PosLayout);
    }

    [Fact]
    public async Task Patch_DatabaseOverrideWinsOverCodeSeed()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);
        var service = CreateService(db);

        var (result, error) = await catalog.UpdateAsync(
            VerticalProfileIds.Gastronomy,
            new UpsertVerticalProfileRequest
            {
                Name = "Gastronomy edited",
                PosFeatures = Json(
                    """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":false,"routeTracking":false,"roomTracking":false,"ticketScan":false}"""),
            },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(VerticalProfileSources.Override, result!.Profile.Source);
        Assert.False(result.Profile.PosFeatures.GetProperty("kitchenDisplay").GetBoolean());
        Assert.Contains("kitchenDisplay", result.RemovedFeatures);

        db.ChangeTracker.Clear();
        var storedSeed = await db.VerticalProfiles.SingleAsync(row => row.Id == VerticalProfileIds.Gastronomy);
        Assert.Contains("\"kitchenDisplay\":true", storedSeed.PosFeatures, StringComparison.Ordinal);

        var active = await service.GetActiveAsync(VerticalProfileIds.Gastronomy);
        Assert.NotNull(active);
        Assert.False(active!.PosFeatures.GetProperty("kitchenDisplay").GetBoolean());
        Assert.Equal("Gastronomy edited", active.Name);
    }

    [Fact]
    public async Task PutFeatures_ReplacesPosFeatureMap()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);

        var (result, error) = await catalog.UpdateFeaturesAsync(
            VerticalProfileIds.Gastronomy,
            new UpdateVerticalProfileFeaturesRequest
            {
                PosFeatures = Json("""{"tables":true,"kitchenDisplay":false}"""),
            },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Profile.PosFeatures.GetProperty("tables").GetBoolean());
        Assert.False(result.Profile.PosFeatures.GetProperty("kitchenDisplay").GetBoolean());
        Assert.Equal(1, result.Profile.FeatureCount);
        Assert.Contains("kitchenDisplay", result.RemovedFeatures);
    }

    [Fact]
    public async Task Delete_CustomProfile_SoftDeletes()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);
        await catalog.CreateAsync(
            new UpsertVerticalProfileRequest { Id = "bakery", Name = "Bakery" },
            "profile-admin",
            Roles.SuperAdmin);

        var error = await catalog.DeleteAsync("bakery", "profile-admin", Roles.SuperAdmin);

        Assert.Null(error);
        Assert.Null(await catalog.ListTenantsAsync("bakery"));
        var row = await db.VerticalProfileOverrides.SingleAsync(item => item.ProfileId == "bakery");
        Assert.True(row.IsDeleted);
        var profile = await db.VerticalProfiles.SingleAsync(item => item.Id == "bakery");
        Assert.False(profile.IsActive);
    }

    [Fact]
    public async Task Delete_ProfileInUse_Returns409WithTenants()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var catalog = CreateCatalog(db, service);
        await catalog.CreateAsync(
            new UpsertVerticalProfileRequest { Id = "bakery", Name = "Bakery" },
            "profile-admin",
            Roles.SuperAdmin);
        var (assigned, assignError) = await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = "bakery",
                Overrides = Json("""{"posFeatures":{"tables":true}}"""),
            },
            "profile-admin",
            Roles.SuperAdmin);
        Assert.Null(assignError);
        Assert.NotNull(assigned);

        var controller = new AdminVerticalProfilesController(service, catalog);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "profile-admin"),
                        new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    ],
                    "test")),
            },
        };

        var result = await controller.Delete("bakery", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = JsonSerializer.Serialize(conflict.Value);
        Assert.Contains(VerticalProfileCatalogErrorCodes.ProfileInUse, body, StringComparison.Ordinal);
        Assert.Contains(tenantId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_SeedProfile_IsRefused()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);

        var error = await catalog.DeleteAsync(VerticalProfileIds.Vet, "profile-admin", Roles.SuperAdmin);

        Assert.NotNull(error);
        Assert.Equal(VerticalProfileCatalogErrorCodes.ProfileIsSeed, error!.Code);
        Assert.NotNull(await CreateService(db).GetActiveAsync(VerticalProfileIds.Vet));
    }

    [Fact]
    public async Task Patch_RejectsIdChange()
    {
        await using var db = await CreateDbAsync();
        var catalog = CreateCatalog(db);

        var (result, error) = await catalog.UpdateAsync(
            VerticalProfileIds.Gastronomy,
            new UpsertVerticalProfileRequest { Id = "not-gastronomy", Name = "Renamed" },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal(VerticalProfileCatalogErrorCodes.IdImmutable, error!.Code);
    }

    [Fact]
    public async Task Create_WritesVerticalProfileCreatedAudit()
    {
        await using var db = await CreateDbAsync();
        var audit = CreateAuditMock();
        var catalog = CreateCatalog(db, audit: audit.Object);

        var (result, error) = await catalog.CreateAsync(
            new UpsertVerticalProfileRequest { Id = "bakery", Name = "Bakery" },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        audit.Verify(
            logger => logger.LogSystemOperationAsync(
                "VERTICAL_PROFILE_CREATED",
                "VerticalProfile",
                "profile-admin",
                Roles.SuperAdmin,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.VerticalProfileCreated,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task FeatureEdit_InvalidatesCacheForAssignedTenants()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(db, cache: cache.Object);
        var catalog = CreateCatalog(db, service, cache: cache.Object);
        await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Gastronomy,
                Overrides = Json("{}"),
            },
            "profile-admin",
            Roles.SuperAdmin);
        cache.Invocations.Clear();

        var (result, error) = await catalog.UpdateFeaturesAsync(
            VerticalProfileIds.Gastronomy,
            new UpdateVerticalProfileFeaturesRequest
            {
                PosFeatures = Json("""{"kitchenDisplay":false}"""),
            },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Single(result!.AffectedTenants);
        Assert.Equal(tenantId, result.AffectedTenants[0].Id);
        cache.Verify(
            serviceCache => serviceCache.RemoveAsync(
                CacheKeys.VerticalProfileCatalog,
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        cache.Verify(
            serviceCache => serviceCache.RemoveAsync(
                CacheKeys.Format(CacheKeys.VerticalProfileEffective, tenantId),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task TenantsByProfile_GroupsAssignments()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        db.CompanySettings.Local.Single(row => row.TenantId == tenantId).VerticalProfileId =
            VerticalProfileIds.HairSalon;
        await db.SaveChangesAsync();
        var catalog = CreateCatalog(db);

        var groups = await catalog.GroupTenantsByProfileAsync();
        var hairSalon = Assert.Single(groups, group => group.ProfileId == VerticalProfileIds.HairSalon);
        Assert.Equal(1, hairSalon.TenantCount);
        Assert.Equal(tenantId, Assert.Single(hairSalon.Tenants).Id);

        var tenants = await catalog.ListTenantsAsync(VerticalProfileIds.HairSalon);
        Assert.NotNull(tenants);
        Assert.Equal(0, Assert.Single(tenants!).OverridesCount);
    }

    [Fact]
    public async Task ListTenants_CountsOverrideKeys()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var catalog = CreateCatalog(db, service);
        await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Gastronomy,
                Overrides = Json("""{"posFeatures":{"tables":true},"posLayout":"tables"}"""),
            },
            "profile-admin",
            Roles.SuperAdmin);

        var tenants = await catalog.ListTenantsAsync(VerticalProfileIds.Gastronomy);

        Assert.NotNull(tenants);
        Assert.Equal(2, Assert.Single(tenants!).OverridesCount);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VerticalProfileCatalog_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IVerticalProfileService CreateService(
        AppDbContext db,
        IVerticalProfileCatalogService? catalog = null,
        ICacheService? cache = null)
    {
        _ = catalog;
        cache ??= CreateCache();
        var tenantAccessor = Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("Tenant accessor was not registered for the test context."));
        return new VerticalProfileService(
            db,
            tenantAccessor,
            CreateAuditMock().Object,
            Mock.Of<ILogger<VerticalProfileService>>(),
            new VerticalProfileRegistry(db),
            cache);
    }

    private static VerticalProfileCatalogService CreateCatalog(
        AppDbContext db,
        IVerticalProfileService? service = null,
        IAuditLogService? audit = null,
        ICacheService? cache = null)
    {
        _ = service;
        return new VerticalProfileCatalogService(
            db,
            new VerticalProfileRegistry(db),
            audit ?? CreateAuditMock().Object,
            cache ?? CreateCache(),
            Mock.Of<ILogger<VerticalProfileCatalogService>>());
    }

    private static ICacheService CreateCache()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return cache.Object;
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(logger => logger.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
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
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Bakery Tenant",
            Slug = "bakery-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Bakery GmbH",
            CompanyAddress = "Wien 1",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            Currency = "EUR",
            Country = "AT",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
        });
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}

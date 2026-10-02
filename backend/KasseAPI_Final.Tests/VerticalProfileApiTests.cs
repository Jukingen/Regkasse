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

public sealed class VerticalProfileApiTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task ProfileList_ReturnsAllSeededActiveProfiles()
    {
        await using var db = await CreateDbAsync(Guid.NewGuid());
        var service = CreateService(db);
        var controller = new AdminVerticalProfilesController(service, CreateCatalog(db));

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsAssignableFrom<IReadOnlyList<VerticalProfileDto>>(ok.Value);
        Assert.Equal(9, profiles.Count);
        Assert.Equal(
            new[]
            {
                VerticalProfileIds.Beherbergung,
                VerticalProfileIds.Gastronomy,
                VerticalProfileIds.GastronomyTables,
                VerticalProfileIds.HairSalon,
                VerticalProfileIds.HandyShop,
                VerticalProfileIds.MobileServices,
                VerticalProfileIds.Taxi,
                VerticalProfileIds.TicketSales,
                VerticalProfileIds.Vet,
            },
            profiles.Select(profile => profile.Id).OrderBy(id => id));

        var permission = Assert.Single(
            typeof(AdminVerticalProfilesController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.SystemCritical, permission.Permission);
    }

    [Fact]
    public async Task TenantAssignment_PersistsAndReturnsEffectiveProfile()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenantAndSettings(db, tenantId);
        await db.SaveChangesAsync();

        var controller = CreateAdminTenantController(CreateService(db), "profile-admin");
        var put = await controller.Put(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.HairSalon,
                Overrides = Json("{}"),
            },
            CancellationToken.None);

        var putOk = Assert.IsType<OkObjectResult>(put.Result);
        var assigned = Assert.IsType<EffectiveVerticalProfileDto>(putOk.Value);
        Assert.Equal(VerticalProfileIds.HairSalon, assigned.ProfileId);

        db.ChangeTracker.Clear();
        var settings = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId);
        var overrideRow = await db.TenantVerticalOverrides.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId);
        Assert.Equal(VerticalProfileIds.HairSalon, settings.VerticalProfileId);
        Assert.Equal("{}", overrideRow.OverridesJson);

        var get = await controller.Get(tenantId, CancellationToken.None);
        var getOk = Assert.IsType<OkObjectResult>(get.Result);
        var returned = Assert.IsType<EffectiveVerticalProfileDto>(getOk.Value);
        Assert.Equal(VerticalProfileIds.HairSalon, returned.ProfileId);
        Assert.Equal(VerticalProfileLayouts.Appointment, returned.PosLayout);
    }

    [Fact]
    public async Task Overrides_DeepMergeFeaturesAndFieldLists()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenantAndSettings(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (profile, error) = await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Gastronomy,
                Overrides = Json(
                    """{"posFeatures":{"tables":true},"requiredFields":{"customer":["phone"]}}"""),
            },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(profile);
        Assert.True(profile!.PosFeatures.GetProperty("tables").GetBoolean());
        Assert.True(profile.PosFeatures.GetProperty("kitchenDisplay").GetBoolean());
        Assert.Equal(
            ["phone"],
            profile.RequiredFields.GetProperty("customer").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["name", "price", "taxGroup"],
            profile.RequiredFields.GetProperty("product").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task PosGet_CrossTenantSettings_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantA);
        SeedTenantAndSettings(db, tenantB);
        await db.SaveChangesAsync();

        var controller = new PosVerticalProfileController(CreateService(db));
        var result = await controller.Get(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task AmbientAdminGet_ReturnsCurrentTenantProfile()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenantAndSettings(db, tenantId);
        await db.SaveChangesAsync();

        var controller = new AdminCurrentVerticalProfileController(CreateService(db));
        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<EffectiveVerticalProfileDto>(ok.Value);
        Assert.Equal(VerticalProfileIds.Gastronomy, profile.ProfileId);
        Assert.False(profile.HasPosFeature("patientRecord"));
        Assert.False(profile.HasPosFeature("serviceDuration"));

        var permission = Assert.Single(
            typeof(AdminCurrentVerticalProfileController)
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.ProductView, permission.Permission);
    }

    [Fact]
    public async Task TenantAssignment_WritesVerticalProfileAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenantAndSettings(db, tenantId);
        await db.SaveChangesAsync();

        var audit = CreateAuditMock();
        var service = CreateService(db, audit.Object);
        var (profile, error) = await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.GastronomyTables,
                Overrides = Json("{}"),
            },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(profile);
        audit.Verify(
            logger => logger.LogSystemOperationAsync(
                "TENANT_VERTICAL_PROFILE_CHANGED",
                "Tenant",
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
                AuditEventType.TenantVerticalProfileChanged,
                tenantId,
                tenantId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task TenantAssignment_PersistsTaxiTariffOnCompanySettings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenantAndSettings(db, tenantId);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (profile, error) = await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Taxi,
                Overrides = Json("{}"),
                TaxiTariffPerKm = 2.40m,
            },
            "profile-admin",
            Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(profile);
        Assert.Equal(VerticalProfileIds.Taxi, profile!.ProfileId);
        Assert.Equal(VerticalProfileLayouts.Taxi, profile.PosLayout);
        Assert.Equal(2.40m, profile.TaxiTariffPerKm);

        db.ChangeTracker.Clear();
        var settings = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == tenantId);
        Assert.Equal(2.40m, settings.TaxiTariffPerKm);
        Assert.Equal(VerticalProfileIds.Taxi, settings.VerticalProfileId);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VerticalProfiles_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IVerticalProfileService CreateService(
        AppDbContext db,
        IAuditLogService? audit = null)
    {
        audit ??= CreateAuditMock().Object;
        var tenantAccessor = Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("Tenant accessor was not registered for the test context."));
        return new VerticalProfileService(
            db,
            tenantAccessor,
            audit,
            Mock.Of<ILogger<VerticalProfileService>>(),
            new VerticalProfileRegistry(db),
            CreateCache());
    }

    private static IVerticalProfileCatalogService CreateCatalog(AppDbContext db) =>
        new VerticalProfileCatalogService(
            db,
            new VerticalProfileRegistry(db),
            CreateAuditMock().Object,
            CreateCache(),
            Mock.Of<ILogger<VerticalProfileCatalogService>>());

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

    private static AdminTenantVerticalProfileController CreateAdminTenantController(
        IVerticalProfileService service,
        string actorId)
    {
        var controller = new AdminTenantVerticalProfileController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, actorId),
                        new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    ],
                    authenticationType: "test")),
            },
        };
        return controller;
    }

    private static void SeedTenantAndSettings(AppDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"tenant-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Vertical Demo GmbH",
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

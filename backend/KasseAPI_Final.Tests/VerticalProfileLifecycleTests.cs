using System.Security.Claims;
using System.Text.Json;
using System.Runtime.CompilerServices;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
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

public sealed class VerticalProfileLifecycleTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task VetToGastronomy_KeepsPetData_AndWarnsBeforeTheChange()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId, VerticalProfileIds.Vet);
        var customerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Anna",
            PetData = new CustomerPetData { PetName = "Bello", PetSpecies = "Hund" },
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Untersuchung",
            Price = 40m,
            Unit = "Stk",
            DurationMinutes = 30,
            StaffId = "staff-anna",
        });
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Praxis",
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        });
        var paymentId = Guid.NewGuid();
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = customerId,
            CustomerName = "Anna",
            CashierId = "cashier",
            CashRegisterId = registerId,
            TotalAmount = 40m,
            ReceiptNumber = "AT-LIFE-1",
            TseSignature = "sig",
            PrescriptionReference = "RX-1",
            RouteFrom = "Alt",
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var controller = CreateController(service);
        var impact = await controller.GetImpact(tenantId, VerticalProfileIds.Gastronomy, CancellationToken.None);
        var impactOk = Assert.IsType<OkObjectResult>(impact.Result);
        var body = Assert.IsType<TenantProfileImpactDto>(impactOk.Value);
        Assert.Equal(1, body.Counts.CustomersWithPetData);
        Assert.Equal(1, body.Counts.PaymentsWithPrescriptionReference);
        Assert.Contains(body.Warnings, warning => warning.Code == TenantProfileImpactCodes.CustomersWithPetData);
        Assert.Contains(body.Warnings, warning => warning.Code == TenantProfileImpactCodes.PaymentsWithPrescriptionReference);

        var put = await controller.Put(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Gastronomy,
                Overrides = Json("{}"),
            },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(put.Result);

        db.ChangeTracker.Clear();
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(row => row.Id == customerId);
        Assert.Equal("Bello", customer.PetData!.PetName);
        var product = await db.Products.IgnoreQueryFilters().SingleAsync(row => row.TenantId == tenantId);
        Assert.Equal(30, product.DurationMinutes);
        Assert.Equal("staff-anna", product.StaffId);
        var payment = await db.PaymentDetails.IgnoreQueryFilters().SingleAsync(row => row.Id == paymentId);
        Assert.Equal("RX-1", payment.PrescriptionReference);
        Assert.Equal("Alt", payment.RouteFrom);
        Assert.Equal("AT-LIFE-1", payment.ReceiptNumber);
        var effective = await service.GetForAdminTenantAsync(tenantId);
        Assert.False(effective!.HasPosFeature("patientRecord"));
    }

    [Fact]
    public async Task GastronomyToVet_AllowsNewPetData()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId, VerticalProfileIds.Gastronomy);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (profile, error) = await service.UpdateTenantAsync(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Vet,
                Overrides = Json("{}"),
            },
            "profile-admin",
            Roles.SuperAdmin);
        Assert.Null(error);
        Assert.True(profile!.HasPosFeature("patientRecord"));

        var controller = new PosCustomerController(
            Mock.Of<IPosCustomerQrLookupService>(),
            db,
            Accessors.GetValue(db, _ => throw new InvalidOperationException("missing accessor")),
            Mock.Of<ILogger<PosCustomerController>>(),
            new VerticalProfileGuard(service, Mock.Of<ILogger<VerticalProfileGuard>>()));
        var created = await controller.CreateCustomer(
            new CreatePosCustomerRequest
            {
                Name = "Anna",
                PetData = new PosCustomerPetDataDto { PetName = "Minka", PetSpecies = "Katze" },
            },
            CancellationToken.None);

        Assert.IsType<CreatedResult>(created.Result);
        var stored = await db.Customers.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Minka", stored.PetData!.PetName);
    }

    [Fact]
    public async Task HistoricalPayment_WarnsAndStillAllowsTheProfileChange()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId, VerticalProfileIds.Vet);
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Praxis",
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Anna",
            CashierId = "cashier",
            CashRegisterId = registerId,
            TotalAmount = 12m,
            ReceiptNumber = "AT-LIFE-2",
            TseSignature = "sig",
            PrescriptionReference = "RX-KEEP",
        });
        await db.SaveChangesAsync();

        var controller = CreateController(CreateService(db));
        var impact = await controller.GetImpact(tenantId, VerticalProfileIds.Gastronomy, CancellationToken.None);
        var body = Assert.IsType<TenantProfileImpactDto>(Assert.IsType<OkObjectResult>(impact.Result).Value);
        Assert.Contains(body.Warnings, warning =>
            warning.Code == TenantProfileImpactCodes.PaymentsWithPrescriptionReference && warning.Count == 1);

        var put = await controller.Put(
            tenantId,
            new UpdateTenantVerticalProfileRequest
            {
                ProfileId = VerticalProfileIds.Gastronomy,
                Overrides = Json("{}"),
            },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(put.Result);
        var payment = await db.PaymentDetails.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("RX-KEEP", payment.PrescriptionReference);
        Assert.Equal("AT-LIFE-2", payment.ReceiptNumber);
    }

    [Fact]
    public async Task ProfileImpact_UnknownTenant_Returns404_AndIgnoresOtherTenants()
    {
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId, VerticalProfileIds.Vet);
        SeedTenant(db, otherId, VerticalProfileIds.Vet);
        db.Customers.Add(new Customer
        {
            TenantId = otherId,
            Name = "Other",
            PetData = new CustomerPetData { PetName = "Rex" },
        });
        await db.SaveChangesAsync();

        var controller = CreateController(CreateService(db));
        var missing = await controller.GetImpact(Guid.NewGuid(), VerticalProfileIds.Gastronomy, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(missing.Result);

        var impact = await controller.GetImpact(tenantId, VerticalProfileIds.Gastronomy, CancellationToken.None);
        var body = Assert.IsType<TenantProfileImpactDto>(Assert.IsType<OkObjectResult>(impact.Result).Value);
        Assert.Equal(0, body.Counts.CustomersWithPetData);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VerticalProfileLifecycle_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static IVerticalProfileService CreateService(AppDbContext db)
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
        return new VerticalProfileService(
            db,
            Accessors.GetValue(db, _ => throw new InvalidOperationException("missing accessor")),
            audit.Object,
            Mock.Of<ILogger<VerticalProfileService>>(),
            new VerticalProfileRegistry(db),
            CreateCache());
    }

    private static ICacheService CreateCache()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return cache.Object;
    }

    private static AdminTenantVerticalProfileController CreateController(IVerticalProfileService service)
    {
        var controller = new AdminTenantVerticalProfileController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "profile-admin"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        ],
                        authenticationType: "test")),
                },
            },
        };
        return controller;
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId, string profileId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"t-{tenantId:N}"[..12],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            VerticalProfileId = profileId,
            CompanyName = "Lifecycle GmbH",
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

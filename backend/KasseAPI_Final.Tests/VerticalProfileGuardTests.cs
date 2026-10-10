using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Appointments;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class VerticalProfileGuardTests
{
    [Fact]
    public async Task VetTenant_PetData_IsAllowed()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Vet", Slug = "vet-" + tenantId.ToString("N")[..8] });
        await db.SaveChangesAsync();

        var controller = CustomerController(db, tenantId, Guard(VerticalProfileIds.Vet, "patientRecord"));
        var result = await controller.CreateCustomer(
            new CreatePosCustomerRequest
            {
                Name = "Anna",
                PetData = new PosCustomerPetDataDto { PetName = "Bello", PetSpecies = "Hund" },
            },
            CancellationToken.None);

        Assert.IsType<CreatedResult>(result.Result);
    }

    [Fact]
    public async Task GastronomyTenant_PetData_ReturnsProfileFeatureDisabled()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Gastro", Slug = "gas-" + tenantId.ToString("N")[..8] });
        await db.SaveChangesAsync();

        var controller = CustomerController(db, tenantId, Guard(VerticalProfileIds.Gastronomy, "kitchenDisplay"));
        var result = await controller.CreateCustomer(
            new CreatePosCustomerRequest
            {
                Name = "Anna",
                PetData = new PosCustomerPetDataDto { PetName = "Bello" },
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
        var body = Assert.IsAssignableFrom<object>(badRequest.Value!);
        var code = body.GetType().GetProperty("code")!.GetValue(body);
        var feature = body.GetType().GetProperty("feature")!.GetValue(body);
        Assert.Equal(FeatureNotEnabledForProfileException.Code, code);
        Assert.Equal("patientRecord", feature);
        Assert.Empty(db.Customers.IgnoreQueryFilters());
    }

    [Fact]
    public async Task TaxiTenant_RouteFrom_IsAllowed()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                VerticalProfiles = ProfileService(VerticalProfileIds.Taxi, VerticalProfileLayouts.Taxi),
            });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.RouteFrom = "Hauptbahnhof";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task NonTaxiTenant_RouteFrom_ReturnsProfileFeatureDisabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                VerticalProfiles = ProfileService(VerticalProfileIds.Gastronomy, VerticalProfileLayouts.Standard, "kitchenDisplay"),
            });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.RouteFrom = "Hauptbahnhof";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(FeatureNotEnabledForProfileException.Code, result.DiagnosticCode);
        Assert.Equal(VerticalProfileGuard.TaxiFeature, result.ProfileFeature);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task NonAppointmentTenant_PostAppointments_Returns403()
    {
        var controller = new PosAppointmentsController(
            Mock.Of<IAppointmentService>(),
            TenantTestDoubles.TenantAccessorReturning(Guid.NewGuid()),
            Guard(VerticalProfileIds.Gastronomy, "kitchenDisplay"));

        var result = await controller.Create(
            new CreatePosAppointmentRequest { StartUtc = DateTime.UtcNow, CustomerName = "Anna" },
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var body = forbidden.Value!;
        Assert.Equal(ProfileEndpointDisabledException.Code, body.GetType().GetProperty("code")!.GetValue(body));
        Assert.Equal("appointment", body.GetType().GetProperty("feature")!.GetValue(body));
    }

    [Theory]
    [InlineData(VerticalProfileIds.Gastronomy)]
    [InlineData(VerticalProfileIds.Vet)]
    [InlineData(VerticalProfileIds.Taxi)]
    public async Task PaymentWithoutProfileFields_Succeeds(string profileId)
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var layout = profileId == VerticalProfileIds.Taxi
            ? VerticalProfileLayouts.Taxi
            : VerticalProfileLayouts.Standard;
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                VerticalProfiles = ProfileService(profileId, layout),
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Null(result.DiagnosticCode);
    }

    [Fact]
    public void TagesabschlussAndFiscalControllers_AreNotProfileGated()
    {
        foreach (var type in new[]
                 {
                     typeof(TagesabschlussController),
                     typeof(RksvSpecialReceiptsController),
                     typeof(ReceiptsController),
                     typeof(PosReceiptsController),
                     typeof(PaymentController),
                 })
        {
            var dependsOnGuard = type
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(IVerticalProfileGuard));
            Assert.False(dependsOnGuard, type.Name);
        }
    }

    private static IVerticalProfileGuard Guard(string profileId, params string[] features) =>
        new VerticalProfileGuard(
            ProfileService(profileId, VerticalProfileLayouts.Standard, features),
            Mock.Of<ILogger<VerticalProfileGuard>>());

    private static IVerticalProfileService ProfileService(string profileId, string layout, params string[] features)
    {
        var enabled = features.ToDictionary(feature => feature, _ => true);
        var profile = new EffectiveVerticalProfileDto(
            profileId,
            profileId,
            JsonSerializer.SerializeToElement(enabled),
            JsonDocument.Parse("{}").RootElement.Clone(),
            JsonDocument.Parse("{}").RootElement.Clone(),
            layout,
            JsonDocument.Parse("{}").RootElement.Clone());
        var mock = new Mock<IVerticalProfileService>();
        mock.Setup(service => service.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        return mock.Object;
    }

    private static PosCustomerController CustomerController(AppDbContext db, Guid tenantId, IVerticalProfileGuard guard) =>
        new(
            Mock.Of<IPosCustomerQrLookupService>(),
            db,
            TenantTestDoubles.TenantAccessorReturning(tenantId),
            Mock.Of<ILogger<PosCustomerController>>(),
            guard);

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VerticalProfileGuard_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }
}

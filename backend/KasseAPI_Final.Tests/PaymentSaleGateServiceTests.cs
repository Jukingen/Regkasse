using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentSaleGateServiceTests
{
    [Fact]
    public async Task ValidatePreconditions_RejectsPrescription_WhenPatientRecordDisabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = CreateGate(ctx, features: new Dictionary<string, bool> { ["patientRecord"] = false });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.PrescriptionReference = " RX-NOPE ";

        var result = await sut.ValidatePreconditionsAsync(request, CancellationToken.None);

        Assert.Equal("PROFILE_FEATURE_DISABLED", result.Stop?.DiagnosticCode);
        Assert.Equal("patientRecord", result.Stop?.ProfileFeature);
        Assert.True(result.Stop?.IsDeterministicFailure);
        Assert.Null(result.Value);
        Assert.Null(ctx.Database.CurrentTransaction);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task ValidatePreconditions_RejectsTaxiFields_WhenProfileIsNotTaxi()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = CreateGate(ctx, profileId: VerticalProfileIds.Gastronomy);

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.RouteFrom = "A";
        request.RouteTo = "B";

        var result = await sut.ValidatePreconditionsAsync(request, CancellationToken.None);

        Assert.Equal("PROFILE_FEATURE_DISABLED", result.Stop?.DiagnosticCode);
        Assert.Equal(VerticalProfileGuard.TaxiFeature, result.Stop?.ProfileFeature);
        Assert.Null(ctx.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ValidatePreconditions_RejectsImei_WhenTrackingDisabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = CreateGate(ctx, features: new Dictionary<string, bool> { ["imeiTracking"] = false });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Items![0].Imei = "12345678";

        var result = await sut.ValidatePreconditionsAsync(request, CancellationToken.None);

        Assert.Equal("PROFILE_FEATURE_DISABLED", result.Stop?.DiagnosticCode);
        Assert.Equal("imeiTracking", result.Stop?.ProfileFeature);
    }

    [Fact]
    public async Task ValidatePreconditions_RejectsTicketProduct_WhenTicketScanDisabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var product = await ctx.Products.SingleAsync(item => item.Id == productId);
        product.IsTicket = true;
        await ctx.SaveChangesAsync();
        var sut = CreateGate(ctx, features: new Dictionary<string, bool> { ["ticketScan"] = false });

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        var result = await sut.ValidatePreconditionsAsync(request, CancellationToken.None);

        Assert.Equal("PROFILE_FEATURE_DISABLED", result.Stop?.DiagnosticCode);
        Assert.Equal("ticketScan", result.Stop?.ProfileFeature);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsInvalidTaxNumber()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = CreateGate(ctx, companyTaxNumber: "NOT-A-UID");
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        request.Steuernummer = "NOT-A-UID";

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.False(result.Stop?.Success);
        Assert.Equal("Invalid Austrian tax number format", result.Stop?.Message);
        Assert.Null(ctx.Database.CurrentTransaction);
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsEmptyCashRegister()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = CreateGate(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal(CashRegisterResolutionCodes.Required, result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
        Assert.Null(ctx.Database.CurrentTransaction);
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsClosedRegister()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var registerId = Guid.NewGuid();
        var sut = CreateGate(
            ctx,
            register: CashRegisterResolutionValidationResult.Failure("REGISTER_CLOSED", "Register is closed."));
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), registerId);

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal("REGISTER_CLOSED", result.Stop?.DiagnosticCode);
        Assert.Equal("Register is closed.", result.Stop?.Message);
    }

    [Fact]
    public async Task PrepareFiscalSale_MapsDecommissionedRegisterToRksvCode()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var sut = CreateGate(
            ctx,
            register: CashRegisterResolutionValidationResult.Failure(
                CashRegisterResolutionCodes.Decommissioned,
                "Register is decommissioned."));
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal(RksvGuardErrorCodes.RegisterDecommissioned, result.Stop?.DiagnosticCode);
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsVoucherMethodWithoutPayload()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var registerId = Guid.NewGuid();
        var sut = CreateGate(ctx, register: CashRegisterResolutionValidationResult.Success(registerId, "K1"));
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), registerId, method: "voucher");

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal(RksvGuardErrorCodes.VoucherCodeRequired, result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsWhenTseIsOfflineAndQueueIsDisabled()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var registerId = Guid.NewGuid();
        var sut = CreateGate(
            ctx,
            register: CashRegisterResolutionValidationResult.Success(registerId, "K1"),
            tse: new TseOptions { TseMode = "Device", OfflineModeEnabled = false },
            health: TseOperationalHealth.Offline);
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), registerId);

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal("TSE_HEALTH_OFFLINE", result.Stop?.DiagnosticCode);
        Assert.Equal("TSE temporarily unavailable", result.Stop?.Message);
        Assert.Null(ctx.Database.CurrentTransaction);
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsVoucherWhileTseOfflineQueueIsOpen()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var registerId = Guid.NewGuid();
        var sut = CreateGate(
            ctx,
            register: CashRegisterResolutionValidationResult.Success(registerId, "K1"),
            tse: new TseOptions { TseMode = "Device", OfflineModeEnabled = true },
            health: TseOperationalHealth.Offline);
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), registerId, method: "voucher");
        request.Payment.VoucherCode = "GUT-1";

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal("TSE_OFFLINE_VOUCHER_BLOCKED", result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
    }

    [Fact]
    public async Task PrepareFiscalSale_RejectsUnsupportedMethodWhileTseOffline()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var registerId = Guid.NewGuid();
        var sut = CreateGate(
            ctx,
            register: CashRegisterResolutionValidationResult.Success(registerId, "K1"),
            tse: new TseOptions { TseMode = "Device", OfflineModeEnabled = true },
            health: TseOperationalHealth.Offline);
        var request = PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), registerId, method: "bank");

        var result = await sut.PrepareFiscalSaleAsync(request, "cashier", new Customer { Id = request.CustomerId, Name = "Gast" });

        Assert.Equal("TSE_OFFLINE_METHOD_UNSUPPORTED", result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
    }

    private static PaymentSaleGateService CreateGate(
        AppDbContext ctx,
        string? profileId = null,
        IReadOnlyDictionary<string, bool>? features = null,
        string companyTaxNumber = "ATU12345678",
        CashRegisterResolutionValidationResult? register = null,
        TseOptions? tse = null,
        TseOperationalHealth health = TseOperationalHealth.Online)
    {
        var profiles = new Mock<IVerticalProfileService>();
        profiles.Setup(service => service.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(profileId ?? VerticalProfileIds.Gastronomy, features));

        var countries = new CountryStrategyContext(ctx, new CountryProfileRegistry(), TenantTestDoubles.PrimaryTenantResolver);
        var tax = new Mock<ITaxStrategyResolver>();
        tax.Setup(resolver => resolver.Resolve(It.IsAny<CountryProfile>(), It.IsAny<VatRegime>()))
            .Returns(new AustriaTaxStrategy());

        var registers = new Mock<ICashRegisterResolutionService>();
        registers.Setup(service => service.ValidatePaymentRegisterAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(register ?? CashRegisterResolutionValidationResult.Success(Guid.NewGuid(), "K1"));

        var http = new Mock<IHttpContextAccessor>();
        http.Setup(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        return new PaymentSaleGateService(
            ctx,
            NullLogger<PaymentService>.Instance,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions { TaxNumber = companyTaxNumber }),
            countries,
            tax.Object,
            registers.Object,
            http.Object,
            Mock.Of<IPaymentOfflineIntentService>(),
            tse ?? new TseOptions { TseMode = "Demo" },
            new FixedTseHealth(health),
            verticalProfiles: profiles.Object);
    }

    private static EffectiveVerticalProfileDto Profile(string profileId, IReadOnlyDictionary<string, bool>? features)
    {
        var flags = features ?? new Dictionary<string, bool>();
        return new EffectiveVerticalProfileDto(
            profileId,
            "test",
            JsonSerializer.SerializeToElement(flags),
            JsonDocument.Parse("{}").RootElement.Clone(),
            JsonDocument.Parse("{}").RootElement.Clone(),
            "standard",
            JsonDocument.Parse("{}").RootElement.Clone());
    }

    private sealed class FixedTseHealth : ITseHealthMonitor
    {
        public FixedTseHealth(TseOperationalHealth status) =>
            Snapshot = new TseHealthSnapshot { Status = status };

        public TseHealthSnapshot Snapshot { get; }

        public event EventHandler<TseHealthChangedEventArgs>? StatusChanged
        {
            add { }
            remove { }
        }
    }
}

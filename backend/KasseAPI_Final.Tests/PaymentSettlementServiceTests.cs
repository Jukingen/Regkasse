using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Services.Preorder;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentSettlementServiceTests
{
    [Fact]
    public async Task Resolve_CashSale_DoesNotOpenTransactionOrWritePayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (request, gates, sale) = Sale();
        var sut = Create(
            new PaymentMethodCatalogService(ctx, TenantTestDoubles.PrimaryTenantResolver),
            new PaymentVoucherLedgerService(ctx, NullLogger<PaymentService>.Instance));

        var result = await sut.ResolveSettlementAsync(
            request, gates, sale, 10m, gates.TenantIdForFlags, request.CashRegisterId, null, CancellationToken.None);

        Assert.Null(result.Stop);
        Assert.Equal("0", result.Value!.MethodResolution.LegacyRaw);
        Assert.False(result.Value.TimeSyncWarning);
        Assert.Null(result.Value.VoucherRedeemLines);
        Assert.Null(ctx.Database.CurrentTransaction);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task Resolve_RejectsPreorderBalance_WhenServiceMissing()
    {
        var (request, gates, sale) = Sale();
        request.PreorderBalanceOrderId = Guid.NewGuid();
        var sut = Create();

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("PREORDER_UNAVAILABLE", result.Stop?.DiagnosticCode);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Resolve_RejectsCombinedPreorderCreateAndBalance()
    {
        var (request, gates, sale) = Sale();
        request.IsPreorder = true;
        request.PreorderBalanceOrderId = Guid.NewGuid();
        var preorder = new Mock<IPreorderService>();
        var sut = Create(preorder: preorder.Object);

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("PREORDER_BALANCE_EXCLUSIVE", result.Stop?.DiagnosticCode);
        preorder.Verify(
            service => service.ValidateBalancePaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Resolve_RejectsFailedBalanceGuard()
    {
        var (request, gates, sale) = Sale();
        var orderId = Guid.NewGuid();
        request.PreorderBalanceOrderId = orderId;
        var preorder = new Mock<IPreorderService>();
        preorder.Setup(service => service.ValidateBalancePaymentAsync(orderId, 10m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreorderBalanceGuardResult.Fail("PREORDER_CLOSED", "Pre-order is closed."));
        var sut = Create(preorder: preorder.Object);

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("PREORDER_CLOSED", result.Stop?.DiagnosticCode);
        Assert.Equal("Pre-order is closed.", result.Stop?.Message);
    }

    [Fact]
    public async Task Resolve_RejectsTenantLimit()
    {
        var tenantId = Guid.NewGuid();
        var (request, gates, sale) = Sale(tenantId: tenantId);
        var limits = new Mock<ITenantLimitGuard>();
        limits.Setup(guard => guard.EnsureSaleWithinLimitsAsync(tenantId, 10m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LimitExceededException("sales.dailyAmount", 100m, 110m, "Daily sales cap reached."));
        var sut = Create(limits: limits.Object);

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal(LimitExceededException.ErrorCodeValue, result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
        Assert.NotNull(result.Stop?.LimitError);
    }

    [Fact]
    public async Task Resolve_SkipsTenantLimit_OnOfflineReplay()
    {
        var (request, gates, sale) = Sale();
        var limits = new Mock<ITenantLimitGuard>(MockBehavior.Strict);
        var sut = Create(limits: limits.Object);

        var result = await Resolve(sut, request, gates, sale, offlineTransactionId: Guid.NewGuid());

        Assert.Null(result.Stop);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task Resolve_RejectsDisabledPaymentMethod()
    {
        var (request, gates, sale) = Sale();
        var catalog = new Mock<IPaymentMethodCatalogService>();
        catalog.Setup(service => service.ResolveForPaymentAsync("cash", request.CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethodResolutionResult(false, "0", "This payment method is disabled.", true));
        var sut = Create(catalog: catalog.Object);

        var result = await Resolve(sut, request, gates, sale);

        Assert.False(result.Stop?.Success);
        Assert.Contains("disabled", result.Stop?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_RejectsVoucherLegacyMismatch()
    {
        var (request, gates, sale) = Sale(method: "voucher", voucherLikely: true);
        var catalog = CatalogReturning(request.CashRegisterId, "0", matchedDefinition: true, method: "voucher");
        var ledger = Ledger();
        var sut = Create(catalog: catalog.Object, vouchers: ledger.Object);

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("VOUCHER_LEGACY_MISMATCH", result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
        ledger.Verify(
            service => service.BuildVoucherRedemptionPlanAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<PaymentMethodRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Resolve_RejectsMixedVoucherWithoutSettlementAmount()
    {
        var (request, gates, sale) = Sale(hasVoucherPayload: true);
        var sut = Create();

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("VOUCHER_MIXED_SETTLEMENT_AMOUNT_REQUIRED", result.Stop?.DiagnosticCode);
    }

    [Fact]
    public async Task Resolve_MixedVoucher_ValidatesCardAgainstRemainderAndPlansVoucher()
    {
        var (request, gates, sale) = Sale(hasVoucherPayload: true);
        request.Payment.Amount = 4m;
        var intentId = Guid.NewGuid();
        request.Payment.CardPaymentIntentId = intentId;
        var catalog = CatalogReturning(request.CashRegisterId, "0", matchedDefinition: false);
        var ledger = Ledger();
        ledger.Setup(service => service.BuildVoucherRedemptionPlanAsync(
                gates.TenantIdForFlags,
                request.CashRegisterId,
                6m,
                request.Payment,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new List<VoucherRedeemLine>()));
        var card = new Mock<ICardPaymentService>();
        card.Setup(service => service.ValidateForFiscalPaymentAsync(intentId, 4m, request.CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new CardPaymentTransaction { Id = intentId }, (string?)null, (string?)null));
        var sut = Create(catalog: catalog.Object, vouchers: ledger.Object, card: card.Object);

        var result = await Resolve(sut, request, gates, sale, tenantId: gates.TenantIdForFlags);

        Assert.Null(result.Stop);
        Assert.Equal(intentId, result.Value!.ValidatedCardTransaction!.Id);
        Assert.NotNull(result.Value.VoucherRedeemLines);
        ledger.Verify(
            service => service.ApplyVoucherRedemptionsInCurrentTransactionAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<PaymentDetails>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<VoucherRedeemLine>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        card.Verify(
            service => service.ValidateForFiscalPaymentAsync(intentId, 10m, request.CashRegisterId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Resolve_RejectsCardIntent()
    {
        var (request, gates, sale) = Sale(method: "card");
        var intentId = Guid.NewGuid();
        request.Payment.CardPaymentIntentId = intentId;
        var catalog = CatalogReturning(request.CashRegisterId, "1", matchedDefinition: true, method: "card");
        var card = new Mock<ICardPaymentService>();
        card.Setup(service => service.ValidateForFiscalPaymentAsync(intentId, 10m, request.CashRegisterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (CardPaymentTransaction?)null, "CARD_INTENT_INVALID", "Card intent was not captured."));
        var sut = Create(catalog: catalog.Object, card: card.Object);

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("CARD_INTENT_INVALID", result.Stop?.DiagnosticCode);
        Assert.True(result.Stop?.IsDeterministicFailure);
    }

    [Fact]
    public async Task Resolve_RejectsNtpDrift()
    {
        var (request, gates, sale) = Sale();
        var sut = Create(ntp: EnabledNtp(), clock: DriftClock());

        var result = await Resolve(sut, request, gates, sale);

        Assert.Equal("NTP_TIME_SYNC", result.Stop?.DiagnosticCode);
        Assert.Equal("Systemzeit nicht synchronisiert", result.Stop?.Message);
    }

    [Fact]
    public async Task Resolve_AcceptsOfflineReplayWithNtpWarning()
    {
        var (request, gates, sale) = Sale();
        var offlineId = Guid.NewGuid();
        var sut = Create(ntp: EnabledNtp(), clock: DriftClock());

        var result = await Resolve(sut, request, gates, sale, offlineTransactionId: offlineId);

        Assert.Null(result.Stop);
        Assert.True(result.Value!.TimeSyncWarning);
        Assert.True(result.Value.EffectiveTseRequired);
    }

    private static Task<SettlementPhase<SaleSettlement>> Resolve(
        PaymentSettlementService sut,
        CreatePaymentRequest request,
        PaymentPreconditions gates,
        PreparedFiscalSale sale,
        Guid? offlineTransactionId = null,
        Guid? tenantId = null) =>
        sut.ResolveSettlementAsync(
            request,
            gates,
            sale,
            10m,
            tenantId ?? gates.TenantIdForFlags,
            request.CashRegisterId,
            offlineTransactionId,
            CancellationToken.None);

    private static (CreatePaymentRequest Request, PaymentPreconditions Gates, PreparedFiscalSale Sale) Sale(
        string method = "cash",
        bool voucherLikely = false,
        bool hasVoucherPayload = false,
        Guid? tenantId = null)
    {
        var customerId = Guid.NewGuid();
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, Guid.NewGuid(), Guid.NewGuid(), method: method);
        var gates = new PaymentPreconditions { TenantIdForFlags = tenantId ?? Guid.NewGuid() };
        var profile = new CountryProfileRegistry().Get(CountryProfileCodes.Austria);
        var sale = new PreparedFiscalSale
        {
            Customer = new Customer { Id = customerId, Name = "Gast" },
            CompanyProfile = new CompanyProfileOptions(),
            CountryBinding = new CountryStrategyBinding(
                new CompanySettings(),
                profile,
                VatRegime.AT_RKSV_STANDARD,
                false),
            TaxStrategy = Mock.Of<ITaxStrategy>(),
            EffectiveSteuernummer = "ATU12345678",
            Principal = new ClaimsPrincipal(),
            VoucherLikelyFromClientMethod = voucherLikely,
            HasVoucherPayload = hasVoucherPayload,
            EffectiveTseRequired = true,
        };
        return (request, gates, sale);
    }

    private static PaymentSettlementService Create(
        IPaymentMethodCatalogService? catalog = null,
        IPaymentVoucherLedgerService? vouchers = null,
        ICardPaymentService? card = null,
        IPreorderService? preorder = null,
        ITenantLimitGuard? limits = null,
        INtpEffectiveSettingsProvider? ntp = null,
        INtpTimeSyncStatus? clock = null)
    {
        catalog ??= CatalogReturning(Guid.Empty, "0", matchedDefinition: false).Object;
        vouchers ??= Ledger().Object;
        ntp ??= DisabledNtp();
        clock ??= new Mock<INtpTimeSyncStatus>().Object;
        return new PaymentSettlementService(
            NullLogger<PaymentService>.Instance,
            catalog,
            vouchers,
            new TseOptions { TseMode = "Device" },
            ntp,
            clock,
            card,
            preorder,
            limits);
    }

    private static Mock<IPaymentMethodCatalogService> CatalogReturning(
        Guid registerId,
        string legacyRaw,
        bool matchedDefinition,
        string method = "cash")
    {
        var catalog = new Mock<IPaymentMethodCatalogService>();
        catalog.Setup(service => service.ResolveForPaymentAsync(method, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethodResolutionResult(true, legacyRaw, null, matchedDefinition));
        catalog.Setup(service => service.ResolveForPaymentAsync(method, registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethodResolutionResult(true, legacyRaw, null, matchedDefinition));
        return catalog;
    }

    private static Mock<IPaymentVoucherLedgerService> Ledger()
    {
        var ledger = new Mock<IPaymentVoucherLedgerService>();
        ledger.Setup(service => service.IsVoucherLegacyPayment(It.IsAny<string>()))
            .Returns((string raw) => string.Equals(raw, "4", StringComparison.Ordinal));
        return ledger;
    }

    private static INtpEffectiveSettingsProvider DisabledNtp()
    {
        var ntp = new Mock<INtpEffectiveSettingsProvider>();
        ntp.Setup(service => service.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NtpSettings { Enabled = false });
        return ntp.Object;
    }

    private static INtpEffectiveSettingsProvider EnabledNtp()
    {
        var ntp = new Mock<INtpEffectiveSettingsProvider>();
        ntp.Setup(service => service.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NtpSettings { Enabled = true });
        return ntp.Object;
    }

    private static INtpTimeSyncStatus DriftClock()
    {
        var clock = new Mock<INtpTimeSyncStatus>();
        var message = "Systemzeit nicht synchronisiert";
        clock.Setup(service => service.ShouldAllowOnlineFiscalPayment(It.IsAny<NtpSettings>(), out message))
            .Returns(false);
        return clock.Object;
    }
}

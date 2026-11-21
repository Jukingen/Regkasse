using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.Imei;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Profile, tax-identity, cash-register, and TSE checks that run before the fiscal transaction.
/// Does not begin a transaction, commit, or write a fiscal document.
/// </summary>
public interface IPaymentSaleGateService
{
    Task<SaleGatePhase<PaymentPreconditions>> ValidatePreconditionsAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken);

    Task<SaleGatePhase<PreparedFiscalSale>> PrepareFiscalSaleAsync(
        CreatePaymentRequest request,
        string userId,
        Customer customer);
}

/// <summary>Stop-or-continue result for a pre-transaction sale gate. No rollback flag: nothing is open yet.</summary>
public sealed class SaleGatePhase<T>
{
    public PaymentResult? Stop { get; init; }
    public T? Value { get; init; }

    public static SaleGatePhase<T> Ok(T value) => new() { Value = value };

    public static SaleGatePhase<T> Fail(PaymentResult error) => new() { Stop = error };
}

public sealed class PaymentPreconditions
{
    public Guid TenantIdForFlags { get; init; }
    public string? PrescriptionReference { get; init; }
    public string? RouteFrom { get; init; }
    public string? RouteTo { get; init; }
    public decimal? RouteKm { get; init; }
    public DateTime? TripStartedAtUtc { get; init; }
}

public sealed class PreparedFiscalSale
{
    public required Customer Customer { get; init; }
    public required CompanyProfileOptions CompanyProfile { get; init; }
    public required CountryStrategyBinding CountryBinding { get; init; }
    public required ITaxStrategy TaxStrategy { get; init; }
    public required string EffectiveSteuernummer { get; init; }
    public required ClaimsPrincipal Principal { get; init; }
    public required bool VoucherLikelyFromClientMethod { get; init; }
    public required bool HasVoucherPayload { get; init; }
    public required bool EffectiveTseRequired { get; init; }
}

public sealed class PaymentSaleGateService : IPaymentSaleGateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ICompanyProfileProvider _companyProfileProvider;
    private readonly ICountryStrategyContext _countryStrategyContext;
    private readonly ITaxStrategyResolver _taxStrategyResolver;
    private readonly ICashRegisterResolutionService _cashRegisterResolution;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPaymentOfflineIntentService _offlineIntents;
    private readonly TseOptions _tseOptions;
    private readonly ITseHealthMonitor _tseHealthMonitor;
    private readonly IFeatureFlagService? _featureFlags;
    private readonly IDevelopmentModeService? _developmentModeService;
    private readonly IHostEnvironment? _hostEnvironment;
    private readonly IOptionsMonitor<DevelopmentOptions>? _developmentOptions;
    private readonly IVerticalProfileService? _verticalProfiles;
    private readonly IVerticalProfileGuard? _profileGuard;

    public PaymentSaleGateService(
        AppDbContext context,
        ILogger<PaymentService> logger,
        ISettingsTenantResolver settingsTenantResolver,
        ICompanyProfileProvider companyProfileProvider,
        ICountryStrategyContext countryStrategyContext,
        ITaxStrategyResolver taxStrategyResolver,
        ICashRegisterResolutionService cashRegisterResolution,
        IHttpContextAccessor httpContextAccessor,
        IPaymentOfflineIntentService offlineIntents,
        TseOptions tseOptions,
        ITseHealthMonitor tseHealthMonitor,
        IFeatureFlagService? featureFlags = null,
        IDevelopmentModeService? developmentModeService = null,
        IHostEnvironment? hostEnvironment = null,
        IOptionsMonitor<DevelopmentOptions>? developmentOptions = null,
        IVerticalProfileService? verticalProfiles = null,
        IVerticalProfileGuard? profileGuard = null)
    {
        _context = context;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
        _companyProfileProvider = companyProfileProvider;
        _countryStrategyContext = countryStrategyContext;
        _taxStrategyResolver = taxStrategyResolver;
        _cashRegisterResolution = cashRegisterResolution;
        _httpContextAccessor = httpContextAccessor;
        _offlineIntents = offlineIntents;
        _tseOptions = tseOptions;
        _tseHealthMonitor = tseHealthMonitor;
        _featureFlags = featureFlags;
        _developmentModeService = developmentModeService;
        _hostEnvironment = hostEnvironment;
        _developmentOptions = developmentOptions;
        _verticalProfiles = verticalProfiles;
        _profileGuard = profileGuard;
    }

    /// <summary>
    /// Profile, feature-flag, and development-mode checks that run before the payment try block.
    /// </summary>
    public async Task<SaleGatePhase<PaymentPreconditions>> ValidatePreconditionsAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var prescriptionReference = NormalizePrescriptionReference(request.PrescriptionReference);
        if (prescriptionReference is not null
            && !await AllowsFeatureAsync("patientRecord", cancellationToken).ConfigureAwait(false))
        {
            return SaleGatePhase<PaymentPreconditions>.Fail(ProfileFeatureRejected("patientRecord"));
        }

        var routeFrom = NormalizeTaxiText(request.RouteFrom);
        var routeTo = NormalizeTaxiText(request.RouteTo);
        var routeKm = request.RouteKm;
        var tripStartedAtUtc = request.TripStartedAtUtc;
        if ((routeFrom is not null || routeTo is not null || routeKm.HasValue || tripStartedAtUtc.HasValue)
            && !await AllowsTaxiSurfaceAsync(cancellationToken).ConfigureAwait(false))
        {
            return SaleGatePhase<PaymentPreconditions>.Fail(ProfileFeatureRejected(VerticalProfileGuard.TaxiFeature));
        }

        if (!request.IsStorno && !request.IsRefund)
        {
            var profileFieldRejection = await RejectDisabledProfileSaleFieldsAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (profileFieldRejection is not null)
                return SaleGatePhase<PaymentPreconditions>.Fail(profileFieldRejection);
        }

        var tenantIdForFlags = await _settingsTenantResolver
            .ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (_featureFlags?.IsEnabled(
                FeatureFlagNames.EnableNewPaymentFlow,
                tenantIdForFlags.ToString("D")) == true)
        {
            // Gradual rollout hook — keep legacy path until V2 payment code is complete.
            _logger.LogInformation(
                "EnableNewPaymentFlow active for tenant {TenantId}; using instrumented payment path",
                tenantIdForFlags);
        }

        if (_developmentModeService is { } dm && dm.ShouldSimulateOffline() && !dm.ShouldForceOnline())
        {
            if (Random.Shared.Next(100) < 30)
            {
                _logger.LogWarning("Development mode active: {BypassType} bypassed", "SimulateOffline");
                return SaleGatePhase<PaymentPreconditions>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Entwicklungsmodus: Offline-Simulation (zufällig).",
                    Errors = { "Development mode: simulated offline / transient failure." },
                    IsDeterministicFailure = false,
                    DiagnosticCode = "DEV_SIMULATE_OFFLINE",
                });
            }
        }

        return SaleGatePhase<PaymentPreconditions>.Ok(new PaymentPreconditions
        {
            TenantIdForFlags = tenantIdForFlags,
            PrescriptionReference = prescriptionReference,
            RouteFrom = routeFrom,
            RouteTo = routeTo,
            RouteKm = routeKm,
            TripStartedAtUtc = tripStartedAtUtc,
        });
    }

    /// <summary>
    /// Tax identity, cash-register authorization, and TSE-offline routing before the fiscal transaction.
    /// A non-null stop is either a rejection or a queued non-fiscal intent.
    /// </summary>
    public async Task<SaleGatePhase<PreparedFiscalSale>> PrepareFiscalSaleAsync(
        CreatePaymentRequest request,
        string userId,
        Customer customer)
    {
        var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync().ConfigureAwait(false);
        var countryBinding = await _countryStrategyContext.LoadAsync().ConfigureAwait(false);
        var taxStrategy = _taxStrategyResolver.Resolve(countryBinding.Profile, countryBinding.VatRegime);

        var effectiveSteuernummer = !string.IsNullOrWhiteSpace(request.Steuernummer)
            && taxStrategy.ValidateVatId(request.Steuernummer, countryBinding.Profile).IsValid
            ? request.Steuernummer!.Trim()
            : companyProfile.TaxNumber;

        if (!taxStrategy.ValidateVatId(effectiveSteuernummer, countryBinding.Profile).IsValid)
        {
            _logger.LogWarning("Resolved Steuernummer invalid (check company settings CompanyTaxNumber): {TaxNumber}", effectiveSteuernummer);
            return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
            {
                Success = false,
                Message = "Invalid Austrian tax number format",
                Errors = { "Steuernummer must be in ATU format (e.g., ATU12345678). Configure company settings (CompanyTaxNumber)." }
            });
        }

        if (request.CashRegisterId == Guid.Empty)
        {
            return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
            {
                Success = false,
                Message = "CashRegisterId is required",
                Errors = { "CashRegisterId must be a non-empty GUID referencing an authorized open cash register." },
                DiagnosticCode = CashRegisterResolutionCodes.Required,
                IsDeterministicFailure = true
            });
        }

        var principal = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        // Cash register: only ValidatePaymentRegisterAsync here — no IPosCashRegisterReadinessService / nextAction check.
        // Shift and assignment rules are aligned with ensure-ready conflict policy; session DTO is client-only unless refreshed.
        // AppPermissions.CashRegisterView does not bypass another user's shift at payment time (CashRegisterShiftOccupancy.IsHeldByOtherUser first).
        var registerValidation = await _cashRegisterResolution.ValidatePaymentRegisterAsync(
            userId,
            request.CashRegisterId,
            principal);
        if (!registerValidation.Ok)
        {
            _logger.LogWarning(
                "Payment rejected: cash register policy. UserId={UserId} RegisterId={RegisterId} Code={Code}",
                userId,
                request.CashRegisterId,
                registerValidation.Code);
            return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
            {
                Success = false,
                Message = registerValidation.Message,
                Errors = { registerValidation.Message },
                DiagnosticCode = MapCashRegisterDiagnosticToRksvPaymentCode(registerValidation.Code)
            });
        }

        // TSE signing is server policy: TseMode Off skips; Demo/Device always sign.
        // Client TseRequired is a hint only — POS __DEV__ toggles must not skip the chain.
        var requestPaymentMethodCode = string.IsNullOrWhiteSpace(request.Payment.Method)
            ? null
            : request.Payment.Method.Trim().ToLowerInvariant();
        var voucherLikelyFromClientMethod =
            string.Equals(requestPaymentMethodCode, "voucher", StringComparison.Ordinal);
        var hasVoucherRedemptions =
            request.Payment.VoucherRedemptions != null && request.Payment.VoucherRedemptions.Count > 0;
        var hasVoucherCode = !string.IsNullOrWhiteSpace(request.Payment.VoucherCode);
        var hasVoucherPayload = hasVoucherRedemptions || hasVoucherCode;
        if (voucherLikelyFromClientMethod
            && !hasVoucherPayload)
        {
            const string msg = "Voucher payment requires voucherCode or voucherRedemptions when method is voucher.";
            return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
            {
                Success = false,
                Message = msg,
                Errors = { msg },
                DiagnosticCode = RksvGuardErrorCodes.VoucherCodeRequired,
                IsDeterministicFailure = true
            });
        }

        var effectiveTseRequired = _tseOptions.RequiresFiscalSignature;
        if (effectiveTseRequired && !request.Payment.TseRequired)
        {
            _logger.LogWarning(
                "Client sent TseRequired=false but TseMode={TseMode}; server requires TSE signature.",
                _tseOptions.TseMode);
        }

        if (effectiveTseRequired && !_tseOptions.UseSoftTseWhenNoDevice)
        {
            var health = _tseHealthMonitor.Snapshot;
            var devBypassTse = _developmentModeService?.ShouldBypassTseCheck() == true;
            var devForceOnline = _developmentModeService?.ShouldForceOnline() == true;
            var tseTreatAsOffline = !devBypassTse
                && !devForceOnline
                && (health.Status == TseOperationalHealth.Offline
                    || (!OpenApiExportMode.IsEnabled
                        && _hostEnvironment?.IsDevelopment() == true
                        && _developmentOptions?.CurrentValue.SimulateTseUnavailable == true));
            if (tseTreatAsOffline)
            {
                if (_tseOptions.OfflineModeEnabled)
                {
                    if (voucherLikelyFromClientMethod || hasVoucherPayload)
                    {
                        return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
                        {
                            Success = false,
                            Message = "TSE offline: Gutschein-Zahlungen sind derzeit nicht möglich.",
                            Errors =
                            {
                                "TSE offline: voucher payments cannot be queued without an active signing device."
                            },
                            DiagnosticCode = "TSE_OFFLINE_VOUCHER_BLOCKED",
                            IsDeterministicFailure = true
                        });
                    }

                    var methodLower = requestPaymentMethodCode ?? string.Empty;
                    if (methodLower is not ("cash" or "card"))
                    {
                        return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
                        {
                            Success = false,
                            Message =
                                "TSE offline: only cash or card payments can be queued as non-fiscal intents.",
                            Errors = { "Unsupported payment method while TSE is offline." },
                            DiagnosticCode = "TSE_OFFLINE_METHOD_UNSUPPORTED",
                            IsDeterministicFailure = true
                        });
                    }

                    return SaleGatePhase<PreparedFiscalSale>.Fail(await _offlineIntents.TryQueueServerNonFiscalOfflineAsync(
                            request,
                            userId,
                            registerValidation.ResolvedRegisterId!.Value)
                        .ConfigureAwait(false));
                }

                _logger.LogError("TSE offline (health monitor); OfflineMode disabled.");
                return SaleGatePhase<PreparedFiscalSale>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "TSE temporarily unavailable",
                    Errors = { "TSE is offline." },
                    DiagnosticCode = "TSE_HEALTH_OFFLINE"
                });
            }
        }

        return SaleGatePhase<PreparedFiscalSale>.Ok(new PreparedFiscalSale
        {
            Customer = customer,
            CompanyProfile = companyProfile,
            CountryBinding = countryBinding,
            TaxStrategy = taxStrategy,
            EffectiveSteuernummer = effectiveSteuernummer,
            Principal = principal,
            VoucherLikelyFromClientMethod = voucherLikelyFromClientMethod,
            HasVoucherPayload = hasVoucherPayload,
            EffectiveTseRequired = effectiveTseRequired,
        });
    }

    private static string? NormalizePrescriptionReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeTaxiText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private async Task<bool> AllowsFeatureAsync(string feature, CancellationToken cancellationToken)
    {
        if (_profileGuard is not null)
            return await _profileGuard.IsFeatureEnabledAsync(feature, cancellationToken).ConfigureAwait(false);

        if (_verticalProfiles is null)
            return false;

        var profile = await _verticalProfiles
            .GetForCurrentTenantAsync(cancellationToken)
            .ConfigureAwait(false);
        return profile?.HasPosFeature(feature) == true;
    }

    private async Task<bool> AllowsTaxiSurfaceAsync(CancellationToken cancellationToken)
    {
        if (_profileGuard is not null)
            return await _profileGuard.IsTaxiSurfaceAsync(cancellationToken).ConfigureAwait(false);

        if (_verticalProfiles is null)
            return false;

        var profile = await _verticalProfiles
            .GetForCurrentTenantAsync(cancellationToken)
            .ConfigureAwait(false);
        return VerticalProfileGuard.IsTaxiSurface(profile);
    }

    private PaymentResult ProfileFeatureRejected(string feature)
    {
        _logger.LogInformation("PROFILE_FEATURE_REJECTED feature={Feature}", feature);
        return new PaymentResult
        {
            Success = false,
            Message = FeatureNotEnabledForProfileException.DefaultMessage,
            Errors = { FeatureNotEnabledForProfileException.Code },
            DiagnosticCode = FeatureNotEnabledForProfileException.Code,
            ProfileFeature = feature,
            IsDeterministicFailure = true,
        };
    }

    private async Task<PaymentResult?> RejectDisabledProfileSaleFieldsAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var hasImei = request.Items?.Any(item =>
            ProductImeiService.ResolveRequestedImeis(item.Imei, item.Imeis).Count > 0) == true;
        if (hasImei && !await AllowsFeatureAsync("imeiTracking", cancellationToken).ConfigureAwait(false))
            return ProfileFeatureRejected("imeiTracking");

        var productIds = request.Items?.Select(item => item.ProductId).Distinct().ToList();
        if (productIds is not { Count: > 0 })
            return null;

        var hasTicketProduct = await _context.Products
            .AsNoTracking()
            .AnyAsync(
                product => productIds.Contains(product.Id) && product.IsTicket,
                cancellationToken)
            .ConfigureAwait(false);
        if (hasTicketProduct && !await AllowsFeatureAsync("ticketScan", cancellationToken).ConfigureAwait(false))
            return ProfileFeatureRejected("ticketScan");

        return null;
    }

    private static string? MapCashRegisterDiagnosticToRksvPaymentCode(string? cashRegisterResolutionCode) =>
        string.Equals(cashRegisterResolutionCode, CashRegisterResolutionCodes.Decommissioned, StringComparison.Ordinal)
            ? RksvGuardErrorCodes.RegisterDecommissioned
            : cashRegisterResolutionCode;
}

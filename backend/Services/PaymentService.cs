using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Lodging;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.Imei;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Services.Preorder;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using AuditLogStatus = KasseAPI_Final.Models.AuditLogStatus;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Ödeme işlemleri için service implementation
    /// </summary>
    public partial class PaymentService : IPaymentService
    {
        private static readonly TenantLicenseValidator TenantLicenseValidator = new();

        private readonly AppDbContext _context;
        private readonly IGenericRepository<PaymentDetails> _paymentRepository;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IGenericRepository<Customer> _customerRepository;
        private readonly ITseService _tseService;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly ILogger<PaymentService> _logger;
        private readonly IUserService _userService;
        private readonly ICompanyProfileProvider _companyProfileProvider;
        private readonly TseOptions _tseOptions;
        private readonly IProductModifierValidationService _modifierValidation;
        private readonly IReceiptSequenceService _receiptSequenceService;
        private readonly IReceiptService _receiptService;
        private readonly IAuditLogService _auditLogService;
        private readonly IFinanzOnlineMetrics? _finanzOnlineMetrics;
        private readonly ICashRegisterResolutionService _cashRegisterResolution;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPaymentMethodCatalogService _paymentMethodCatalog;
        private readonly IPricingRuleResolver _pricingRuleResolver;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly InventoryOptions _inventoryOptions;
        private readonly IPosCriticalActionAuditService _posCriticalAudit;
        private readonly ITseHealthMonitor _tseHealthMonitor;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly INtpEffectiveSettingsProvider _ntpEffectiveSettings;
        private readonly INtpTimeSyncStatus _ntpTimeSyncStatus;
        private readonly ILicenseService? _licenseService;
        private readonly IOptions<OfflineVoucherEncryptionOptions> _offlineVoucherEncryption;
        private readonly IHostEnvironment? _hostEnvironment;
        private readonly IOptionsMonitor<DevelopmentOptions>? _developmentOptions;
        private readonly IDevelopmentModeService? _developmentModeService;
        private readonly LicenseOptions? _licenseOptions;
        private readonly IPaymentReversalApprovalService _reversalApproval;
        private readonly ICardPaymentService? _cardPaymentService;
        private readonly FeatureFlags.IFeatureFlagService? _featureFlags;
        private readonly ITenantLimitGuard? _tenantLimitGuard;
        private readonly IPreorderService? _preorderService;
        private readonly ICountryStrategyContext _countryStrategyContext;
        private readonly ITaxStrategyResolver _taxStrategyResolver;
        private readonly ICountryTaxTypeRegistry _taxTypes;
        private readonly Fiscal.IFiscalSignatureRouter _fiscalSignatureRouter;
        private readonly IDeReceiptSequenceService? _deReceiptSequenceService;
        private readonly IChReceiptSequenceService? _chReceiptSequenceService;
        private readonly IEuReceiptSequenceService? _euReceiptSequenceService;
        private readonly IChMwstEffectiveRates? _chMwstRates;
        private readonly IVerticalProfileService? _verticalProfiles;
        private readonly IVerticalProfileGuard? _profileGuard;
        private readonly ITicketRedemptionService? _ticketRedemptions;
        private readonly ITenantExtensionRegistry? _extensionRegistry;
        private readonly IPaymentQueryService _paymentQuery;
        private readonly IPaymentFinanzOnlineSubmissionService _finanzOnlineSubmission;
        private readonly IPaymentReceiptReadService _receiptRead;
        private readonly IPaymentOfflineIntentService _offlineIntents;
        private readonly IPaymentBenefitService _benefits;
        private readonly IPaymentVoucherLedgerService _voucherLedger;

        private static readonly IDataProtectionProvider FallbackOfflinePayloadProtection =
            DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "regkasse-offline-intent-dp")));

        public PaymentService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IGenericRepository<Product> productRepository,
            IGenericRepository<Customer> customerRepository,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService,
            IUserService userService,
            IProductModifierValidationService modifierValidation,
            IReceiptSequenceService receiptSequenceService,
            IReceiptService receiptService,
            IAuditLogService auditLogService,
            ICompanyProfileProvider companyProfileProvider,
            Microsoft.Extensions.Options.IOptions<TseOptions> tseOptions,
            Microsoft.Extensions.Options.IOptions<InventoryOptions> inventoryOptions,
            ILogger<PaymentService> logger,
            ICashRegisterResolutionService cashRegisterResolution,
            IHttpContextAccessor httpContextAccessor,
            IPaymentMethodCatalogService paymentMethodCatalog,
            IPricingRuleResolver pricingRuleResolver,
            ISettingsTenantResolver settingsTenantResolver,
            IFinanzOnlineMetrics? finanzOnlineMetrics = null,
            IPosCriticalActionAuditService? posCriticalAudit = null,
            IOptions<NtpSettings>? ntpSettings = null,
            INtpEffectiveSettingsProvider? ntpEffectiveSettings = null,
            INtpTimeSyncStatus? ntpTimeSyncStatus = null,
            ITseHealthMonitor? tseHealthMonitor = null,
            IDataProtectionProvider? dataProtectionProvider = null,
            ILicenseService? licenseService = null,
            IOptions<OfflineVoucherEncryptionOptions>? offlineVoucherEncryption = null,
            IHostEnvironment? hostEnvironment = null,
            IOptionsMonitor<DevelopmentOptions>? developmentOptions = null,
            IDevelopmentModeService? developmentModeService = null,
            IOptions<LicenseOptions>? licenseOptions = null,
            IPaymentReversalApprovalService? reversalApproval = null,
            ICardPaymentService? cardPaymentService = null,
            FeatureFlags.IFeatureFlagService? featureFlags = null,
            ITenantLimitGuard? tenantLimitGuard = null,
            IPreorderService? preorderService = null,
            ICountryStrategyContext? countryStrategyContext = null,
            ITaxStrategyResolver? taxStrategyResolver = null,
            ICountryProfileRegistry? countryProfileRegistry = null,
            ICountryTaxTypeRegistry? countryTaxTypeRegistry = null,
            Fiscal.IFiscalSignatureRouter? fiscalSignatureRouter = null,
            IDeReceiptSequenceService? deSequenceService = null,
            IChReceiptSequenceService? chSequenceService = null,
            IEuReceiptSequenceService? euSequenceService = null,
            IChMwstEffectiveRates? chMwstRates = null,
            IVerticalProfileService? verticalProfiles = null,
            ITicketRedemptionService? ticketRedemptions = null,
            IVerticalProfileGuard? profileGuard = null,
            ITenantExtensionRegistry? extensionRegistry = null,
            IPaymentQueryService? paymentQuery = null,
            IPaymentFinanzOnlineSubmissionService? finanzOnlineSubmission = null,
            IPaymentReceiptReadService? receiptRead = null,
            IPaymentOfflineIntentService? offlineIntents = null,
            IPaymentBenefitService? benefits = null,
            IPaymentVoucherLedgerService? voucherLedger = null)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
            _userService = userService;
            _modifierValidation = modifierValidation;
            _receiptSequenceService = receiptSequenceService;
            _receiptService = receiptService;
            _auditLogService = auditLogService;
            _companyProfileProvider = companyProfileProvider;
            _tseOptions = tseOptions.Value;
            _inventoryOptions = inventoryOptions.Value;
            _logger = logger;
            _cashRegisterResolution = cashRegisterResolution;
            _httpContextAccessor = httpContextAccessor;
            _paymentMethodCatalog = paymentMethodCatalog;
            _pricingRuleResolver = pricingRuleResolver;
            _settingsTenantResolver = settingsTenantResolver;
            _finanzOnlineMetrics = finanzOnlineMetrics;
            _posCriticalAudit = posCriticalAudit ?? PosCriticalActionAuditNoOp.Instance;
            var ntpOpt = ntpSettings ?? Options.Create(new NtpSettings { Enabled = false });
            _ntpEffectiveSettings = ntpEffectiveSettings ?? new OptionsOnlyNtpEffectiveSettingsProvider(ntpOpt);
            _ntpTimeSyncStatus = ntpTimeSyncStatus ?? PermissiveNtpTimeSyncStatus.Instance;
            _tseHealthMonitor = tseHealthMonitor ?? AlwaysOnlineTseHealthMonitor.Instance;
            _dataProtectionProvider = dataProtectionProvider ?? FallbackOfflinePayloadProtection;
            _licenseService = licenseService;
            _offlineVoucherEncryption = offlineVoucherEncryption ?? Options.Create(new OfflineVoucherEncryptionOptions());
            _hostEnvironment = hostEnvironment;
            _developmentOptions = developmentOptions;
            _developmentModeService = developmentModeService;
            _licenseOptions = licenseOptions?.Value;
            _reversalApproval = reversalApproval ?? NoOpPaymentReversalApprovalService.Instance;
            _cardPaymentService = cardPaymentService;
            _featureFlags = featureFlags;
            _tenantLimitGuard = tenantLimitGuard;
            _preorderService = preorderService;
            var registry = countryProfileRegistry ?? new CountryProfileRegistry();
            _countryStrategyContext = countryStrategyContext
                ?? new CountryStrategyContext(_context, registry, _settingsTenantResolver);
            _taxStrategyResolver = taxStrategyResolver ?? CountryStrategyWiring.CreateTaxResolver();
            _taxTypes = countryTaxTypeRegistry ?? new CountryTaxTypeRegistry();
            _fiscalSignatureRouter = fiscalSignatureRouter
                ?? new Fiscal.FiscalSignatureRouter(_featureFlags, tse: _tseService);
            _deReceiptSequenceService = deSequenceService;
            _chReceiptSequenceService = chSequenceService;
            _euReceiptSequenceService = euSequenceService;
            _chMwstRates = chMwstRates;
            _verticalProfiles = verticalProfiles;
            _ticketRedemptions = ticketRedemptions;
            _profileGuard = profileGuard;
            _extensionRegistry = extensionRegistry;
            _paymentQuery = paymentQuery ?? new PaymentQueryService(
                _paymentRepository,
                _context,
                _paymentMethodCatalog,
                _settingsTenantResolver,
                _logger);
            _finanzOnlineSubmission = finanzOnlineSubmission ?? new PaymentFinanzOnlineSubmissionService(
                _context,
                _companyProfileProvider,
                _finanzOnlineService,
                _auditLogService,
                _userService,
                _settingsTenantResolver,
                _logger,
                _finanzOnlineMetrics);
            _receiptRead = receiptRead ?? new PaymentReceiptReadService(
                _receiptService,
                _paymentRepository,
                _auditLogService,
                _userService,
                _logger);
            _offlineIntents = offlineIntents ?? new PaymentOfflineIntentService(
                _context,
                _logger,
                _settingsTenantResolver,
                _dataProtectionProvider,
                _offlineVoucherEncryption,
                _tseOptions,
                _hostEnvironment,
                _developmentModeService,
                _licenseOptions,
                _tenantLimitGuard);
            _benefits = benefits ?? new PaymentBenefitService(
                _context,
                _customerRepository,
                _settingsTenantResolver,
                _countryStrategyContext,
                _taxStrategyResolver,
                _pricingRuleResolver,
                _taxTypes,
                _chMwstRates);
            _voucherLedger = voucherLedger ?? new PaymentVoucherLedgerService(_context, _logger);
        }

        private Task RunPaymentHookAsync(
            Guid tenantId,
            string hookName,
            Guid paymentId,
            decimal? totalAmount = null,
            string? receiptNumber = null)
        {
            var payload = new Dictionary<string, object?>
            {
                ["paymentId"] = paymentId,
            };
            if (totalAmount is decimal amount)
                payload["totalAmount"] = amount;
            if (!string.IsNullOrEmpty(receiptNumber))
                payload["receiptNumber"] = receiptNumber;

            return TenantExtensionRunner.RunAsync(
                _extensionRegistry,
                tenantId,
                hookName,
                payload,
                CancellationToken.None);
        }

        /// <summary>
        /// Sale-path <c>payment.before-sign</c>. When an extension returns adjusted line gross
        /// totals, those amounts are re-taxed with the existing country strategy and written onto
        /// the payment before the unchanged <c>SignAsync</c> call. Payloads without a matching
        /// line list leave the cart as calculated.
        /// </summary>
        private async Task<(TaxCalculationResult Tax, Dictionary<string, decimal> Details)> ApplyBeforeSignCartAsync(
            Guid tenantId,
            PaymentDetails payment,
            List<PaymentItem> paymentItems,
            TaxCalculationResult taxResult,
            Dictionary<string, decimal> taxDetails,
            ITaxStrategy taxStrategy,
            CountryStrategyBinding countryBinding,
            ICountryTaxTypeRegistry rateCatalog,
            string? buyerVatId,
            IReadOnlyDictionary<Guid, bool> requiresPrescriptionByProduct)
        {
            var lines = paymentItems.Select(item => new ExtensionCartLine(
                item.ProductId,
                item.Quantity,
                item.TaxType,
                item.TotalPrice,
                requiresPrescriptionByProduct.TryGetValue(item.ProductId, out var required) && required)).ToList();

            var payload = new Dictionary<string, object?>
            {
                ["paymentId"] = payment.Id,
                ["totalAmount"] = payment.TotalAmount,
                ["receiptNumber"] = payment.ReceiptNumber,
                [ExtensionCart.HasPrescriptionKey] = !string.IsNullOrEmpty(payment.PrescriptionReference),
                [ExtensionCart.PrescriptionReferenceKey] = payment.PrescriptionReference,
                [ExtensionCart.LinesKey] = lines,
            };

            var modified = await TenantExtensionRunner.RunAsync(
                    _extensionRegistry,
                    tenantId,
                    TenantExtensionHooks.PaymentBeforeSign,
                    payload,
                    CancellationToken.None)
                .ConfigureAwait(false);

            var adjusted = ExtensionCart.ReadLines(modified);
            if (adjusted.Count != paymentItems.Count || adjusted.Count != taxResult.Lines.Count)
                return (taxResult, taxDetails);

            var changed = false;
            for (var i = 0; i < adjusted.Count; i++)
            {
                if (adjusted[i].GrossTotal != paymentItems[i].TotalPrice)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
                return (taxResult, taxDetails);

            var inputs = new List<TaxLineItemInput>(adjusted.Count);
            for (var i = 0; i < adjusted.Count; i++)
            {
                inputs.Add(CountryPaymentTaxLineMapper.FromProductTaxType(
                    countryBinding.Profile,
                    adjusted[i].GrossTotal,
                    1,
                    paymentItems[i].TaxType,
                    rateCatalog));
            }

            var recalculated = taxStrategy.CalculateTax(
                inputs,
                new TaxCalculationContext
                {
                    CountryProfile = countryBinding.Profile,
                    VatRegime = countryBinding.VatRegime,
                    TaxExempt = countryBinding.Settings.TaxExempt,
                    BuyerVatId = buyerVatId,
                    DestinationCountry = CountryPaymentTaxLineMapper.ResolveOssDestinationCountry(
                        countryBinding.VatRegime,
                        buyerVatId),
                    TenantId = countryBinding.Settings.TenantId,
                });

            for (var i = 0; i < paymentItems.Count && i < recalculated.Lines.Count; i++)
            {
                var line = recalculated.Lines[i];
                var quantity = paymentItems[i].Quantity <= 0 ? 1 : paymentItems[i].Quantity;
                paymentItems[i].TotalPrice = line.LineGross;
                paymentItems[i].TaxAmount = line.LineTax;
                paymentItems[i].LineNet = line.LineNet;
                paymentItems[i].TaxRate = line.TaxRate;
                paymentItems[i].TaxType = line.TaxType;
                paymentItems[i].UnitPrice = quantity == 1
                    ? line.LineGross
                    : decimal.Round(line.LineGross / quantity, 2, MidpointRounding.AwayFromZero);
            }

            var details = recalculated.TaxDetails.ToDictionary(static kv => kv.Key, static kv => kv.Value);
            payment.TotalAmount = recalculated.Totals.TotalGross;
            payment.TaxAmount = recalculated.Totals.TotalVat;
            payment.TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(details));
            payment.PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(paymentItems));
            return (recalculated, details);
        }

        private static PaymentResult ExtensionRejected(ExtensionFailedException ex) =>
            new()
            {
                Success = false,
                Message = ex.Message,
                Errors = { ex.Message },
                IsDeterministicFailure = true,
                DiagnosticCode = ExtensionFailedException.ErrorCode,
            };

        private ICountryTaxTypeRegistry TaxTypesFor(CountryStrategyBinding binding)
        {
            if (_chMwstRates is null
                || !string.Equals(binding.Profile.Code, CountryProfileCodes.Switzerland, StringComparison.OrdinalIgnoreCase))
            {
                return _taxTypes;
            }

            return _chMwstRates.ForCalculation(_taxTypes, binding.Settings.TenantId);
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

        private async Task<bool> TenantIssuesTicketsAsync(CancellationToken cancellationToken) =>
            await AllowsFeatureAsync("ticketScan", cancellationToken).ConfigureAwait(false);

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

        private static PaymentResult ImeiPaymentFailure(string code, string message) => new()
        {
            Success = false,
            Message = message,
            Errors = { code },
            DiagnosticCode = code,
            IsDeterministicFailure = true,
        };

        private async Task<PaymentResult?> TryReserveImeisForLineAsync(
            Product product,
            PaymentItemRequest itemRequest,
            List<ProductImei> pendingImeiRows,
            HashSet<string> reservedImeiCodes,
            IDbContextTransaction transaction)
        {
            var requested = ProductImeiService.ResolveRequestedImeis(itemRequest.Imei, itemRequest.Imeis);
            if (!product.ImeiTracked)
            {
                if (requested.Count == 0)
                    return null;

                await transaction.RollbackAsync().ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return ImeiPaymentFailure(
                    ProductImeiErrorCodes.NotAllowed,
                    "IMEI is not allowed for this product.");
            }

            if (requested.Count != itemRequest.Quantity)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return ImeiPaymentFailure(
                    ProductImeiErrorCodes.Required,
                    "Each IMEI-tracked product line must include one IMEI per quantity.");
            }

            foreach (var imei in requested)
            {
                if (!reservedImeiCodes.Add(imei))
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    _context.ChangeTracker.Clear();
                    return ImeiPaymentFailure(
                        ProductImeiErrorCodes.Duplicate,
                        "Duplicate IMEI in this payment.");
                }

                var row = await _context.ProductImeis
                    .FirstOrDefaultAsync(item =>
                        item.ProductId == product.Id
                        && item.Imei == imei
                        && item.Status == ProductImeiStatus.InStock)
                    .ConfigureAwait(false);
                if (row == null)
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    _context.ChangeTracker.Clear();
                    return ImeiPaymentFailure(
                        ProductImeiErrorCodes.NotAvailable,
                        $"IMEI {imei} is not available for product {product.Name}.");
                }

                pendingImeiRows.Add(row);
            }

            return null;
        }

        private static PaymentResult VariantPaymentFailure(string code, string message) => new()
        {
            Success = false,
            Message = message,
            Errors = { code },
            DiagnosticCode = code,
            IsDeterministicFailure = true,
        };

        /// <summary>
        /// Variant stock is an operational side effect inside the payment transaction.
        /// The variant id is not written onto payment_details, the receipt, or the TSE payload.
        /// </summary>
        private async Task<PaymentResult?> TryApplyVariantForLineAsync(
            Product product,
            PaymentItemRequest itemRequest,
            Dictionary<PaymentItemRequest, decimal> variantPriceDeltas,
            IDbContextTransaction transaction,
            string userId)
        {
            var hasActiveVariant = await _context.ProductVariants
                .AnyAsync(variant => variant.ProductId == product.Id && variant.IsActive)
                .ConfigureAwait(false);
            var variantId = itemRequest.VariantId;

            if (!hasActiveVariant)
            {
                if (variantId is null || variantId == Guid.Empty)
                    return null;

                await transaction.RollbackAsync().ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return VariantPaymentFailure(
                    ProductVariantSaleCodes.NotAllowed,
                    "Variant is not allowed for this product.");
            }

            if (variantId is null || variantId == Guid.Empty)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return VariantPaymentFailure(
                    ProductVariantSaleCodes.Required,
                    "A variant is required for this product.");
            }

            var row = await _context.ProductVariants
                .FirstOrDefaultAsync(variant =>
                    variant.Id == variantId
                    && variant.ProductId == product.Id
                    && variant.IsActive)
                .ConfigureAwait(false);
            if (row == null || row.TenantId != product.TenantId)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return VariantPaymentFailure(
                    ProductVariantSaleCodes.Invalid,
                    "Variant does not belong to this product.");
            }

            if (row.Stock < itemRequest.Quantity)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _context.ChangeTracker.Clear();
                return VariantPaymentFailure(
                    ProductVariantSaleCodes.StockInsufficient,
                    "Variant stock is insufficient.");
            }

            row.Stock -= itemRequest.Quantity;
            row.UpdatedAtUtc = DateTime.UtcNow;
            variantPriceDeltas[itemRequest] = row.PriceDelta;

            try
            {
                await _auditLogService.LogSystemOperationAsync(
                    action: "PRODUCT_VARIANT_STOCK_ADJUSTED",
                    entityType: "ProductVariant",
                    userId: string.IsNullOrWhiteSpace(userId) ? "system" : userId,
                    userRole: "Cashier",
                    description: "PRODUCT_VARIANT_STOCK_ADJUSTED",
                    actionType: AuditEventType.ProductVariantStockAdjusted,
                    entityId: row.Id,
                    tenantId: product.TenantId,
                    newValues: new
                    {
                        tenantId = product.TenantId,
                        productId = product.Id,
                        variantId = row.Id,
                        delta = -itemRequest.Quantity,
                        newStock = row.Stock,
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Variant stock audit failed for {VariantId}", row.Id);
            }

            return null;
        }

        private async Task<string> AllocateCountryBelegNrAsync(
            CountryStrategyBinding countryBinding,
            IDbContextTransaction transaction,
            Guid cashRegisterId,
            string registerNumber)
        {
            if (countryBinding.Profile.Code == CountryProfileCodes.EuDefault)
                return await FormatFromSequenceAsync(
                    _euReceiptSequenceService,
                    EuReceiptSequenceService.FormatEuBelegNr,
                    countryBinding,
                    cashRegisterId,
                    registerNumber);

            return countryBinding.Profile.FiscalSystem switch
            {
                FiscalSystem.RKSV_AT => await _receiptSequenceService
                    .AllocateNextBelegNrInTransactionAsync(transaction, cashRegisterId, registerNumber, DateTime.UtcNow),
                FiscalSystem.KASSENSICHERHEIT_DE => await FormatFromSequenceAsync(
                    _deReceiptSequenceService,
                    DeReceiptSequenceService.FormatDeBelegNr,
                    countryBinding,
                    cashRegisterId,
                    registerNumber),
                FiscalSystem.MWST_CH => await FormatFromSequenceAsync(
                    _chReceiptSequenceService,
                    ChReceiptSequenceService.FormatChBelegNr,
                    countryBinding,
                    cashRegisterId,
                    registerNumber),
                _ => throw new InvalidOperationException($"No sequence service for {countryBinding.Profile.Code}"),
            };
        }

        private async Task<string> FormatFromSequenceAsync(
            object? sequenceService,
            Func<string, string, int, string> formatter,
            CountryStrategyBinding countryBinding,
            Guid cashRegisterId,
            string registerNumber)
        {
            if (sequenceService is null)
                throw new InvalidOperationException($"Sequence service missing for {countryBinding.Profile.Code}");

            var tenantId = countryBinding.Settings.TenantId;
            var seq = await (sequenceService switch
            {
                IDeReceiptSequenceService de => de.AllocateNextAsync(tenantId, cashRegisterId),
                IChReceiptSequenceService ch => ch.AllocateNextAsync(tenantId, cashRegisterId),
                IEuReceiptSequenceService eu => eu.AllocateNextAsync(tenantId, cashRegisterId),
                _ => throw new InvalidOperationException("Unknown sequence service type"),
            });

            var slug = await _context.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(slug))
                throw new InvalidOperationException($"Tenant slug missing for '{tenantId}'.");

            return formatter(slug, registerNumber, seq);
        }

        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        public async Task<PaymentResult> CreatePaymentAsync(
            CreatePaymentRequest request,
            string userId,
            Guid? offlineTransactionId = null,
            Guid? offlineReplayBatchCorrelationId = null)
        {
            try
            {
                var result = await CreatePaymentCoreAsync(request, userId, offlineTransactionId, offlineReplayBatchCorrelationId);
                await TryAttachPreorderAfterSaleAsync(request, result, userId);
                await TryApplyPreorderBalanceAfterSaleAsync(request, result);
                await _posCriticalAudit.LogPaymentOutcomeAsync(userId, request, result);
                return result;
            }
            catch (Exception ex)
            {
                await _posCriticalAudit.LogPaymentUnhandledExceptionAsync(userId, request, ex);
                throw;
            }
        }

        private async Task EnsureLicenseAllowsPaymentAsync(CancellationToken cancellationToken)
        {
            if (_licenseService is null)
                return;

            if (LicenseEnforcementPolicy.ShouldDisableEnforcement(
                    _hostEnvironment,
                    _tseOptions,
                    _developmentModeService,
                    _licenseOptions))
            {
                return;
            }

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
            if (tenantId == Guid.Empty)
                goto CheckDeployment;

            var tenantLicenseValidUntilUtc = await _context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.LicenseValidUntilUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!tenantLicenseValidUntilUtc.HasValue)
                goto CheckDeployment;

            var tenantStatus = TenantLicenseValidator.GetStatus(tenantLicenseValidUntilUtc);
            if (tenantStatus == TenantLicenseStatus.GraceReadOnly
                || tenantStatus == TenantLicenseStatus.Lockdown
                || tenantStatus == TenantLicenseStatus.Archived)
            {
                _logger.LogWarning(
                    "Payment blocked: tenant license denies payments (TenantId={TenantId}, Status={Status}, ValidUntilUtc={ValidUntilUtc})",
                    tenantId,
                    tenantStatus,
                    tenantLicenseValidUntilUtc);
                throw new LicenseExpiredException(
                    "tenant",
                    "Neue Zahlungen sind aufgrund abgelaufener Mandantenlizenz nicht mehr moeglich. " +
                    "Bitte verlaengern Sie die Lizenz, um weiterhin Verkaeufe durchfuehren zu koennen.");
            }

        CheckDeployment:
            var deploymentStatus = await _licenseService
                .GetCurrentDeploymentStatusAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!deploymentStatus.IsExpired || deploymentStatus.IsTrial)
                return;

            var daysExpired = deploymentStatus.ExpiryDate.HasValue
                ? Math.Abs((DateTime.UtcNow - DateTime.SpecifyKind(deploymentStatus.ExpiryDate.Value, DateTimeKind.Utc)).Days)
                : Math.Abs(deploymentStatus.DaysRemaining);
            if (daysExpired <= 15)
                return;

            _logger.LogWarning(
                "Payment blocked: deployment license expired beyond grace period (DaysExpired={DaysExpired}, ExpiryUtc={ExpiryUtc})",
                daysExpired,
                deploymentStatus.ExpiryDate);
            throw new LicenseExpiredException(
                "deployment",
                "Die Server-Lizenz ist seit mehr als 15 Tagen abgelaufen. " +
                "Zahlungen sind deaktiviert. Bitte verlaengern Sie die Lizenz im Admin-Panel.");
        }

        private async Task<PaymentResult?> TryRejectSaleForTenantLimitsAsync(
            Guid tenantId,
            decimal totalAmount,
            Guid? offlineTransactionId,
            CancellationToken cancellationToken)
        {
            if (_tenantLimitGuard is null || tenantId == Guid.Empty || offlineTransactionId.HasValue)
                return null;

            try
            {
                await _tenantLimitGuard
                    .EnsureSaleWithinLimitsAsync(tenantId, totalAmount, cancellationToken)
                    .ConfigureAwait(false);
                return null;
            }
            catch (LimitExceededException ex)
            {
                _logger.LogWarning(
                    "Payment blocked by tenant limit TenantId={TenantId} LimitKey={LimitKey} Limit={Limit} Current={Current}",
                    tenantId,
                    ex.LimitKey,
                    ex.LimitAmount,
                    ex.CurrentAmount);
                return new PaymentResult
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = { ex.Message },
                    DiagnosticCode = LimitExceededException.ErrorCodeValue,
                    LimitError = ex.ToErrorDto(),
                    IsDeterministicFailure = true,
                };
            }
        }



        /// <summary>
        /// Read-only eligibility preview: which benefits would apply for this customer and cart. No persistence (no BenefitDailyUsage write, no payment).
        /// </summary>
        public Task<BenefitEligibilityPreviewResponse?> ComputeBenefitEligibilityPreviewAsync(BenefitEligibilityPreviewRequest request) =>
            _benefits.ComputeBenefitEligibilityPreviewAsync(request);


        /// <summary>
        /// QR payload (RKSV belegdaten veya NON_FISCAL_DEMO) ve demo/fiscal flag'leri üretir.
        /// tseRequired=false: Açık NON_FISCAL marker ile UI yanlışlıkla fiskal sanmasın.
        /// </summary>
        private Task<(string QrPayload, bool IsDemoFiscal, string TseProvider)> BuildQrPayloadAndFlagsAsync(
            PaymentDetails payment,
            bool tseRequired)
        {
            var isDemoFiscal = !tseRequired || _tseOptions.UseSoftTseWhenNoDevice;
            var tseProvider = tseRequired ? (_tseOptions.UseSoftTseWhenNoDevice ? "Demo" : "Device") : "None";
            var receiptNumber = payment.ReceiptNumber ?? "";
            var createdAt = payment.CreatedAt;
            var totalAmount = payment.TotalAmount;
            var formattedTotalAmount = totalAmount.ToString("F2", CultureInfo.InvariantCulture);
            var signatureValue = payment.TseSignature ?? "";

            string qrPayload;
            if (!string.IsNullOrEmpty(signatureValue))
            {
                if (LooksLikeFiskalyMachineCode(signatureValue))
                {
                    qrPayload = signatureValue.Trim();
                    tseProvider = "Fiskaly";
                    isDemoFiscal = false;
                }
                else if (!RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(signatureValue, out qrPayload))
                {
                    _logger.LogWarning(
                        "Unable to build RKSV §9 QR payload from compact JWS for payment {PaymentId}",
                        payment.Id);
                    qrPayload = string.Empty;
                }
            }
            else
            {
                // tseRequired=false: Açık NON_FISCAL marker (sadece flag değil) - UI fiskal sanmasın
                qrPayload = $"NON_FISCAL_DEMO_{receiptNumber}_{createdAt:yyyy-MM-ddTHH:mm:ss}_{formattedTotalAmount}";
            }

            return Task.FromResult((qrPayload, isDemoFiscal, tseProvider));
        }

        private static bool LooksLikeFiskalyMachineCode(string signatureValue)
        {
            var trimmed = signatureValue.Trim();
            if (!trimmed.StartsWith("_R1-AT", StringComparison.Ordinal))
                return false;
            return trimmed.Split('.').Length != 3;
        }

        /// <summary>
        /// Ödeme için QR payload üretir. Aynı mantık BuildQrPayloadAndFlagsAsync ile.
        /// </summary>
        public async Task<(string? QrPayload, DateTime? UpdatedAt)?> GetQrPayloadForPaymentAsync(Guid paymentId)
        {
            var payment = await GetPaymentAsync(paymentId);
            if (payment == null)
                return null;
            var effectiveTseRequired = !string.IsNullOrEmpty(payment.TseSignature);
            var (qrPayload, _, _) = await BuildQrPayloadAndFlagsAsync(payment, effectiveTseRequired);
            var updatedAt = payment.UpdatedAt ?? payment.CreatedAt;
            return (qrPayload, updatedAt);
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        public Task<PaymentDetails?> GetPaymentAsync(Guid paymentId) =>
            _paymentQuery.GetPaymentAsync(paymentId);

        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        public Task<IEnumerable<PaymentDetails>> GetCustomerPaymentsAsync(Guid customerId, int pageNumber = 1, int pageSize = 20) =>
            _paymentQuery.GetCustomerPaymentsAsync(customerId, pageNumber, pageSize);

        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        public Task<IEnumerable<PaymentDetails>> GetPaymentsByMethodAsync(string paymentMethod, int pageNumber = 1, int pageSize = 20) =>
            _paymentQuery.GetPaymentsByMethodAsync(paymentMethod, pageNumber, pageSize);

        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        public Task<IEnumerable<PaymentDetails>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 20) =>
            _paymentQuery.GetPaymentsByDateRangeAsync(startDate, endDate, pageNumber, pageSize);

        private async Task TryAttachPreorderAfterSaleAsync(
            CreatePaymentRequest request,
            PaymentResult result,
            string userId)
        {
            if (_preorderService is null || !result.Success || result.Payment is null)
                return;
            if (!request.IsPreorder || request.IsStorno || request.IsRefund)
                return;

            try
            {
                await _preorderService.TryCreateFromSuccessfulPaymentAsync(result.Payment, request, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preorder create failed after fiscal payment {PaymentId}", result.Payment.Id);
            }
        }

        private async Task TryApplyPreorderBalanceAfterSaleAsync(
            CreatePaymentRequest request,
            PaymentResult result)
        {
            if (_preorderService is null || !result.Success || result.Payment is null)
                return;
            if (request.PreorderBalanceOrderId is not Guid orderId || orderId == Guid.Empty)
                return;
            if (request.IsStorno || request.IsRefund || request.IsPreorder)
                return;

            try
            {
                await _preorderService.ApplyBalancePaymentAsync(orderId, result.Payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preorder balance apply failed after fiscal payment {PaymentId}", result.Payment.Id);
            }
        }

        private async Task TryMarkPreorderCancelledAsync(Guid sourcePaymentId)
        {
            if (_preorderService is null || sourcePaymentId == Guid.Empty)
                return;

            try
            {
                await _preorderService.MarkCancelledForPaymentAsync(sourcePaymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preorder cancel mark failed for payment {PaymentId}", sourcePaymentId);
            }
        }

        /// <summary>
        /// Cancel payment via fiscal storno (reversal). Original payment is NEVER modified; a reversal record is created with TSE signature and credit note.
        /// Sprint 6: optional idempotencyKey — retries with same key return existing storno.
        /// </summary>
        public async Task<PaymentResult> CancelPaymentAsync(
            Guid paymentId,
            string reason,
            string userId,
            string? idempotencyKey = null,
            CancellationReasonCode reasonCode = CancellationReasonCode.Other,
            string? approvalToken = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cancellation reason is required (minimum 5 characters)",
                        Errors = { "Reason must be at least 5 characters" },
                        DiagnosticCode = "CANCELLATION_REASON_REQUIRED",
                        IsDeterministicFailure = true
                    };
                }

                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment cancel demo rejection: AuthenticatedUserId={AuthenticatedUserId} PaymentId={PaymentId} RejectionCode={RejectionCode}",
                        userId, paymentId, rejectionReason);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot cancel real payments",
                        Errors = { "Demo users are restricted to test operations only" },
                        DiagnosticCode = rejectionReason
                    };
                }

                var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
                if (key != null)
                {
                    var existingStorno = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.CancelIdempotencyKey == key && p.IsStorno);
                    if (existingStorno != null)
                    {
                        if (!await PaymentBelongsToEffectiveTenantAsync(existingStorno))
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Payment not found",
                                Errors = { "Payment not found" }
                            };
                        }
                        _logger.LogInformation("Idempotent cancel: returning existing storno {StornoId} for payment {PaymentId} key {Key}", existingStorno.Id, paymentId, key);
                        await TryMarkPreorderCancelledAsync(paymentId);
                        var replayRefundFailure = await RefundCardAfterFiscalStornoAsync(
                                existingStorno.OriginalPaymentId ?? paymentId,
                                Math.Abs(existingStorno.TotalAmount))
                            .ConfigureAwait(false);
                        if (replayRefundFailure != null)
                            return replayRefundFailure;
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Payment cancelled successfully",
                            Payment = existingStorno
                        };
                    }
                }

                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                if (!await PaymentBelongsToEffectiveTenantAsync(payment))
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                var hasRefundRows = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsRefund && p.IsActive);
                if (hasRefundRows)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cannot cancel: partial refunds exist for this payment",
                        Errors = { "Full storno is not allowed after partial refunds on the same sale." },
                        DiagnosticCode = "STORNO_BLOCKED_BY_REFUNDS",
                        IsDeterministicFailure = true
                    };
                }

                // Already cancelled = a storno (reversal) already exists for this payment. Original payment is never modified.
                var alreadyHasStorno = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
                if (alreadyHasStorno)
                {
                    var refundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                        .ConfigureAwait(false);
                    if (refundFailure != null)
                        return refundFailure;

                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "A reversal (storno) already exists for this payment" },
                        DiagnosticCode = "STORNO_ALREADY_EXISTS",
                        IsDeterministicFailure = true
                    };
                }

                // Legacy: if payment was previously cancelled by old flow (IsActive=false), do not create duplicate storno
                if (!payment.IsActive)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "Payment has already been cancelled (legacy)" }
                    };
                }

                // RKSV: reversal row must carry a concrete StornoReason mapped from reason code.
                var formattedReason = PaymentReversalReasonMapper.FormatCancellationReason(reasonCode, reason);
                var stornoReason = PaymentReversalReasonMapper.ToStornoReason(reasonCode);

                var approvalGate = await _reversalApproval.EnforceApprovalAsync(
                    payment,
                    PaymentReversalOperation.Cancel,
                    null,
                    formattedReason,
                    (int)reasonCode,
                    userId,
                    approvalToken,
                    key,
                    CancellationToken.None);
                var approvalFailure = MapApprovalGateFailure(approvalGate);
                if (approvalFailure != null)
                    return approvalFailure;

                return await CreateStornoReversalAsync(payment, formattedReason, userId, key, stornoReason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while cancelling payment",
                    Errors = { ex.Message }
                };
            }
        }

        private async Task<PaymentDetails?> FindOriginalSalePaymentByReceiptNumberAsync(Guid cashRegisterId, string receiptNumber)
        {
            var key = receiptNumber.Trim();
            return await _context.PaymentDetails.AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.CashRegisterId == cashRegisterId &&
                    p.ReceiptNumber == key &&
                    p.IsActive &&
                    !p.IsStorno &&
                    !p.IsRefund &&
                    p.RksvSpecialReceiptKind == null);
        }

        private static string FormatStornoReasonForAudit(StornoReason reason) =>
            reason switch
            {
                StornoReason.None => "None",
                StornoReason.FalscherBetrag => "Falscher Betrag",
                StornoReason.KundeStorniert => "Kunde storniert",
                StornoReason.TechnischerFehler => "Technischer Fehler",
                StornoReason.Anderes => "Anderes",
                _ => reason.ToString()
            };

        /// <summary>
        /// Creates a fiscal storno (reversal) record for the given payment. Original payment is never modified.
        /// Creates: storno PaymentDetails, credit note Invoice, Receipt, TSE signature; reverts stock.
        /// </summary>
        private async Task<PaymentResult> CreateStornoReversalAsync(
            PaymentDetails payment,
            string reason,
            string userId,
            string? cancelIdempotencyKey,
            StornoReason? stornoReasonEnum = null)
        {
            var paymentId = payment.Id;
            if (payment.IsStorno)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cannot storno a storno payment",
                    Errors = { "The selected payment is already a reversal row; use the original sale payment." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_TARGET_IS_STORNO"
                };
            }

            if (!stornoReasonEnum.HasValue || stornoReasonEnum.Value == StornoReason.None)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Storno reason is required",
                    Errors = { "A valid StornoReason must be supplied for RKSV compliance." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_REASON_REQUIRED"
                };
            }

            var resolvedStornoReason = stornoReasonEnum.Value;

            if (!string.IsNullOrWhiteSpace(cancelIdempotencyKey))
            {
                var key = cancelIdempotencyKey.Trim();
                var existingByKey = await _context.PaymentDetails.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.CancelIdempotencyKey == key && p.IsStorno);
                if (existingByKey != null)
                {
                    if (!await PaymentBelongsToEffectiveTenantAsync(existingByKey))
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Payment not found",
                            Errors = { "Payment not found" },
                            IsDeterministicFailure = true
                        };
                    }

                    if (existingByKey.OriginalPaymentId == paymentId)
                    {
                        _logger.LogInformation(
                            "Idempotent storno: returning existing reversal {StornoId} for payment {PaymentId} key {Key}",
                            existingByKey.Id,
                            paymentId,
                            key);
                        await TryMarkPreorderCancelledAsync(paymentId);
                        var replayRefundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                            .ConfigureAwait(false);
                        if (replayRefundFailure != null)
                            return replayRefundFailure;
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Payment cancelled successfully",
                            Payment = existingByKey,
                            PaymentId = existingByKey.Id
                        };
                    }

                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Idempotency key already used for a different storno",
                        Errors = { "CancelIdempotencyKey is already bound to another reversal." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "STORNO_IDEMPOTENCY_KEY_CONFLICT"
                    };
                }
            }

            var hasRefundRows = await _context.PaymentDetails.AsNoTracking()
                .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsRefund && p.IsActive);
            if (hasRefundRows)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cannot storno: partial refunds already exist for this receipt",
                    Errors = { "A full storno is not allowed after partial refunds on the same sale." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_BLOCKED_BY_REFUNDS"
                };
            }

            var alreadyHasStorno = await _context.PaymentDetails.AsNoTracking()
                .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
            if (alreadyHasStorno)
            {
                var refundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                    .ConfigureAwait(false);
                if (refundFailure != null)
                    return refundFailure;

                return new PaymentResult
                {
                    Success = false,
                    Message = "Payment is already cancelled",
                    Errors = { "A reversal (storno) already exists for this payment" },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_ALREADY_EXISTS"
                };
            }

            if (payment.CashRegisterId == Guid.Empty)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Payment has no valid CashRegisterId; cannot create fiscal storno",
                    Errors = { "CashRegisterId is required for reversal" }
                };
            }

            var reg = await _context.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId);
            if (reg == null)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cash register for this payment no longer exists",
                    Errors = { "Cannot create storno: cash register was removed." }
                };
            }

            var cashRegisterId = payment.CashRegisterId;
            var registerNumber = reg.RegisterNumber;
            var originalReceipt = await _context.Receipts.AsNoTracking()
                .FirstOrDefaultAsync(r => r.PaymentId == paymentId);
            var originalInvoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.SourcePaymentId == paymentId);

            if (originalReceipt == null || originalReceipt.ReceiptId == Guid.Empty)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Original receipt is required for storno",
                    Errors = { "RKSV storno requires an existing receipt linked to the original payment." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_ORIGINAL_RECEIPT_REQUIRED"
                };
            }

            var originalItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
            var stornoItems = originalItems.Select(i => new PaymentItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = -i.UnitPrice,
                TotalPrice = -i.TotalPrice,
                TaxType = i.TaxType,
                TaxRate = i.TaxRate,
                TaxAmount = -i.TaxAmount,
                LineNet = -i.LineNet,
                Modifiers = (i.Modifiers ?? new List<PaymentItemModifierSnapshot>()).Select(m => new PaymentItemModifierSnapshot
                {
                    ModifierId = m.ModifierId,
                    Name = m.Name,
                    UnitPrice = -m.UnitPrice,
                    TotalPrice = -m.TotalPrice,
                    TaxType = m.TaxType,
                    TaxRate = m.TaxRate,
                    TaxAmount = -m.TaxAmount,
                    LineNet = -m.LineNet
                }).ToList()
            }).ToList();

            var originalTaxDetails = new Dictionary<string, decimal>();
            try
            {
                if (payment.TaxDetails?.RootElement.ValueKind == JsonValueKind.Object)
                    originalTaxDetails = JsonSerializer.Deserialize<Dictionary<string, decimal>>(payment.TaxDetails.RootElement.GetRawText()) ?? new Dictionary<string, decimal>();
            }
            catch { /* keep empty */ }
            var stornoTaxDetails = originalTaxDetails.ToDictionary(kv => kv.Key, kv => -kv.Value);

            var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync().ConfigureAwait(false);
            var countryBinding = await _countryStrategyContext.LoadAsync().ConfigureAwait(false);
            var stornoId = Guid.NewGuid();
            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
            string stornoBelegNr = string.Empty;
            Guid stornoInvoiceId = Guid.Empty;
            string signingProvider = "local";

            await using var dbTx = await _context.Database.BeginTransactionAsync();
            try
            {
                var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                stornoBelegNr = await AllocateCountryBelegNrAsync(countryBinding, dbTx, cashRegisterId, registerNumber);

                var storno = new PaymentDetails
                {
                    Id = stornoId,
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.CustomerName,
                    PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(stornoItems)),
                    TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(stornoTaxDetails)),
                    OriginalPaymentId = paymentId,
                    OriginalReceiptId = originalReceipt.ReceiptId,
                    IsRefund = false,
                    IsStorno = true,
                    StornoReason = resolvedStornoReason,
                    CancellationReason = reason,
                    CancelledAt = DateTime.UtcNow,
                    TotalAmount = -payment.TotalAmount,
                    TaxAmount = -payment.TaxAmount,
                    PaymentMethod = payment.PaymentMethod,
                    Notes = $"Storno: {reason}",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Steuernummer = payment.Steuernummer ?? string.Empty,
                    CashRegisterId = cashRegisterId,
                    CashierId = userId,
                    TableNumber = payment.TableNumber,
                    TseTimestamp = DateTime.UtcNow,
                    ReceiptNumber = stornoBelegNr,
                    CancelIdempotencyKey = cancelIdempotencyKey
                };
                CompanyProfileMapper.CopySnapshotFromOriginal(storno, payment, companyProfile);
                FiscalDocumentCountryStamp.Apply(storno, countryBinding);

                await RunPaymentHookAsync(
                    effectiveTenantId,
                    TenantExtensionHooks.PaymentBeforeSign,
                    storno.Id,
                    storno.TotalAmount,
                    stornoBelegNr);

                // Fiskaly SIGN AT: negative storno amount is signed as receipt_type=CANCELLATION
                // (PUT /cash-register/{id}/receipt/{id}) via TseService — same endpoint as the Fiskaly test page.
                TseSignatureResult sigResult;
                try
                {
                    sigResult = await FiscalTseSigning.SignAsync(
                        _tseService,
                        new FiscalSigningRequest(
                            cashRegisterId,
                            stornoBelegNr,
                            storno.TotalAmount,
                            registerNumber,
                            TaxDetailsJson: JsonSerializer.Serialize(stornoTaxDetails),
                            DbTransaction: dbTx));
                    signingProvider = sigResult.SigningProvider;
                }
                catch (Exception ex)
                {
                    await dbTx.RollbackAsync();
                    _logger.LogError(ex, "Failed to generate TSE signature for storno {StornoId}", stornoId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Failed to generate TSE signature for reversal",
                        Errors = { "TSE signature generation failed" }
                    };
                }

                storno.TseSignature = sigResult.CompactJws;
                storno.PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed;
                storno.CertificateThumbprint = sigResult.CertificateThumbprint;
                _logger.LogInformation("TSE signature generated for storno {StornoId} BelegNr {BelegNr}", stornoId, stornoBelegNr);

                stornoInvoiceId = Guid.NewGuid();
                var stornoInvoice = new Invoice
                {
                    Id = stornoInvoiceId,
                    SourcePaymentId = storno.Id,
                    InvoiceNumber = storno.ReceiptNumber,
                    InvoiceDate = storno.CreatedAt,
                    DueDate = storno.CreatedAt,
                    Status = InvoiceStatus.Paid,
                    Subtotal = storno.TotalAmount - storno.TaxAmount,
                    TaxAmount = storno.TaxAmount,
                    TotalAmount = storno.TotalAmount,
                    PaidAmount = storno.TotalAmount,
                    RemainingAmount = 0,
                    CustomerName = storno.CustomerName,
                    CustomerTaxNumber = payment.Steuernummer,
                    CompanyName = storno.CompanyName ?? companyProfile.CompanyName,
                    CompanyTaxNumber = storno.Steuernummer,
                    CompanyAddress = storno.CompanyAddress ?? companyAddress,
                    TseSignature = storno.TseSignature ?? string.Empty,
                    KassenId = registerNumber,
                    TseTimestamp = storno.TseTimestamp,
                    CashRegisterId = cashRegisterId,
                    PaymentMethod = payment.PaymentMethod,
                    PaymentReference = null,
                    PaymentDate = storno.CreatedAt,
                    InvoiceItems = storno.PaymentItems,
                    TaxDetails = storno.TaxDetails,
                    DocumentType = DocumentType.CreditNote,
                    OriginalInvoiceId = originalInvoice?.Id,
                    StornoReasonCode = resolvedStornoReason.ToString(),
                    StornoReasonText = reason,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsActive = true
                };
                FiscalDocumentCountryStamp.CopyFromPayment(stornoInvoice, storno, countryBinding);

                _context.PaymentDetails.Add(storno);
                _context.Invoices.Add(stornoInvoice);
                await _receiptService.AddReceiptFromPaymentToContextAsync(storno);

                var stornoReceiptEntity = _context.ChangeTracker.Entries<Receipt>()
                    .Select(e => e.Entity)
                    .FirstOrDefault(r => r.PaymentId == stornoId);
                if (stornoReceiptEntity == null)
                {
                    await dbTx.RollbackAsync();
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Internal error: storno receipt missing.",
                        Errors = { "Cannot finalize reversal without receipt context." }
                    };
                }

                await _voucherLedger.ApplyVoucherRefundsForStornoAsync(
                    paymentId,
                    stornoId,
                    stornoReceiptEntity.ReceiptId,
                    effectiveTenantId,
                    userId,
                    cancelIdempotencyKey,
                    CancellationToken.None);

                foreach (var item in originalItems)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == effectiveTenantId);
                    if (product != null && _inventoryOptions.EnforceStockOnSales)
                    {
                        product.StockQuantity += item.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (product != null && !_inventoryOptions.EnforceStockOnSales)
                    {
                        _logger.LogDebug(
                            "Stock enforcement disabled: storno skipped stock revert for product {ProductId} qty={Qty}",
                            item.ProductId,
                            item.Quantity);
                    }
                }

                await _context.SaveChangesAsync();
                await dbTx.CommitAsync();
            }
            catch (DbUpdateException ex) when (IsStornoUniqueViolation(ex))
            {
                await dbTx.RollbackAsync();
                _logger.LogWarning(ex, "Concurrent storno lost the unique original-payment race for {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "Payment is already cancelled",
                    Errors = { "A reversal (storno) already exists for this payment" },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_ALREADY_EXISTS"
                };
            }
            catch (DbUpdateException ex) when (IsCancelIdempotencyKeyViolation(ex))
            {
                await dbTx.RollbackAsync();
                if (cancelIdempotencyKey != null)
                {
                    var existing = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.CancelIdempotencyKey == cancelIdempotencyKey && p.IsStorno);
                    if (existing != null)
                    {
                        if (!await PaymentBelongsToEffectiveTenantAsync(existing))
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Payment not found",
                                Errors = { "Payment not found" }
                            };
                        }
                        _logger.LogInformation("Idempotent cancel (race): returning existing storno {StornoId} for key {Key}", existing.Id, cancelIdempotencyKey);
                        await TryMarkPreorderCancelledAsync(paymentId);
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Payment cancelled successfully",
                            Payment = existing
                        };
                    }
                }
                throw;
            }
            catch (ExtensionFailedException ex)
            {
                await dbTx.RollbackAsync();
                _logger.LogWarning(ex, "Tenant extension {Feature} rejected storno for payment {PaymentId}", ex.FeatureName, paymentId);
                return ExtensionRejected(ex);
            }
            catch (Exception ex)
            {
                await dbTx.RollbackAsync();
                _logger.LogError(ex, "Storno fiscal transaction failed for payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "Storno transaction failed",
                    Errors = { ex.Message }
                };
            }

            var cardRefundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                .ConfigureAwait(false);
            if (cardRefundFailure != null)
            {
                cardRefundFailure.PaymentId = stornoId;
                return cardRefundFailure;
            }

            var stornoRow = await _context.PaymentDetails.AsNoTracking()
                .FirstAsync(p => p.Id == stornoId);

            _logger.LogInformation("Storno created for payment {PaymentId} by user {UserId} (BelegNr {BelegNr}, Invoice {InvoiceId}). Original payment unchanged.",
                paymentId, userId, stornoBelegNr, stornoInvoiceId);

            await LogPaymentAuditAsync("PaymentReversal", "Payment", stornoRow.Id, userId,
                amount: stornoRow.TotalAmount,
                paymentMethod: stornoRow.PaymentMethodRaw,
                tseSignature: stornoRow.TseSignature,
                description: reason,
                responseData: new
                {
                    stornoRow.Id,
                    stornoRow.ReceiptNumber,
                    stornoRow.OriginalPaymentId,
                    stornoRow.OriginalReceiptId,
                    stornoRow.CancellationReason,
                    CreatedAt = stornoRow.CreatedAt,
                    FiskalyReceiptType = FiskalyReceiptSchemaMapper.ReceiptTypeCancellation,
                    SigningProvider = signingProvider
                });

            await LogFiskalyCancellationAuditAsync(
                stornoRow,
                userId,
                signingProvider,
                reason);

            await TryMarkPreorderCancelledAsync(paymentId);
            return new PaymentResult
            {
                Success = true,
                Message = "Payment cancelled successfully",
                Payment = stornoRow
            };
        }

        /// <summary>
        /// Ödeme iade et. Sprint 6: optional idempotencyKey — retries with same key return existing refund (no duplicate BelegNr/stock).
        /// </summary>
        public async Task<PaymentResult> RefundPaymentAsync(
            Guid paymentId,
            decimal amount,
            string reason,
            string userId,
            string? idempotencyKey = null,
            RefundReasonCode reasonCode = RefundReasonCode.Other,
            string? approvalToken = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund reason is required (minimum 5 characters)",
                        Errors = { "Reason must be at least 5 characters" },
                        DiagnosticCode = "REFUND_REASON_REQUIRED",
                        IsDeterministicFailure = true
                    };
                }

                // Demo kullanıcı kontrolü: IsDemo bayrağı (ve eski Demo rolü geriye dönük uyumluluk için)
                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment refund demo rejection: AuthenticatedUserId={AuthenticatedUserId} AuthenticatedUserEmail={AuthenticatedUserEmail} ResolvedUserId={ResolvedUserId} ResolvedUserEmail={ResolvedUserEmail} ResolvedUserRole={ResolvedUserRole} ResolvedUserIsDemo={ResolvedUserIsDemo} RejectionCode={RejectionCode} PaymentId={PaymentId}",
                        userId,
                        user?.Email ?? "",
                        user?.Id ?? "",
                        user?.Email ?? "",
                        user?.Role ?? "",
                        user?.IsDemo ?? false,
                        rejectionReason,
                        paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot refund real payments",
                        Errors = { "Demo users are restricted to test operations only" },
                        DiagnosticCode = rejectionReason
                    };
                }

                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                if (!await PaymentBelongsToEffectiveTenantAsync(payment))
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                // Sprint 6: Idempotency — if key provided and we already have a refund for this payment with this key, return it.
                var refundKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
                if (refundKey != null)
                {
                    var existingRefund = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdempotencyKey == refundKey && p.IsRefund && p.OriginalPaymentId == paymentId);
                    if (existingRefund != null)
                    {
                        if (!await PaymentBelongsToEffectiveTenantAsync(existingRefund))
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Payment not found",
                                Errors = { "Payment not found" }
                            };
                        }
                        _logger.LogInformation("Idempotent refund: returning existing refund {RefundId} for payment {PaymentId} key {Key}", existingRefund.Id, paymentId, refundKey);
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Refund processed successfully",
                            Payment = existingRefund
                        };
                    }
                }

                // Already cancelled: legacy (IsActive=false) or fiscal storno exists. Do not refund.
                var hasStorno = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
                if (!payment.IsActive || hasStorno)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cannot refund cancelled payment",
                        Errors = { "Payment has been cancelled (or has a reversal) and cannot be refunded" }
                    };
                }

                const decimal refundTol = 0.01m;
                if (amount <= 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund amount must be greater than zero",
                        Errors = { "Refund amount must be greater than zero" },
                        IsDeterministicFailure = true
                    };
                }

                var refundedSoFar = await _context.PaymentDetails.AsNoTracking()
                    .Where(p => p.OriginalPaymentId == paymentId && p.IsRefund && p.IsActive)
                    .SumAsync(p => (decimal?)-p.TotalAmount) ?? 0m;

                var remainingRefundable = payment.TotalAmount - refundedSoFar;
                if (amount > remainingRefundable + refundTol)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund amount exceeds remaining refundable amount",
                        Errors = { $"Refund amount exceeds remaining refundable amount (remaining={remainingRefundable:N2})." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "REFUND_EXCEEDS_REMAINING"
                    };
                }

                var formattedRefundReason = PaymentReversalReasonMapper.FormatRefundReason(reasonCode, reason);
                var approvalGate = await _reversalApproval.EnforceApprovalAsync(
                    payment,
                    PaymentReversalOperation.Refund,
                    amount,
                    formattedRefundReason,
                    (int)reasonCode,
                    userId,
                    approvalToken,
                    refundKey,
                    CancellationToken.None);
                var approvalFailure = MapApprovalGateFailure(approvalGate);
                if (approvalFailure != null)
                    return approvalFailure;

                decimal refundRatio = amount / payment.TotalAmount;
                decimal refundTaxAmount = -payment.TaxAmount * refundRatio;

                if (payment.CashRegisterId == Guid.Empty)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment has no valid CashRegisterId",
                        Errors = { "Cannot refund without cash register context." }
                    };
                }

                var refundReg = await _context.CashRegisters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId);
                if (refundReg == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cash register for this payment no longer exists",
                        Errors = { "Cannot create refund: cash register was removed." }
                    };
                }

                var refundCashRegisterId = payment.CashRegisterId;
                var refundRegisterNumber = refundReg.RegisterNumber;
                var originalSaleReceipt = await _context.Receipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.PaymentId == paymentId);
                var originalInvoiceForRefund = await _context.Invoices.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.SourcePaymentId == paymentId);

                if (_cardPaymentService != null)
                {
                    var (gwOk, gwCode, gwMsg) = await _cardPaymentService
                        .RefundForFiscalPaymentAsync(paymentId, amount)
                        .ConfigureAwait(false);
                    if (!gwOk)
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = gwMsg ?? "Gateway refund failed.",
                            Errors = { gwMsg ?? "Gateway refund failed." },
                            IsDeterministicFailure = true,
                            DiagnosticCode = gwCode ?? OnlinePaymentErrorCodes.GatewayRefundFailed
                        };
                    }
                }

                // Build negated PaymentItems so Receipt totals match (full or partial refund)
                var originalItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
                var refundItems = originalItems.Select(i =>
                {
                    var scale = refundRatio;
                    return new PaymentItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = -i.UnitPrice * scale,
                        TotalPrice = -i.TotalPrice * scale,
                        TaxType = i.TaxType,
                        TaxRate = i.TaxRate,
                        TaxAmount = -i.TaxAmount * scale,
                        LineNet = -i.LineNet * scale,
                        Modifiers = (i.Modifiers ?? new List<PaymentItemModifierSnapshot>()).Select(m => new PaymentItemModifierSnapshot
                        {
                            ModifierId = m.ModifierId,
                            Name = m.Name,
                            UnitPrice = -m.UnitPrice * scale,
                            TotalPrice = -m.TotalPrice * scale,
                            TaxType = m.TaxType,
                            TaxRate = m.TaxRate,
                            TaxAmount = -m.TaxAmount * scale,
                            LineNet = -m.LineNet * scale
                        }).ToList()
                    };
                }).ToList();

                // Negated tax details for refund
                var originalTaxDetails = new Dictionary<string, decimal>();
                try
                {
                    if (payment.TaxDetails?.RootElement.ValueKind == JsonValueKind.Object)
                        originalTaxDetails = JsonSerializer.Deserialize<Dictionary<string, decimal>>(payment.TaxDetails.RootElement.GetRawText()) ?? new Dictionary<string, decimal>();
                }
                catch { /* keep empty */ }
                var refundTaxDetails = originalTaxDetails.ToDictionary(kv => kv.Key, kv => -kv.Value * refundRatio);

                var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync().ConfigureAwait(false);
                var countryBinding = await _countryStrategyContext.LoadAsync().ConfigureAwait(false);
                var refundId = Guid.NewGuid();
                var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
                string refundBelegNr = string.Empty;
                Guid refundInvoiceId = Guid.Empty;

                await using var refundTx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var effectiveTenantIdForRefund = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                    refundBelegNr = await AllocateCountryBelegNrAsync(countryBinding, refundTx, refundCashRegisterId, refundRegisterNumber);

                    var refund = new PaymentDetails
                    {
                        Id = refundId,
                        CustomerId = payment.CustomerId,
                        CustomerName = payment.CustomerName,
                        PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(refundItems)),
                        TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(refundTaxDetails)),
                        OriginalPaymentId = paymentId,
                        OriginalReceiptId = originalSaleReceipt?.ReceiptId,
                        IsRefund = true,
                        RefundReason = formattedRefundReason,
                        RefundAmount = amount,
                        RefundedAt = DateTime.UtcNow,
                        TotalAmount = -amount,
                        TaxAmount = refundTaxAmount,
                        PaymentMethod = payment.PaymentMethod,
                        Notes = $"Refund: {reason}",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        Steuernummer = payment.Steuernummer ?? string.Empty,
                        CashRegisterId = refundCashRegisterId,
                        CashierId = userId,
                        TableNumber = payment.TableNumber,
                        TseTimestamp = DateTime.UtcNow,
                        ReceiptNumber = refundBelegNr,
                        IdempotencyKey = refundKey
                    };
                    CompanyProfileMapper.CopySnapshotFromOriginal(refund, payment, companyProfile);
                    FiscalDocumentCountryStamp.Apply(refund, countryBinding);

                    await RunPaymentHookAsync(
                        effectiveTenantIdForRefund,
                        TenantExtensionHooks.PaymentBeforeSign,
                        refund.Id,
                        refund.TotalAmount,
                        refundBelegNr);

                    TseSignatureResult sigResult;
                    try
                    {
                        sigResult = await FiscalTseSigning.SignAsync(
                            _tseService,
                            new FiscalSigningRequest(
                                refundCashRegisterId,
                                refundBelegNr,
                                refund.TotalAmount,
                                refundRegisterNumber,
                                TaxDetailsJson: JsonSerializer.Serialize(refundTaxDetails),
                                DbTransaction: refundTx));
                    }
                    catch (Exception ex)
                    {
                        await refundTx.RollbackAsync();
                        _logger.LogError(ex, "Failed to generate TSE signature for refund {RefundId}", refundId);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Failed to generate TSE signature for refund",
                            Errors = { "TSE signature generation failed" }
                        };
                    }

                    refund.TseSignature = sigResult.CompactJws;
                    refund.PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed;
                    refund.CertificateThumbprint = sigResult.CertificateThumbprint;
                    _logger.LogInformation("TSE signature generated for refund {RefundId} BelegNr {BelegNr}", refundId, refundBelegNr);

                    refundInvoiceId = Guid.NewGuid();
                    var refundInvoice = new Invoice
                    {
                        Id = refundInvoiceId,
                        SourcePaymentId = refund.Id,
                        InvoiceNumber = refund.ReceiptNumber,
                        InvoiceDate = refund.CreatedAt,
                        DueDate = refund.CreatedAt,
                        Status = InvoiceStatus.Paid,
                        Subtotal = refund.TotalAmount - refund.TaxAmount,
                        TaxAmount = refund.TaxAmount,
                        TotalAmount = refund.TotalAmount,
                        PaidAmount = refund.TotalAmount,
                        RemainingAmount = 0,
                        CustomerName = refund.CustomerName,
                        CustomerTaxNumber = payment.Steuernummer,
                        CompanyName = refund.CompanyName ?? companyProfile.CompanyName,
                        CompanyTaxNumber = refund.Steuernummer,
                        CompanyAddress = refund.CompanyAddress ?? companyAddress,
                        TseSignature = refund.TseSignature ?? string.Empty,
                        KassenId = refundRegisterNumber,
                        TseTimestamp = refund.TseTimestamp,
                        CashRegisterId = refundCashRegisterId,
                        PaymentMethod = payment.PaymentMethod,
                        PaymentReference = null,
                        PaymentDate = refund.CreatedAt,
                        InvoiceItems = refund.PaymentItems,
                        TaxDetails = refund.TaxDetails,
                        DocumentType = DocumentType.CreditNote,
                        OriginalInvoiceId = originalInvoiceForRefund?.Id,
                        StornoReasonText = reason,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId,
                        IsActive = true
                    };
                    FiscalDocumentCountryStamp.CopyFromPayment(refundInvoice, refund, countryBinding);

                    _context.PaymentDetails.Add(refund);
                    _context.Invoices.Add(refundInvoice);
                    await _receiptService.AddReceiptFromPaymentToContextAsync(refund);

                    if (string.Equals(payment.PaymentMethodRaw, ((int)PaymentMethod.Voucher).ToString(), StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "Voucher fiscal refund does not restore stored-value balance. Use full storno (cancellation) for voucher sales. OriginalPaymentId={PaymentId} RefundId={RefundId}",
                            paymentId,
                            refundId);
                    }

                    foreach (var item in originalItems)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == effectiveTenantIdForRefund);
                        if (product != null)
                        {
                            var refundQuantity = (int)Math.Round(item.Quantity * refundRatio);
                            if (refundQuantity > 0)
                            {
                                if (_inventoryOptions.EnforceStockOnSales)
                                {
                                    product.StockQuantity += refundQuantity;
                                    product.UpdatedAt = DateTime.UtcNow;
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Stock enforcement disabled: refund skipped stock revert for product {ProductId} qty={Qty}",
                                        item.ProductId,
                                        refundQuantity);
                                }
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await refundTx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
                {
                    await refundTx.RollbackAsync();
                    if (refundKey != null)
                    {
                        var existing = await _context.PaymentDetails.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.IdempotencyKey == refundKey && p.IsRefund && p.OriginalPaymentId == paymentId);
                        if (existing != null)
                        {
                            if (!await PaymentBelongsToEffectiveTenantAsync(existing))
                            {
                                return new PaymentResult
                                {
                                    Success = false,
                                    Message = "Payment not found",
                                    Errors = { "Payment not found" }
                                };
                            }
                            _logger.LogInformation("Idempotent refund (race): returning existing refund {RefundId} for key {Key}", existing.Id, refundKey);
                            return new PaymentResult
                            {
                                Success = true,
                                Message = "Refund processed successfully",
                                Payment = existing
                            };
                        }
                    }
                    throw;
                }
                catch (ExtensionFailedException ex)
                {
                    await refundTx.RollbackAsync();
                    _logger.LogWarning(ex, "Tenant extension {Feature} rejected refund for payment {PaymentId}", ex.FeatureName, paymentId);
                    return ExtensionRejected(ex);
                }
                catch (Exception ex)
                {
                    await refundTx.RollbackAsync();
                    _logger.LogError(ex, "Refund fiscal transaction failed for payment {PaymentId}", paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund transaction failed",
                        Errors = { ex.Message }
                    };
                }

                var refundRow = await _context.PaymentDetails.AsNoTracking()
                    .FirstAsync(p => p.Id == refundId);

                _logger.LogInformation("Refund created for payment {PaymentId} by user {UserId} for amount {Amount} (BelegNr {BelegNr}, Invoice {InvoiceId})",
                    paymentId, userId, amount, refundBelegNr, refundInvoiceId);

                await LogPaymentAuditAsync("PaymentRefunded", "Payment", refundRow.Id, userId,
                    amount: refundRow.TotalAmount,
                    paymentMethod: refundRow.PaymentMethodRaw,
                    tseSignature: refundRow.TseSignature,
                    description: reason,
                    responseData: new
                    {
                        refundRow.Id,
                        refundRow.ReceiptNumber,
                        refundRow.OriginalPaymentId,
                        refundRow.OriginalReceiptId,
                        RefundAmount = amount,
                        CreatedAt = refundRow.CreatedAt
                    });

                return new PaymentResult
                {
                    Success = true,
                    Message = "Refund processed successfully",
                    Payment = refundRow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while processing refund",
                    Errors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Get payment statistics for date range
        /// </summary>
        public Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate) =>
            _paymentQuery.GetPaymentStatisticsAsync(startDate, endDate);

        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        public async Task<string> GenerateTseSignatureAsync(PaymentDetails payment)
        {
            try
            {
                // TseMode=Device iken cihaz kontrolü; Demo modda atlanır
                if (!_tseOptions.UseSoftTseWhenNoDevice)
                {
                    var tseStatus = await _tseService.GetDeviceStatusAsync();
                    if (!tseStatus.IsConnected)
                    {
                        // Bypass: allow operation when signing mode is explicitly Fake/Simulation.
                        var isFakeOrSimulationMode =
                            _tseOptions.IsFakeSigningMode ||
                            string.Equals(_tseOptions.Mode, "Simulation", StringComparison.OrdinalIgnoreCase);

                        if (isFakeOrSimulationMode)
                        {
                            // Do not block in simulated environments; continue with warning for operator visibility.
                            _logger.LogWarning(
                                "TSE device is not connected, but operation is allowed because TSE mode is {TseMode} for payment {PaymentId}.",
                                _tseOptions.Mode,
                                payment.Id);
                        }
                        else
                        {
                            _logger.LogError("TSE device not connected. Cannot generate signature for payment {PaymentId}", payment.Id);
                            throw new InvalidOperationException("TSE device is not connected");
                        }
                    }

                    // TSE cihazı hazır mı kontrolü
                    if (!tseStatus.IsReady)
                    {
                        _logger.LogWarning("TSE device not ready. Status: {Status}", tseStatus.Status);
                        throw new InvalidOperationException($"TSE device is not ready. Status: {tseStatus.Status}");
                    }
                }

                if (payment.CashRegisterId == Guid.Empty)
                    throw new InvalidOperationException("Payment has no CashRegisterId; cannot sign.");
                var regForSign = await _context.CashRegisters.FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId)
                    ?? throw new InvalidOperationException($"Cash register {payment.CashRegisterId} not found.");

                var signTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                await RunPaymentHookAsync(
                    signTenantId,
                    TenantExtensionHooks.PaymentBeforeSign,
                    payment.Id,
                    payment.TotalAmount,
                    payment.ReceiptNumber);

                var sigResult = await FiscalTseSigning.SignAsync(
                    _tseService,
                    new FiscalSigningRequest(
                        payment.CashRegisterId,
                        payment.ReceiptNumber ?? payment.Id.ToString(),
                        payment.TotalAmount,
                        regForSign.RegisterNumber,
                        TaxDetailsJson: payment.TaxDetails?.RootElement.GetRawText()));
                var signature = sigResult.CompactJws;

                _logger.LogInformation("TSE signature generated successfully for payment {PaymentId}: {Signature}",
                    payment.Id, signature);

                return signature;
            }
            catch (ExtensionFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TSE signature for payment {PaymentId}", payment.Id);
                throw;
            }
        }

        /// <summary>
        /// FinanzOnline entegrasyonu
        /// </summary>
        public Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment) =>
            _finanzOnlineSubmission.SendToFinanzOnlineAsync(payment);

        /// <summary>Retry FinanzOnline submit for a payment (reconciliation). No-op if already Submitted.</summary>
        public Task<FinanzOnlineSubmitResponse> RetryFinanzOnlineSubmitAsync(Guid paymentId) =>
            _finanzOnlineSubmission.RetryFinanzOnlineSubmitAsync(paymentId);

        /// <summary>
        /// Returns persisted receipt for payment. Receipt is created at payment time and includes totals, tax breakdown, signature, QR payload.
        /// When userId is provided, audit is written for ReceiptGenerated or ReceiptReprinted (append-only).
        /// </summary>
        public Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId, string? userId = null) =>
            _receiptRead.GetReceiptDataAsync(paymentId, userId);

        /// <summary>
        /// Backoffice: bestätigter Nachdruck — eine strukturierte Audit-Zeile (<c>ReceiptReprintConfirmed</c> / <c>ReceiptReprintRejected</c>), kein neuer Beleg, keine TSE-Neuerzeugung.
        /// </summary>
        public Task<ReceiptReprintOperationResult> ConfirmReceiptReprintAsync(Guid paymentId, ReceiptReprintRequest? request, string userId, CancellationToken cancellationToken = default) =>
            _receiptRead.ConfirmReceiptReprintAsync(paymentId, request, userId, cancellationToken);

        #region Private Methods

        /// <summary>
        /// True when the payment's cash register belongs to the effective tenant (settings snapshot).
        /// </summary>
        private async Task<bool> PaymentBelongsToEffectiveTenantAsync(PaymentDetails payment, CancellationToken cancellationToken = default)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return await _context.CashRegisters.AsNoTracking().ForResolvedTenantScope()
                .AnyAsync(cr => cr.Id == payment.CashRegisterId && cr.TenantId == tenantId, cancellationToken);
        }

        /// <summary>Resolves user role for audit. Returns "Unknown" if user not found.</summary>
        private async Task<string> GetUserRoleAsync(string userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.Role ?? "Unknown";
        }

        /// <summary>Writes payment lifecycle audit log after successful commit. Append-only; never updates. Swallows exceptions to avoid affecting caller.</summary>
        private async Task LogPaymentAuditAsync(string action, string entityType, Guid entityId, string userId,
            decimal? amount = null, string? paymentMethod = null, string? tseSignature = null, string? description = null,
            object? responseData = null, string? correlationId = null)
        {
            try
            {
                var userRole = await GetUserRoleAsync(userId);
                await _auditLogService.LogPaymentOperationAsync(action, entityType, entityId, userId, userRole,
                    amount: amount, paymentMethod: paymentMethod, tseSignature: tseSignature,
                    correlationId: correlationId,
                    description: description, responseData: responseData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed for action {Action} EntityType={EntityType} EntityId={EntityId}; payment operation already committed.", action, entityType, entityId);
            }
        }

        /// <summary>
        /// Dedicated Fiskaly SIGN AT cancellation audit (receipt_type=CANCELLATION).
        /// Soft TSE / local signing does not create a Fiskaly dashboard receipt — skip this event then.
        /// </summary>
        private async Task LogFiskalyCancellationAuditAsync(
            PaymentDetails storno,
            string userId,
            string signingProvider,
            string reason)
        {
            if (!string.Equals(signingProvider, "fiskaly", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var userRole = await GetUserRoleAsync(userId);
                await _auditLogService.LogSystemOperationAsync(
                    action: AuditLogActions.FISKALY_CANCELLATION_RECEIPT_SIGNED,
                    entityType: "FiskalyReceipt",
                    userId: userId,
                    userRole: userRole,
                    description: "Fiskaly SIGN AT cancellation receipt signed for payment storno.",
                    status: AuditLogStatus.Success,
                    actionType: AuditEventType.FiskalyCancellationReceiptSigned,
                    entityId: storno.Id,
                    newValues: new
                    {
                        storno.Id,
                        storno.ReceiptNumber,
                        storno.OriginalPaymentId,
                        storno.OriginalReceiptId,
                        FiskalyReceiptType = FiskalyReceiptSchemaMapper.ReceiptTypeCancellation,
                        Reason = reason
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Fiskaly cancellation audit log write failed for storno {StornoId}; payment operation already committed.",
                    storno.Id);
            }
        }

        /// <summary>
        /// Returns the standard PaymentResult for daily allowance conflict (concurrency or unique-index race).
        /// Keeps contract stable for mobile POS.
        /// </summary>
        private static PaymentResult ToDailyAllowanceConflictResult() =>
            PaymentCommitFailureMapper.ToDailyAllowanceConflictResult();

        /// <summary>
        /// True when the exception is a PostgreSQL unique constraint violation (23505) on the idempotency_key column.
        /// Used to return existing payment on concurrent duplicate key insert.
        /// </summary>
        private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsIdempotencyKeyViolation(ex);

        /// <summary>
        /// Unique violation on <c>voucher_ledger_entries.idempotency_key</c> (e.g. index IX_voucher_ledger_entries_idempotency_key).
        /// </summary>
        private static bool IsVoucherLedgerIdempotencyConstraintViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsVoucherLedgerIdempotencyConstraintViolation(ex);

        /// <summary>Sprint 6: True when the exception is a unique constraint violation on cancel_idempotency_key.</summary>
        private static bool IsCancelIdempotencyKeyViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsCancelIdempotencyKeyViolation(ex);

        private static bool IsStornoUniqueViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsStornoUniqueViolation(ex);

        /// <summary>
        /// True when the exception is a PostgreSQL unique constraint violation (23505) on BenefitDailyUsage
        /// (CustomerId, BenefitDefinitionId, UsageDate) create-if-missing race. Excludes idempotency_key violations.
        /// </summary>
        private static bool IsBenefitDailyUsageConflict(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsBenefitDailyUsageConflict(ex);

        /// <summary>Walks inner exceptions for <see cref="PostgresException"/> (Npgsql may nest it).</summary>
        private static bool TryGetPostgresException(Exception ex, out PostgresException pgEx) =>
            PaymentCommitFailureMapper.TryGetPostgresException(ex, out pgEx);

        /// <summary>
        /// Maps PostgreSQL errors from payment fiscal transaction SaveChanges to a deterministic <see cref="PaymentResult"/>.
        /// </summary>
        private static bool TryMapPaymentCommitPostgresException(PostgresException pgEx, out PaymentResult result) =>
            PaymentCommitFailureMapper.TryMapPaymentCommitPostgresException(pgEx, out result);

        /// <summary>Persist FinanzOnline reconciliation state on PaymentDetails after submit (post-commit). Best-effort; does not throw. Logs FO attempt to audit for incident investigation (correlation id + retry history).</summary>
        private Task UpdatePaymentFinanzOnlineStateAsync(Guid paymentId, FinanzOnlineSubmitResponse result, bool isRetry,
            string? userIdForAudit = null,
            string? correlationIdForAudit = null) =>
            _finanzOnlineSubmission.UpdatePaymentFinanzOnlineStateAsync(
                paymentId, result, isRetry, userIdForAudit, correlationIdForAudit);

        private static FinanzOnlineFailureKind ClassifyFinanzOnlineFailure(Exception ex) =>
            PaymentFinanzOnlineSubmissionService.ClassifyFinanzOnlineFailure(ex);

        /// <summary>
        /// Marks the matching active cart <see cref="CartStatus.Completed"/> inside the open payment transaction.
        /// A second request for that cart returns <c>CART_ALREADY_PAID</c> (HTTP 409) instead of another receipt.
        /// </summary>
        private async Task<PaymentResult?> TryClaimMatchingCartForPaymentAsync(
            string userId,
            int tableNumber,
            IReadOnlyList<PaymentItemRequest> items)
        {
            if (items == null || items.Count == 0)
                return null;

            var active = await _context.Carts.AsNoTracking()
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.TableNumber == tableNumber && c.Status == CartStatus.Active)
                .ConfigureAwait(false);
            if (active != null && CartLinesMatch(active, items))
            {
                var provider = _context.Database.ProviderName ?? string.Empty;
                if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    // Single UPDATE holds the row lock until this payment transaction commits.
                    var claimed = await _context.Carts
                        .Where(c => c.CartId == active.CartId && c.Status == CartStatus.Active)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(c => c.Status, CartStatus.Completed)
                            .SetProperty(c => c.UpdatedAt, DateTime.UtcNow))
                        .ConfigureAwait(false);
                    if (claimed == 0)
                        return CartAlreadyPaid();
                    return null;
                }

                var tracked = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartId == active.CartId && c.Status == CartStatus.Active)
                    .ConfigureAwait(false);
                if (tracked == null)
                    return CartAlreadyPaid();
                tracked.Status = CartStatus.Completed;
                tracked.UpdatedAt = DateTime.UtcNow;
                return null;
            }

            if (active != null)
                return null;

            var paid = await _context.Carts.AsNoTracking()
                .Include(c => c.Items)
                .Where(c => c.UserId == userId && c.TableNumber == tableNumber && c.Status == CartStatus.Completed)
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (paid != null && CartLinesMatch(paid, items))
                return CartAlreadyPaid();

            return null;
        }

        private static PaymentResult CartAlreadyPaid() => new()
        {
            Success = false,
            Message = "Cart is already paid",
            Errors = { "This cart was already paid. A second receipt was not created." },
            IsDeterministicFailure = true,
            DiagnosticCode = "CART_ALREADY_PAID"
        };

        private static bool CartLinesMatch(Cart cart, IReadOnlyList<PaymentItemRequest> items)
        {
            if (cart.Items == null || cart.Items.Count == 0)
                return false;

            var reqByProduct = items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var cartByProduct = cart.Items.GroupBy(ci => ci.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            if (reqByProduct.Count != cartByProduct.Count)
                return false;
            foreach (var kv in reqByProduct)
            {
                if (!cartByProduct.TryGetValue(kv.Key, out var qty) || qty != kv.Value)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Card gateway refund runs only after the fiscal storno row has committed.
        /// Cash sales have no card row and return success without a gateway call.
        /// </summary>
        private async Task<PaymentResult?> RefundCardAfterFiscalStornoAsync(Guid originalPaymentId, decimal amount)
        {
            if (_cardPaymentService == null)
                return null;

            var (gwOk, gwCode, gwMsg) = await _cardPaymentService
                .RefundForFiscalPaymentAsync(originalPaymentId, amount)
                .ConfigureAwait(false);
            if (gwOk)
                return null;

            return new PaymentResult
            {
                Success = false,
                Message = gwMsg ?? "Gateway refund failed.",
                Errors = { gwMsg ?? "Gateway refund failed." },
                IsDeterministicFailure = true,
                DiagnosticCode = gwCode ?? OnlinePaymentErrorCodes.GatewayRefundFailed
            };
        }

        /// <summary>
        /// Aktif sepet satırları ödeme kalemleriyle (ürün + miktar) birebir örtüşüyorsa birim brüt fiyatları döndürür; böylece ödeme toplamı sepetteki anlık fiyatla uyumlu kalır.
        /// </summary>
        private sealed record CartPriceSnapshot(
            Dictionary<Guid, decimal> UnitPrices,
            Dictionary<Guid, string> StayNames);

        private async Task<CartPriceSnapshot?> TryGetCartSnapshotUnitPricesAsync(
            string userId,
            int tableNumber,
            IReadOnlyList<PaymentItemRequest> items)
        {
            var cart = await _context.Carts
                .AsNoTracking()
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.TableNumber == tableNumber && c.Status == CartStatus.Active);
            if (cart?.Items == null || cart.Items.Count == 0)
                return null;

            var reqByProduct = items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var cartByProduct = cart.Items.GroupBy(ci => ci.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            if (reqByProduct.Count != cartByProduct.Count)
                return null;
            foreach (var kv in reqByProduct)
            {
                if (!cartByProduct.TryGetValue(kv.Key, out var cq) || cq != kv.Value)
                    return null;
            }

            var prices = new Dictionary<Guid, decimal>();
            var stayNames = new Dictionary<Guid, string>();
            foreach (var g in cart.Items.GroupBy(ci => ci.ProductId))
            {
                var qty = g.Sum(x => x.Quantity);
                if (qty <= 0)
                    return null;
                var weighted = g.Sum(x => x.UnitPrice * x.Quantity);
                prices[g.Key] = Math.Round(weighted / qty, 2, MidpointRounding.AwayFromZero);
                var stay = g.Select(ci => ci.Notes).FirstOrDefault(LodgingStayLine.IsStayLabel);
                if (stay != null)
                    stayNames[g.Key] = stay;
            }

            return new CartPriceSnapshot(prices, stayNames);
        }


        private async Task DispatchPostCommitComplianceAsync(
            PaymentDetails createdPayment,
            Invoice createdInvoice,
            string userId,
            Guid? offlineReplayBatchCorrelationId,
            bool effectiveTseRequired)
        {
            try
            {
                // Audit: append-only, after successful commit. Best-effort; failure does not roll back payment.
                var paymentAuditCorrelation = offlineReplayBatchCorrelationId?.ToString("N");
                await LogPaymentAuditAsync("PaymentCreated", "Payment", createdPayment.Id, userId,
                    amount: createdPayment.TotalAmount,
                    paymentMethod: createdPayment.PaymentMethodRaw,
                    tseSignature: createdPayment.TseSignature,
                    correlationId: paymentAuditCorrelation,
                    responseData: new
                    {
                        createdPayment.Id,
                        createdPayment.ReceiptNumber,
                        createdPayment.TotalAmount,
                        createdPayment.CashRegisterId,
                        CreatedAt = createdPayment.CreatedAt,
                        offlineReplayBatchCorrelationId = offlineReplayBatchCorrelationId
                    });

                var persistedReceipt = await _context.Receipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.PaymentId == createdPayment.Id);
                if (persistedReceipt != null)
                {
                    await LogPaymentAuditAsync("ReceiptPersisted", "Receipt", persistedReceipt.ReceiptId, userId,
                        amount: createdPayment.TotalAmount,
                        tseSignature: createdPayment.TseSignature,
                        correlationId: paymentAuditCorrelation,
                        description: "Canonical fiscal receipt persisted with payment",
                        responseData: new
                        {
                            persistedReceipt.ReceiptId,
                            persistedReceipt.ReceiptNumber,
                            PaymentId = createdPayment.Id,
                            createdPayment.CashRegisterId,
                            offlineReplayBatchCorrelationId = offlineReplayBatchCorrelationId
                        });
                }

                // FinanzOnline: best-effort after commit; failure does not roll back DB.
                if (effectiveTseRequired)
                {
                    try
                    {
                        _finanzOnlineMetrics?.IncrementSubmitTotal();
                        var foResult = await _finanzOnlineService.SubmitInvoiceAsync(createdInvoice).ConfigureAwait(false);
                        await UpdatePaymentFinanzOnlineStateAsync(createdPayment.Id, foResult, isRetry: false,
                            userIdForAudit: userId,
                            correlationIdForAudit: offlineReplayBatchCorrelationId?.ToString("N")).ConfigureAwait(false);
                        if (foResult.Success)
                            _logger.LogInformation("Invoice sent to FinanzOnline: {InvoiceId}, ReferenceId={RefId}", createdInvoice.Id, foResult.ReferenceId);
                        else
                        {
                            _finanzOnlineMetrics?.IncrementSubmitFailed(foResult.FailureKind);
                            _logger.LogWarning("FinanzOnline submit failed for Invoice {InvoiceId}: {Error}, FailureKind={Kind}", createdInvoice.Id, foResult.ErrorMessage, foResult.FailureKind);
                        }
                    }
                    catch (Exception ex)
                    {
                        var kind = ClassifyFinanzOnlineFailure(ex);
                        _finanzOnlineMetrics?.IncrementSubmitFailed(kind);
                        _logger.LogWarning(ex, "Failed to send invoice to FinanzOnline: {InvoiceId}", createdInvoice.Id);
                        await UpdatePaymentFinanzOnlineStateAsync(createdPayment.Id, new FinanzOnlineSubmitResponse
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            SubmittedAt = DateTime.UtcNow,
                            Status = "Failed",
                            FailureKind = kind
                        }, isRetry: false,
                            userIdForAudit: userId,
                            correlationIdForAudit: offlineReplayBatchCorrelationId?.ToString("N")).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Post-commit compliance orchestration failed for payment {PaymentId}. The payment was successfully committed, but secondary actions failed.", createdPayment.Id);
            }
        }


        private static string? MapCashRegisterDiagnosticToRksvPaymentCode(string? cashRegisterResolutionCode) =>
            string.Equals(cashRegisterResolutionCode, CashRegisterResolutionCodes.Decommissioned, StringComparison.Ordinal)
                ? RksvGuardErrorCodes.RegisterDecommissioned
                : cashRegisterResolutionCode;

        private static PaymentResult? MapApprovalGateFailure(PaymentReversalApprovalGateOutcome gate) =>
            gate.Result switch
            {
                PaymentReversalApprovalGateResult.ApprovalRequired => new PaymentResult
                {
                    Success = false,
                    Message = "Manager approval is required for this reversal",
                    Errors = { "Submit the 6-digit approval token from a manager to continue" },
                    DiagnosticCode = "REVERSAL_APPROVAL_REQUIRED",
                    RequiresApproval = true,
                    ApprovalRequestId = gate.ApprovalRequestId,
                    ApprovalTokenExpiresAtUtc = gate.ExpiresAtUtc,
                    ApprovalNotificationSent = gate.NotificationSent,
                    IsDeterministicFailure = true,
                },
                PaymentReversalApprovalGateResult.InvalidToken => new PaymentResult
                {
                    Success = false,
                    Message = "Invalid or expired approval token",
                    Errors = { "Approval token is invalid, expired, or does not match the pending request" },
                    DiagnosticCode = "REVERSAL_APPROVAL_INVALID",
                    IsDeterministicFailure = true,
                },
                _ => null,
            };

        private static bool IsCardLegacyPayment(string legacyRaw) =>
            string.Equals(legacyRaw, ((int)PaymentMethod.Card).ToString(), StringComparison.Ordinal);

        #endregion
    }
}

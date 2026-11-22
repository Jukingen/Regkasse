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
        private readonly IPaymentStornoService _storno;
        private readonly IPaymentRefundService _refund;
        private readonly IPaymentSaleGateService _saleGate;
        private readonly IPaymentSettlementService _settlement;
        private readonly IPaymentDocumentPersistenceService _documents;
        private readonly IPaymentPostCommitService _postCommit;

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
            IPaymentVoucherLedgerService? voucherLedger = null,
            IPaymentStornoService? storno = null,
            IPaymentRefundService? refund = null,
            IPaymentSaleGateService? saleGate = null,
            IPaymentSettlementService? settlement = null,
            IPaymentDocumentPersistenceService? documents = null,
            IPaymentPostCommitService? postCommit = null)
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
            _storno = storno ?? new PaymentStornoService(
                _context,
                _paymentRepository,
                _userService,
                _receiptService,
                _companyProfileProvider,
                _settingsTenantResolver,
                _countryStrategyContext,
                _inventoryOptions,
                _tseService,
                _voucherLedger,
                _reversalApproval,
                _logger,
                _cardPaymentService,
                _preorderService,
                PaymentBelongsToEffectiveTenantAsync,
                AllocateCountryBelegNrAsync,
                RunPaymentHookAsync,
                LogPaymentAuditAsync,
                LogFiskalyCancellationAuditAsync);
            _refund = refund ?? new PaymentRefundService(
                _context,
                _paymentRepository,
                _userService,
                _receiptService,
                _companyProfileProvider,
                _settingsTenantResolver,
                _countryStrategyContext,
                _inventoryOptions,
                _tseService,
                _reversalApproval,
                _logger,
                _cardPaymentService,
                PaymentBelongsToEffectiveTenantAsync,
                AllocateCountryBelegNrAsync,
                RunPaymentHookAsync,
                LogPaymentAuditAsync);
            _saleGate = saleGate ?? new PaymentSaleGateService(
                _context,
                _logger,
                _settingsTenantResolver,
                _companyProfileProvider,
                _countryStrategyContext,
                _taxStrategyResolver,
                _cashRegisterResolution,
                _httpContextAccessor,
                _offlineIntents,
                _tseOptions,
                _tseHealthMonitor,
                _featureFlags,
                _developmentModeService,
                _hostEnvironment,
                _developmentOptions,
                _verticalProfiles,
                _profileGuard);
            _settlement = settlement ?? new PaymentSettlementService(
                _logger,
                _paymentMethodCatalog,
                _voucherLedger,
                _tseOptions,
                _ntpEffectiveSettings,
                _ntpTimeSyncStatus,
                _cardPaymentService,
                _preorderService,
                _tenantLimitGuard);
            _documents = documents ?? new PaymentDocumentPersistenceService(
                _context,
                _logger,
                _auditLogService,
                _fiscalSignatureRouter,
                _receiptService,
                _voucherLedger,
                AllocateCountryBelegNrAsync,
                _ticketRedemptions,
                _extensionRegistry,
                _profileGuard,
                _verticalProfiles);
            _postCommit = postCommit ?? new PaymentPostCommitService(
                _context,
                _logger,
                _auditLogService,
                _finanzOnlineService,
                _finanzOnlineSubmission,
                _inventoryOptions,
                LogPaymentAuditAsync,
                BuildQrPayloadAndFlagsAsync,
                _finanzOnlineMetrics,
                _cardPaymentService);
        }

        internal Task RunPaymentHookAsync(
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


        internal static PaymentResult ExtensionRejected(ExtensionFailedException ex) =>
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

        internal async Task<string> AllocateCountryBelegNrAsync(
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

        /// <summary>
        /// Read-only eligibility preview: which benefits would apply for this customer and cart. No persistence (no BenefitDailyUsage write, no payment).
        /// </summary>
        public Task<BenefitEligibilityPreviewResponse?> ComputeBenefitEligibilityPreviewAsync(BenefitEligibilityPreviewRequest request) =>
            _benefits.ComputeBenefitEligibilityPreviewAsync(request);


        /// <summary>
        /// QR payload (RKSV belegdaten veya NON_FISCAL_DEMO) ve demo/fiscal flag'leri üretir.
        /// tseRequired=false: Açık NON_FISCAL marker ile UI yanlışlıkla fiskal sanmasın.
        /// </summary>
        internal Task<(string QrPayload, bool IsDemoFiscal, string TseProvider)> BuildQrPayloadAndFlagsAsync(
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

        /// <summary>
        /// Cancel payment via fiscal storno (reversal). Original payment is NEVER modified; a reversal record is created with TSE signature and credit note.
        /// Sprint 6: optional idempotencyKey — retries with same key return existing storno.
        /// </summary>
        public Task<PaymentResult> CancelPaymentAsync(
            Guid paymentId,
            string reason,
            string userId,
            string? idempotencyKey = null,
            CancellationReasonCode reasonCode = CancellationReasonCode.Other,
            string? approvalToken = null) =>
            _storno.CancelPaymentAsync(paymentId, reason, userId, idempotencyKey, reasonCode, approvalToken);

        /// <summary>
        /// Ödeme iade et. Sprint 6: optional idempotencyKey — retries with same key return existing refund (no duplicate BelegNr/stock).
        /// </summary>
        public Task<PaymentResult> RefundPaymentAsync(
            Guid paymentId,
            decimal amount,
            string reason,
            string userId,
            string? idempotencyKey = null,
            RefundReasonCode reasonCode = RefundReasonCode.Other,
            string? approvalToken = null) =>
            _refund.RefundPaymentAsync(paymentId, amount, reason, userId, idempotencyKey, reasonCode, approvalToken);


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
        internal async Task<bool> PaymentBelongsToEffectiveTenantAsync(PaymentDetails payment, CancellationToken cancellationToken = default)
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
        internal async Task LogPaymentAuditAsync(string action, string entityType, Guid entityId, string userId,
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
        internal async Task LogFiskalyCancellationAuditAsync(
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


        private static string? MapCashRegisterDiagnosticToRksvPaymentCode(string? cashRegisterResolutionCode) =>
            string.Equals(cashRegisterResolutionCode, CashRegisterResolutionCodes.Decommissioned, StringComparison.Ordinal)
                ? RksvGuardErrorCodes.RegisterDecommissioned
                : cashRegisterResolutionCode;

        internal static PaymentResult? MapApprovalGateFailure(PaymentReversalApprovalGateOutcome gate) =>
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

        #endregion
    }
}

using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Stages the Beleg number, signature, payment, invoice, receipt, tickets, and sold IMEI rows
/// on the caller's open fiscal transaction. Does not begin a transaction, call SaveChanges, or commit.
/// The signature-chain row lock stays on that transaction until the caller commits.
/// </summary>
public interface IPaymentDocumentPersistenceService
{
    Task<DocumentPhase<PersistedFiscalDocuments>> PersistFiscalDocumentsAsync(
        CreatePaymentRequest request,
        string userId,
        IDbContextTransaction transaction,
        PaymentPreconditions preconditions,
        PreparedFiscalSale sale,
        SaleTotals totals,
        SaleSettlement settlement,
        Guid cashRegisterId,
        string registerNumber,
        string? idempotencyKey,
        Guid? offlineTransactionId,
        Guid? offlineReplayBatchCorrelationId);

    Task<(TaxCalculationResult Tax, Dictionary<string, decimal> Details)> ApplyBeforeSignCartAsync(
        Guid tenantId,
        PaymentDetails payment,
        List<PaymentItem> paymentItems,
        TaxCalculationResult taxResult,
        Dictionary<string, decimal> taxDetails,
        ITaxStrategy taxStrategy,
        CountryStrategyBinding countryBinding,
        ICountryTaxTypeRegistry rateCatalog,
        string? buyerVatId,
        IReadOnlyDictionary<Guid, bool> requiresPrescriptionByProduct);
}

/// <summary>Stop-or-continue result. The caller rolls back the fiscal transaction when <see cref="Stop"/> is set.</summary>
public sealed class DocumentPhase<T>
{
    public PaymentResult? Stop { get; init; }
    public T? Value { get; init; }

    public static DocumentPhase<T> Ok(T value) => new() { Value = value };

    public static DocumentPhase<T> Fail(PaymentResult error) => new() { Stop = error };
}

public sealed class SaleTotals
{
    public required Guid EffectiveTenantId { get; init; }
    public required List<PaymentItem> PaymentItems { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal TotalTaxAmount { get; init; }
    public required Dictionary<string, decimal> TaxDetails { get; init; }
    public JsonDocument? AppliedBenefitsSnapshot { get; init; }
    public required TaxCalculationResult TaxResult { get; init; }
    public required List<(PaymentItemRequest Item, Product Product, decimal UnitGross)> PricedLines { get; init; }
    public required List<ProductImei> PendingImeiRows { get; init; }
    public required int PaymentStockLinesSkipped { get; init; }
    public required ICountryTaxTypeRegistry RateCatalog { get; init; }
}

public sealed class PersistedFiscalDocuments
{
    public required PaymentDetails Payment { get; init; }
    public required Invoice Invoice { get; init; }
    public required IReadOnlyList<IssuedTicketDto> IssuedTickets { get; init; }
    public required Guid EffectiveTenantId { get; init; }
}

public delegate Task<string> CountryBelegAllocator(
    CountryStrategyBinding countryBinding,
    IDbContextTransaction transaction,
    Guid cashRegisterId,
    string registerNumber);

public sealed class PaymentDocumentPersistenceService : IPaymentDocumentPersistenceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly IFiscalSignatureRouter _fiscalSignatureRouter;
    private readonly IReceiptService _receiptService;
    private readonly IPaymentVoucherLedgerService _voucherLedger;
    private readonly CountryBelegAllocator _allocateBelegNr;
    private readonly ITicketRedemptionService? _ticketRedemptions;
    private readonly ITenantExtensionRegistry? _extensionRegistry;
    private readonly IVerticalProfileGuard? _profileGuard;
    private readonly IVerticalProfileService? _verticalProfiles;

    public PaymentDocumentPersistenceService(
        AppDbContext context,
        ILogger<PaymentService> logger,
        IAuditLogService auditLogService,
        IFiscalSignatureRouter fiscalSignatureRouter,
        IReceiptService receiptService,
        IPaymentVoucherLedgerService voucherLedger,
        CountryBelegAllocator allocateBelegNr,
        ITicketRedemptionService? ticketRedemptions = null,
        ITenantExtensionRegistry? extensionRegistry = null,
        IVerticalProfileGuard? profileGuard = null,
        IVerticalProfileService? verticalProfiles = null)
    {
        _context = context;
        _logger = logger;
        _auditLogService = auditLogService;
        _fiscalSignatureRouter = fiscalSignatureRouter;
        _receiptService = receiptService;
        _voucherLedger = voucherLedger;
        _allocateBelegNr = allocateBelegNr;
        _ticketRedemptions = ticketRedemptions;
        _extensionRegistry = extensionRegistry;
        _profileGuard = profileGuard;
        _verticalProfiles = verticalProfiles;
    }
    public async Task<DocumentPhase<PersistedFiscalDocuments>> PersistFiscalDocumentsAsync(
        CreatePaymentRequest request,
        string userId,
        IDbContextTransaction transaction,
        PaymentPreconditions preconditions,
        PreparedFiscalSale sale,
        SaleTotals totals,
        SaleSettlement settlement,
        Guid cashRegisterId,
        string registerNumber,
        string? idempotencyKey,
        Guid? offlineTransactionId,
        Guid? offlineReplayBatchCorrelationId)
    {
        var totalAmount = totals.TotalAmount;
        var totalTaxAmount = totals.TotalTaxAmount;
        var taxDetails = totals.TaxDetails;
        var taxResult = totals.TaxResult;
        var paymentItems = totals.PaymentItems;
        var effectiveTseRequired = settlement.EffectiveTseRequired;

        // Single database transaction: register row lock + stock + BelegNr + payment + invoice + receipt commit together.
        // Sprint 1: Allocate BelegNr within this transaction so it commits or rolls back with payment/invoice/receipt/stock.
        var preReceiptNumber = !string.IsNullOrWhiteSpace(request.ReservedReceiptNumber)
            ? request.ReservedReceiptNumber.Trim()
            : await _allocateBelegNr(sale.CountryBinding, transaction, cashRegisterId, registerNumber);

        if (!string.IsNullOrWhiteSpace(request.ReservedReceiptNumber))
        {
            var reservedTaken = await _context.PaymentDetails
                .AnyAsync(p => p.ReceiptNumber == preReceiptNumber && p.IsActive);
            if (reservedTaken)
            {
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Reserved receipt number is already in use.",
                    Errors = { "Reserved receipt number conflict." },
                    IsDeterministicFailure = false,
                    DiagnosticCode = "RECEIPT_NUMBER_CONFLICT"
                });
            }
        }

        // Cash tender is operational only. It is not part of the RKSV signature or the TSE payload.
        decimal? cashGiven = null;
        decimal? changeDue = null;
        var isCashSale = !request.IsStorno
            && !request.IsRefund
            && string.Equals(
                settlement.MethodResolution.LegacyRaw,
                ((int)PaymentMethod.Cash).ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        if (isCashSale && request.CashGiven is decimal tendered)
        {
            cashGiven = decimal.Round(tendered, 2, MidpointRounding.AwayFromZero);
            var due = decimal.Round(cashGiven.Value - totalAmount, 2, MidpointRounding.AwayFromZero);
            changeDue = due < 0m ? 0m : due;
        }

        // Ödeme detayları oluştur (totals = CartMoneyHelper toplamları; optional customer % discount already applied above)
        var payment = new PaymentDetails
        {
            CustomerId = sale.Customer.Id,
            CustomerName = sale.Customer.Name,
            PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(paymentItems)),
            TotalAmount = totalAmount,
            TaxAmount = totalTaxAmount,
            CashGiven = cashGiven,
            ChangeDue = changeDue,
            TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(taxDetails)),
            PaymentMethodRaw = settlement.MethodResolution.LegacyRaw,
            Notes = request.Notes,
            PrescriptionReference = preconditions.PrescriptionReference,
            RouteFrom = preconditions.RouteFrom,
            RouteTo = preconditions.RouteTo,
            RouteKm = preconditions.RouteKm,
            TripStartedAtUtc = preconditions.TripStartedAtUtc,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            TableNumber = request.TableNumber,
            CashierId = userId,
            Steuernummer = sale.EffectiveSteuernummer,
            CompanyName = sale.CompanyProfile.CompanyName,
            CompanyAddress = CompanyProfileMapper.FormatCompanyAddress(
                sale.CompanyProfile.Street, sale.CompanyProfile.ZipCode, sale.CompanyProfile.City),
            CashRegisterId = cashRegisterId,
            TseTimestamp = DateTime.UtcNow,
            IsPrinted = false,
            ReceiptNumber = preReceiptNumber,
            AppliedBenefitsSnapshot = totals.AppliedBenefitsSnapshot,
            IdempotencyKey = idempotencyKey,
            OfflineTransactionId = offlineTransactionId,
            OfflineReplayBatchCorrelationId = offlineReplayBatchCorrelationId,
            TimeSyncWarning = settlement.TimeSyncWarning,
            TransactionId = settlement.ValidatedCardTransaction?.GatewayTransactionId,
            Provider = settlement.ValidatedCardTransaction?.Gateway
        };
        FiscalDocumentCountryStamp.Apply(payment, sale.CountryBinding);

        if (sale.CountryBinding.Profile.FiscalSystem == FiscalSystem.MWST_CH)
        {
            try
            {
                var chSign = await _fiscalSignatureRouter.SignAsync(
                    new FiscalSignatureContext(
                        sale.CountryBinding,
                        taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                            line.UnitPriceGross,
                            1,
                            line.TaxRate * 100m)).ToList(),
                        payment.TotalAmount,
                        preReceiptNumber));
                payment.SwissQrText = chSign.SwissQrText;
                _logger.LogInformation(
                    "CH_MWST QR built. TenantId={TenantId} Receipt={Receipt} Provider={Provider} Vat={Vat}",
                    sale.CountryBinding.Settings.TenantId,
                    preReceiptNumber,
                    chSign.Provider,
                    chSign.TotalVat);
                try
                {
                    await _auditLogService.LogSystemOperationAsync(
                        action: "CH_MWST_QR_BUILT",
                        entityType: "Payment",
                        userId: userId,
                        userRole: "Cashier",
                        description: "CH MWST QR payload built for canary tenant",
                        actionType: AuditEventType.ChMwstQrBuilt,
                        entityId: payment.Id,
                        tenantId: sale.CountryBinding.Settings.TenantId);
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "CH_MWST audit write failed for {Receipt}", preReceiptNumber);
                }
            }
            catch (FeatureDisabledException ex)
            {
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Swiss MWST is not enabled",
                    Errors = { ex.Message },
                    DiagnosticCode = "CH_FLAG_OFF"
                });
            }
            catch (ChMwstCanaryRejectedException ex)
            {
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Swiss MWST canary rejected the tenant",
                    Errors = { ex.Message },
                    DiagnosticCode = "CH_NOT_CANARY"
                });
            }
        }
        else if (sale.CountryBinding.Profile.Code == CountryProfileCodes.EuDefault)
        {
            try
            {
                var euSign = await _fiscalSignatureRouter.SignAsync(
                    new FiscalSignatureContext(
                        sale.CountryBinding,
                        taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                            line.UnitPriceGross,
                            1,
                            line.TaxRate * 100m)).ToList(),
                        payment.TotalAmount,
                        preReceiptNumber,
                        BuyerVatId: request.Steuernummer));
                _logger.LogInformation(
                    "EN_16931 Peppol path. TenantId={TenantId} Receipt={Receipt} Status={Status}",
                    sale.CountryBinding.Settings.TenantId,
                    preReceiptNumber,
                    euSign.PeppolStatus);
            }
            catch (FeatureDisabledException ex)
            {
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "EN 16931 is not enabled",
                    Errors = { ex.Message },
                    DiagnosticCode = "EU_FLAG_OFF"
                });
            }
        }
        else if (sale.CountryBinding.Profile.FiscalSystem == FiscalSystem.RKSV_AT && effectiveTseRequired)
        {
            var requiresPrescriptionByProduct = new Dictionary<Guid, bool>();
            foreach (var priced in totals.PricedLines)
                requiresPrescriptionByProduct[priced.Product.Id] = priced.Product.RequiresPrescription;

            (taxResult, taxDetails) = await ApplyBeforeSignCartAsync(
                totals.EffectiveTenantId,
                payment,
                paymentItems,
                taxResult,
                taxDetails,
                sale.TaxStrategy,
                sale.CountryBinding,
                totals.RateCatalog,
                request.Steuernummer,
                requiresPrescriptionByProduct);
            totalAmount = payment.TotalAmount;
            totalTaxAmount = payment.TaxAmount;
            try
            {
                var atSign = await _fiscalSignatureRouter.SignAsync(
                    new FiscalSignatureContext(
                        sale.CountryBinding,
                        taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                            line.UnitPriceGross,
                            1,
                            line.TaxRate * 100m)).ToList(),
                        payment.TotalAmount,
                        preReceiptNumber,
                        CashRegisterId: cashRegisterId,
                        RegisterNumber: registerNumber,
                        TaxDetailsJson: JsonSerializer.Serialize(taxDetails),
                        DbTransaction: transaction));
                payment.TseSignature = atSign.Signature!;
                payment.PrevSignatureValueUsed = atSign.PrevSignatureValue;
                payment.CertificateThumbprint = atSign.CertificateThumbprint;
                _logger.LogInformation(
                    "TSE signature generated for payment {PaymentId} provider={Provider}",
                    payment.Id,
                    atSign.Provider);
            }
            catch (TseUnavailableException ex)
            {
                _logger.LogError(
                    ex,
                    "TSE unavailable while signing payment (BelegNr={BelegNr}, paymentDraftId={PaymentId}).",
                    preReceiptNumber,
                    payment.Id);
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "TSE is not available",
                    Errors = { ex.Message },
                    DiagnosticCode = "TSE_UNAVAILABLE"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to generate TSE signature (BelegNr={BelegNr}, effectiveTseRequired={Effective}, clientTseRequested={ClientTse}, paymentDraftId={PaymentId}). InnerException surface in logs from TseService phase.",
                    preReceiptNumber,
                    effectiveTseRequired,
                    request.Payment.TseRequired,
                    payment.Id);
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Failed to generate TSE signature",
                    Errors = { "TSE signature generation failed" }
                });
            }
        }
        else if (sale.CountryBinding.Profile.FiscalSystem == FiscalSystem.KASSENSICHERHEIT_DE)
        {
            var deSign = await _fiscalSignatureRouter.SignAsync(
                new FiscalSignatureContext(
                    sale.CountryBinding,
                    taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                        line.UnitPriceGross,
                        1,
                        line.TaxRate * 100m)).ToList(),
                    payment.TotalAmount,
                    preReceiptNumber,
                    CashRegisterId: cashRegisterId,
                    RegisterNumber: registerNumber,
                    TaxDetailsJson: JsonSerializer.Serialize(taxDetails),
                    PaymentMethodRaw: payment.PaymentMethodRaw,
                    PaymentId: payment.Id,
                    DbTransaction: transaction));
            payment.CountryCodeAtIssue = "DE";
        }

        var companyAddress = payment.CompanyAddress
            ?? CompanyProfileMapper.FormatCompanyAddress(
                sale.CompanyProfile.Street, sale.CompanyProfile.ZipCode, sale.CompanyProfile.City);

        var posInvoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SourcePaymentId = payment.Id,
            InvoiceNumber = payment.ReceiptNumber,
            InvoiceDate = payment.CreatedAt,
            DueDate = payment.CreatedAt,
            Status = InvoiceStatus.Paid,
            Subtotal = payment.TotalAmount - payment.TaxAmount,
            TaxAmount = payment.TaxAmount,
            TotalAmount = payment.TotalAmount,
            PaidAmount = payment.TotalAmount,
            RemainingAmount = 0,
            CustomerName = payment.CustomerName,
            CustomerTaxNumber = payment.Steuernummer,
            CompanyName = payment.CompanyName ?? sale.CompanyProfile.CompanyName,
            CompanyTaxNumber = payment.Steuernummer,
            CompanyAddress = companyAddress,
            TseSignature = payment.TseSignature ?? string.Empty,
            KassenId = registerNumber,
            TseTimestamp = payment.TseTimestamp,
            CashRegisterId = cashRegisterId,
            PaymentMethod = payment.PaymentMethod,
            PaymentReference = payment.TransactionId,
            PaymentDate = payment.CreatedAt,
            InvoiceItems = payment.PaymentItems,
            TaxDetails = payment.TaxDetails,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        FiscalDocumentCountryStamp.CopyFromPayment(posInvoice, payment, sale.CountryBinding);

        _context.PaymentDetails.Add(payment);
        IReadOnlyList<IssuedTicketDto> issuedTickets = [];
        if (_ticketRedemptions is not null
            && await TenantIssuesTicketsAsync(CancellationToken.None).ConfigureAwait(false)
            && !request.IsStorno
            && !request.IsRefund)
        {
            var ticketLines = totals.PricedLines
                .Where(line => line.Product.IsTicket)
                .Select(line => (line.Product, line.Item.Quantity))
                .ToList();
            if (ticketLines.Count > 0)
            {
                issuedTickets = await _ticketRedemptions
                    .IssueForSaleAsync(
                        totals.EffectiveTenantId,
                        payment.Id,
                        ticketLines,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        foreach (var imeiRow in totals.PendingImeiRows)
        {
            imeiRow.Status = ProductImeiStatus.Sold;
            imeiRow.SoldPaymentId = payment.Id;
            imeiRow.SoldAtUtc = DateTime.UtcNow;
        }

        _context.Invoices.Add(posInvoice);
        await _receiptService.AddReceiptFromPaymentToContextAsync(payment);

        if (settlement.VoucherRedeemLines is { Count: > 0 })
        {
            var receiptEntity = _context.ChangeTracker.Entries<Receipt>()
                .Select(e => e.Entity)
                .FirstOrDefault(r => r.PaymentId == payment.Id);
            if (receiptEntity == null)
            {
                return DocumentPhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Internal error: receipt not attached for voucher redemption.",
                    Errors = { "Receipt creation failed before voucher ledger." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_RECEIPT_MISSING"
                });
            }

            var voucherApplyError = await _voucherLedger.ApplyVoucherRedemptionsInCurrentTransactionAsync(
                totals.EffectiveTenantId,
                userId,
                payment,
                receiptEntity.ReceiptId,
                settlement.VoucherRedeemLines,
                idempotencyKey,
                CancellationToken.None);
            if (voucherApplyError != null)
                return DocumentPhase<PersistedFiscalDocuments>.Fail(voucherApplyError);
        }

        // Stock updates: when EnforceStockOnSales is true, product entities were modified in the loop (tracked); SaveChanges persists them with payment/invoice/receipt in one commit.
        return DocumentPhase<PersistedFiscalDocuments>.Ok(new PersistedFiscalDocuments
        {
            Payment = payment,
            Invoice = posInvoice,
            IssuedTickets = issuedTickets,
            EffectiveTenantId = totals.EffectiveTenantId,
        });
    }


    public async Task<(TaxCalculationResult Tax, Dictionary<string, decimal> Details)> ApplyBeforeSignCartAsync(
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
}

using System.Globalization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Services.Preorder;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Tender, card-intent, voucher-plan, pre-order, tenant-limit, and NTP checks.
/// Runs on the caller's open fiscal transaction. Does not begin a transaction,
/// call SaveChanges, or commit. Voucher ledger rows stay in document persistence.
/// </summary>
public interface IPaymentSettlementService
{
    Task<SettlementPhase<SaleSettlement>> ResolveSettlementAsync(
        CreatePaymentRequest request,
        PaymentPreconditions preconditions,
        PreparedFiscalSale sale,
        decimal totalAmount,
        Guid effectiveTenantId,
        Guid cashRegisterId,
        Guid? offlineTransactionId,
        CancellationToken cancellationToken);
}

/// <summary>Stop-or-continue result. The caller rolls back the fiscal transaction when <see cref="Stop"/> is set.</summary>
public sealed class SettlementPhase<T>
{
    public PaymentResult? Stop { get; init; }
    public T? Value { get; init; }

    public static SettlementPhase<T> Ok(T value) => new() { Value = value };

    public static SettlementPhase<T> Fail(PaymentResult error) => new() { Stop = error };
}

public sealed class SaleSettlement
{
    public required PaymentMethodResolutionResult MethodResolution { get; init; }
    public CardPaymentTransaction? ValidatedCardTransaction { get; init; }
    public List<VoucherRedeemLine>? VoucherRedeemLines { get; init; }
    public required bool TimeSyncWarning { get; init; }
    public required bool EffectiveTseRequired { get; init; }
}

public sealed class PaymentSettlementService : IPaymentSettlementService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentMethodCatalogService _paymentMethodCatalog;
    private readonly IPaymentVoucherLedgerService _voucherLedger;
    private readonly TseOptions _tseOptions;
    private readonly INtpEffectiveSettingsProvider _ntpEffectiveSettings;
    private readonly INtpTimeSyncStatus _ntpTimeSyncStatus;
    private readonly ICardPaymentService? _cardPaymentService;
    private readonly IPreorderService? _preorderService;
    private readonly ITenantLimitGuard? _tenantLimitGuard;

    public PaymentSettlementService(
        ILogger<PaymentService> logger,
        IPaymentMethodCatalogService paymentMethodCatalog,
        IPaymentVoucherLedgerService voucherLedger,
        TseOptions tseOptions,
        INtpEffectiveSettingsProvider ntpEffectiveSettings,
        INtpTimeSyncStatus ntpTimeSyncStatus,
        ICardPaymentService? cardPaymentService = null,
        IPreorderService? preorderService = null,
        ITenantLimitGuard? tenantLimitGuard = null)
    {
        _logger = logger;
        _paymentMethodCatalog = paymentMethodCatalog;
        _voucherLedger = voucherLedger;
        _tseOptions = tseOptions;
        _ntpEffectiveSettings = ntpEffectiveSettings;
        _ntpTimeSyncStatus = ntpTimeSyncStatus;
        _cardPaymentService = cardPaymentService;
        _preorderService = preorderService;
        _tenantLimitGuard = tenantLimitGuard;
    }

    public async Task<SettlementPhase<SaleSettlement>> ResolveSettlementAsync(
        CreatePaymentRequest request,
        PaymentPreconditions preconditions,
        PreparedFiscalSale sale,
        decimal totalAmount,
        Guid effectiveTenantId,
        Guid cashRegisterId,
        Guid? offlineTransactionId,
        CancellationToken cancellationToken)
    {
        var effectiveTseRequired = sale.EffectiveTseRequired;
        if (request.PreorderBalanceOrderId is Guid preorderBalanceId && preorderBalanceId != Guid.Empty)
        {
            if (_preorderService is null)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Pre-order balance payment is not available.",
                    Errors = { "Pre-order balance payment is not available." },
                    DiagnosticCode = "PREORDER_UNAVAILABLE"
                });
            }

            if (request.IsPreorder)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Pre-order create and balance payment cannot be combined.",
                    Errors = { "IsPreorder and PreorderBalanceOrderId cannot both be set." },
                    DiagnosticCode = "PREORDER_BALANCE_EXCLUSIVE"
                });
            }

            var balanceGuard = await _preorderService.ValidateBalancePaymentAsync(
                preorderBalanceId,
                totalAmount,
                cancellationToken).ConfigureAwait(false);
            if (!balanceGuard.Ok)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = balanceGuard.Message ?? "Pre-order balance payment is not allowed.",
                    Errors = { balanceGuard.Message ?? "Pre-order balance payment is not allowed." },
                    DiagnosticCode = balanceGuard.Code
                });
            }
        }

        var limitRejection = await TryRejectSaleForTenantLimitsAsync(
                preconditions.TenantIdForFlags,
                totalAmount,
                offlineTransactionId,
                cancellationToken)
            .ConfigureAwait(false);
        if (limitRejection != null)
            return SettlementPhase<SaleSettlement>.Fail(limitRejection);

        var methodResolution = await _paymentMethodCatalog.ResolveForPaymentAsync(
            request.Payment.Method,
            cashRegisterId);
        if (!methodResolution.Ok)
        {
            return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
            {
                Success = false,
                Message = methodResolution.ErrorMessage ?? "Invalid payment method.",
                Errors = { methodResolution.ErrorMessage ?? "Invalid payment method." }
            });
        }

        // POS sends method code "voucher". If tenant catalog maps code voucher to a non-voucher legacy value, reject before committing (no silent skip of Gutschein redemption).
        if (sale.VoucherLikelyFromClientMethod
            && methodResolution.MatchedPaymentMethodDefinition
            && !_voucherLedger.IsVoucherLegacyPayment(methodResolution.LegacyRaw))
        {
            var expectedLegacy = ((int)PaymentMethod.Voucher).ToString(CultureInfo.InvariantCulture);
            _logger.LogError(
                "Payment rejected: payment_method_definitions has code voucher but legacy_payment_method_value={Legacy} (expected {Expected}). Fix tenant catalog.",
                methodResolution.LegacyRaw,
                expectedLegacy);
            return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
            {
                Success = false,
                Message =
                    $"Voucher payment method is misconfigured: catalog code \"voucher\" must map to legacy value {expectedLegacy} (PaymentMethod.Voucher).",
                Errors =
                {
                    $"Voucher catalog misconfiguration: code voucher must use legacy_payment_method_value {expectedLegacy}."
                },
                IsDeterministicFailure = true,
                DiagnosticCode = "VOUCHER_LEGACY_MISMATCH"
            });
        }

        var isVoucherMethodResolved = _voucherLedger.IsVoucherLegacyPayment(methodResolution.LegacyRaw);
        var expectedVoucherTotal = decimal.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
        // Gateway intents capture the remainder after voucher, never the cart gross.
        var expectedGatewayAmount = expectedVoucherTotal;
        if (!isVoucherMethodResolved && sale.HasVoucherPayload)
        {
            if (!request.Payment.Amount.HasValue)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Settlement amount is required when voucher redemption is combined with a non-voucher payment method.",
                    Errors =
                    {
                        "Settlement amount is required when voucher redemption is combined with a non-voucher payment method."
                    },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_MIXED_SETTLEMENT_AMOUNT_REQUIRED"
                });
            }

            var settlementAmount = decimal.Round(request.Payment.Amount.Value, 2, MidpointRounding.AwayFromZero);
            if (settlementAmount < 0)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Settlement amount cannot be negative.",
                    Errors = { "Settlement amount cannot be negative." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_MIXED_SETTLEMENT_NEGATIVE"
                });
            }

            if (settlementAmount > totalAmount + 0.01m)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Settlement amount cannot exceed total amount.",
                    Errors = { "Settlement amount cannot exceed total amount." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_MIXED_SETTLEMENT_EXCEEDS_TOTAL"
                });
            }

            expectedVoucherTotal = decimal.Round(totalAmount - settlementAmount, 2, MidpointRounding.AwayFromZero);
            expectedGatewayAmount = settlementAmount;
            if (expectedVoucherTotal <= 0)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Voucher redemption amount must be greater than zero in mixed voucher payments.",
                    Errors = { "Voucher redemption amount must be greater than zero in mixed voucher payments." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_MIXED_ZERO_REDEEM"
                });
            }
        }
        else if (request.Payment.Amount.HasValue)
        {
            expectedGatewayAmount = decimal.Round(
                request.Payment.Amount.Value,
                2,
                MidpointRounding.AwayFromZero);
        }

        CardPaymentTransaction? validatedCardTransaction = null;
        var gatewayIntentId = request.Payment.CardPaymentIntentId
            ?? request.Payment.OnlinePaymentId
            ?? request.Payment.GatewayIntentId;
        var requiresGatewayIntent = IsCardLegacyPayment(methodResolution.LegacyRaw)
            || OnlinePaymentMethods.Normalize(request.Payment.Method) is not null
            || gatewayIntentId.HasValue;
        if (requiresGatewayIntent && _cardPaymentService != null)
        {
            var intentToValidate = gatewayIntentId is Guid gid && gid != Guid.Empty ? gid : Guid.Empty;
            var (cardOk, cardTxn, cardErrCode, cardErrMsg) = await _cardPaymentService
                .ValidateForFiscalPaymentAsync(intentToValidate, expectedGatewayAmount, cashRegisterId)
                .ConfigureAwait(false);
            if (!cardOk)
            {
                return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = cardErrMsg ?? "Card payment intent validation failed.",
                    Errors = { cardErrMsg ?? "Card payment intent validation failed." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = cardErrCode ?? "CARD_INTENT_INVALID"
                });
            }

            validatedCardTransaction = cardTxn;
        }

        // Voucher redemption when method is voucher, or when voucher payload is explicitly combined with a non-voucher settlement.
        List<VoucherRedeemLine>? voucherRedeemLinesForCommit = null;
        if (isVoucherMethodResolved || sale.HasVoucherPayload)
        {
            effectiveTseRequired = _tseOptions.RequiresFiscalSignature;
            var (voucherPlanError, voucherLines) = await _voucherLedger.BuildVoucherRedemptionPlanAsync(
                effectiveTenantId,
                cashRegisterId,
                expectedVoucherTotal,
                request.Payment,
                CancellationToken.None);
            if (voucherPlanError != null)
                return SettlementPhase<SaleSettlement>.Fail(voucherPlanError);

            voucherRedeemLinesForCommit = voucherLines;
        }

        var timeSyncWarningForPayment = false;
        var ntpEff = await _ntpEffectiveSettings.GetEffectiveAsync(CancellationToken.None)
            .ConfigureAwait(false);
        if (effectiveTseRequired && ntpEff.Enabled)
        {
            if (!_ntpTimeSyncStatus.ShouldAllowOnlineFiscalPayment(
                    ntpEff,
                    out var clockMsg))
            {
                if (offlineTransactionId.HasValue)
                {
                    timeSyncWarningForPayment = true;
                    _logger.LogWarning(
                        "Fiscal payment from offline replay accepted with NTP drift flag (TimeSyncWarning). OfflineTransactionId={OfflineId}",
                        offlineTransactionId);
                }
                else
                {
                    _logger.LogWarning("Payment rejected: NTP/system time outside RKSV tolerance.");
                    return SettlementPhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = clockMsg ?? "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren",
                        Errors = { clockMsg ?? "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren" },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "NTP_TIME_SYNC"
                    });
                }
            }
        }

        return SettlementPhase<SaleSettlement>.Ok(new SaleSettlement
        {
            MethodResolution = methodResolution,
            ValidatedCardTransaction = validatedCardTransaction,
            VoucherRedeemLines = voucherRedeemLinesForCommit,
            TimeSyncWarning = timeSyncWarningForPayment,
            EffectiveTseRequired = effectiveTseRequired,
        });
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

    private static bool IsCardLegacyPayment(string legacyRaw) =>
        string.Equals(legacyRaw, ((int)PaymentMethod.Card).ToString(), StringComparison.Ordinal);
}

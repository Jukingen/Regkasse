using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface ICardPaymentService
{
    Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> CreateIntentAsync(
        CreateCardPaymentIntentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> CreateIntentFromPosRequestAsync(
        CardPaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> ConfirmIntentAsync(
        Guid intentId,
        ConfirmCardPaymentIntentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(CardPaymentConfirmResponse? Response, string? ErrorCode, string? ErrorMessage)> ConfirmByPaymentIntentIdAsync(
        ConfirmCardPaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> CancelIntentAsync(
        Guid intentId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, CardPaymentTransaction? Transaction, string? ErrorCode, string? ErrorMessage)> ValidateForFiscalPaymentAsync(
        Guid intentId,
        decimal expectedAmount,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);

    Task LinkToPaymentAsync(Guid intentId, Guid paymentDetailsId, CancellationToken cancellationToken = default);
}

public sealed class CardPaymentService : ICardPaymentService
{
    private readonly AppDbContext _context;
    private readonly IPaymentGateway _gateway;
    private readonly ICashRegisterResolutionService _cashRegisterResolution;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly PaymentGatewayOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CardPaymentService> _logger;

    public CardPaymentService(
        AppDbContext context,
        IPaymentGateway gateway,
        ICashRegisterResolutionService cashRegisterResolution,
        ISettingsTenantResolver settingsTenantResolver,
        IOptions<PaymentGatewayOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CardPaymentService> logger)
    {
        _context = context;
        _gateway = gateway;
        _cashRegisterResolution = cashRegisterResolution;
        _settingsTenantResolver = settingsTenantResolver;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> CreateIntentAsync(
        CreateCardPaymentIntentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount < 0.01m)
            return (null, "CARD_INTENT_INVALID_AMOUNT", "Amount must be greater than zero.");

        var principal = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var registerValidation = await _cashRegisterResolution.ValidatePaymentRegisterForCommitAsync(
            userId,
            request.CashRegisterId,
            principal,
            cancellationToken).ConfigureAwait(false);
        if (!registerValidation.Ok)
            return (null, registerValidation.Code ?? "CARD_INTENT_INVALID_REGISTER", registerValidation.Message);

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var intentId = Guid.NewGuid();

        var gatewayRequest = new CreatePaymentIntentRequest
        {
            InternalIntentId = intentId,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description,
            Metadata = request.Metadata ?? new Dictionary<string, string>()
        };

        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.CreatePaymentIntentAsync(gatewayRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Card gateway CreatePaymentIntent failed");
            return (null, "CARD_GATEWAY_ERROR", "Card payment gateway is unavailable.");
        }

        if (!gatewayResult.Success)
            return (null, "CARD_INTENT_CREATE_FAILED", gatewayResult.ErrorMessage ?? "Failed to create payment intent.");

        var row = new CardPaymentTransaction
        {
            Id = intentId,
            TenantId = tenantId,
            CashRegisterId = registerValidation.ResolvedRegisterId!.Value,
            Amount = request.Amount,
            Currency = gatewayRequest.Currency,
            Gateway = _gateway.ProviderName,
            GatewayPaymentIntentId = gatewayResult.PaymentIntentId,
            GatewayTransactionId = gatewayResult.TransactionId ?? gatewayResult.PaymentIntentId,
            ClientSecret = gatewayResult.ClientSecret,
            Status = CardPaymentTransactionStatuses.FromPaymentIntentStatus(gatewayResult.Status),
            CreatedByUserId = userId,
            Description = request.Description,
            MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>())
        };

        _context.CardPaymentTransactions.Add(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (MapResponse(row), null, null);
    }

    public async Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> CreateIntentFromPosRequestAsync(
        CardPaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString(),
            ["cashRegisterId"] = request.CashRegisterId.ToString()
        };
        if (!string.IsNullOrWhiteSpace(request.ReceiptNumber))
            metadata["receiptNumber"] = request.ReceiptNumber.Trim();

        var description = string.IsNullOrWhiteSpace(request.ReceiptNumber)
            ? null
            : $"Payment for receipt {request.ReceiptNumber.Trim()}";

        return await CreateIntentAsync(
            new CreateCardPaymentIntentRequest
            {
                Amount = request.Amount,
                Currency = "EUR",
                CashRegisterId = request.CashRegisterId,
                Description = description,
                Metadata = metadata
            },
            userId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> ConfirmIntentAsync(
        Guid intentId,
        ConfirmCardPaymentIntentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadIntentForMutationAsync(intentId, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return (null, "CARD_INTENT_NOT_FOUND", "Card payment intent not found.");

        if (row.Status is CardPaymentTransactionStatuses.Succeeded or CardPaymentTransactionStatuses.Cancelled or CardPaymentTransactionStatuses.Refunded)
            return (MapResponse(row), null, null);

        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.ConfirmPaymentAsync(row.GatewayPaymentIntentId!, request.PaymentMethodId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Card gateway ConfirmPayment failed for intent {IntentId}", intentId);
            return (null, "CARD_GATEWAY_ERROR", "Card payment gateway is unavailable.");
        }

        row.Status = CardPaymentTransactionStatuses.FromPaymentIntentStatus(gatewayResult.Status);
        row.GatewayTransactionId = gatewayResult.TransactionId;
        row.CardBrand = gatewayResult.CardBrand;
        row.CardLast4 = gatewayResult.LastFourDigits;
        row.ErrorMessage = gatewayResult.ErrorMessage;
        row.UpdatedAt = DateTime.UtcNow;
        if (gatewayResult.Status == PaymentIntentStatus.Succeeded)
            row.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!gatewayResult.Success)
            return (MapResponse(row), "CARD_CONFIRM_DECLINED", gatewayResult.ErrorMessage ?? "Card payment was declined.");

        return (MapResponse(row), null, null);
    }

    public async Task<(CardPaymentConfirmResponse? Response, string? ErrorCode, string? ErrorMessage)> ConfirmByPaymentIntentIdAsync(
        ConfirmCardPaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentIntentId))
            return (null, "CARD_INTENT_ID_REQUIRED", "Payment intent id is required.");

        var intentId = await ResolveIntentIdAsync(request.PaymentIntentId.Trim(), cancellationToken).ConfigureAwait(false);
        if (intentId == null)
            return (null, "CARD_INTENT_NOT_FOUND", "Card payment intent not found.");

        var (response, errorCode, errorMessage) = await ConfirmIntentAsync(
            intentId.Value,
            new ConfirmCardPaymentIntentRequest { PaymentMethodId = request.PaymentMethodId ?? string.Empty },
            userId,
            cancellationToken).ConfigureAwait(false);

        if (response == null)
            return (null, errorCode, errorMessage);

        var confirmResponse = new CardPaymentConfirmResponse
        {
            Success = response.Status == CardPaymentTransactionStatuses.Succeeded,
            TransactionId = response.Id,
            ErrorMessage = response.ErrorMessage ?? errorMessage
        };

        return (confirmResponse, errorCode, errorMessage);
    }

    public async Task<(CardPaymentIntentResponse? Response, string? ErrorCode, string? ErrorMessage)> CancelIntentAsync(
        Guid intentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadIntentForMutationAsync(intentId, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return (null, "CARD_INTENT_NOT_FOUND", "Card payment intent not found.");

        if (row.Status == CardPaymentTransactionStatuses.Succeeded)
            return (null, "CARD_INTENT_ALREADY_SUCCEEDED", "Cannot cancel a succeeded card payment intent.");

        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.CancelPaymentAsync(row.GatewayPaymentIntentId!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Card gateway CancelPayment failed for intent {IntentId}", intentId);
            return (null, "CARD_GATEWAY_ERROR", "Card payment gateway is unavailable.");
        }

        row.Status = CardPaymentTransactionStatuses.FromPaymentIntentStatus(gatewayResult.Status);
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (MapResponse(row), null, null);
    }

    public async Task<(bool Ok, CardPaymentTransaction? Transaction, string? ErrorCode, string? ErrorMessage)> ValidateForFiscalPaymentAsync(
        Guid intentId,
        decimal expectedAmount,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.RequireCardIntentForPosPayments)
            return (true, null, null, null);

        if (intentId == Guid.Empty)
            return (false, null, "CARD_INTENT_REQUIRED", "Card payment requires a confirmed card payment intent.");

        var row = await _context.CardPaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == intentId, cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
            return (false, null, "CARD_INTENT_NOT_FOUND", "Card payment intent not found.");

        if (row.Status != CardPaymentTransactionStatuses.Succeeded)
            return (false, row, "CARD_INTENT_NOT_CONFIRMED", "Card payment intent is not confirmed.");

        if (row.PaymentId.HasValue)
            return (false, row, "CARD_INTENT_ALREADY_USED", "Card payment intent was already linked to a fiscal payment.");

        if (row.CashRegisterId != cashRegisterId)
            return (false, row, "CARD_INTENT_REGISTER_MISMATCH", "Card payment intent cash register mismatch.");

        if (Math.Abs(row.Amount - expectedAmount) > 0.01m)
            return (false, row, "CARD_INTENT_AMOUNT_MISMATCH", "Card payment intent amount does not match sale total.");

        return (true, row, null, null);
    }

    public async Task LinkToPaymentAsync(Guid intentId, Guid paymentDetailsId, CancellationToken cancellationToken = default)
    {
        var row = await _context.CardPaymentTransactions
            .FirstOrDefaultAsync(c => c.Id == intentId, cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return;

        row.PaymentId = paymentDetailsId;
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid?> ResolveIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(paymentIntentId, out var internalId))
        {
            var exists = await _context.CardPaymentTransactions.AsNoTracking()
                .AnyAsync(t => t.Id == internalId, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                return internalId;
        }

        var row = await _context.CardPaymentTransactions.AsNoTracking()
            .Where(t => t.GatewayPaymentIntentId == paymentIntentId)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row?.Id;
    }

    private async Task<CardPaymentTransaction?> LoadIntentForMutationAsync(Guid intentId, CancellationToken cancellationToken) =>
        await _context.CardPaymentTransactions
            .FirstOrDefaultAsync(c => c.Id == intentId, cancellationToken)
            .ConfigureAwait(false);

    private static CardPaymentIntentResponse MapResponse(CardPaymentTransaction row) =>
        new()
        {
            Id = row.Id,
            Amount = row.Amount,
            Currency = row.Currency,
            Status = row.Status,
            GatewayProvider = row.Gateway,
            ClientSecret = row.ClientSecret,
            TransactionId = row.GatewayTransactionId ?? row.Id.ToString(),
            CardBrand = row.CardBrand,
            LastFourDigits = row.CardLast4,
            ErrorMessage = row.ErrorMessage,
            CashRegisterId = row.CashRegisterId,
            PaymentDetailsId = row.PaymentId,
            CreatedAtUtc = row.CreatedAt,
            ConfirmedAtUtc = row.CompletedAt
        };
}

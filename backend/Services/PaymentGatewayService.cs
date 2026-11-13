using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Stripe;

namespace KasseAPI_Final.Services;

/// <summary>
/// Online-payment orchestration against <c>gateway_payment_intents</c>
/// (<see cref="CardPaymentTransaction"/>). Fiscal TSE/RKSV commit stays on
/// <see cref="IPaymentService"/>. Webhooks only mutate intent status;
/// they never create fiscal rows.
/// </summary>
public sealed class PaymentGatewayService : IPaymentGatewayService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _context;
    private readonly IPaymentGateway _gateway;
    private readonly ICashRegisterResolutionService _cashRegisterResolution;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly PaymentGatewayOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(
        AppDbContext context,
        IPaymentGateway gateway,
        ICashRegisterResolutionService cashRegisterResolution,
        ISettingsTenantResolver settingsTenantResolver,
        IOptions<PaymentGatewayOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PaymentGatewayService> logger,
        IHostEnvironment? environment = null)
    {
        _context = context;
        _gateway = gateway;
        _cashRegisterResolution = cashRegisterResolution;
        _settingsTenantResolver = settingsTenantResolver;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _environment = environment;
    }

    public async Task<PaymentGatewayOperationResult> ProcessPaymentAsync(
        InitiateOnlinePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount < 0.01m)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.InvalidAmount,
                "Amount must be greater than zero.");

        var method = OnlinePaymentMethods.Normalize(request.Method);
        if (method is null
            || string.Equals(method, "voucher", StringComparison.OrdinalIgnoreCase))
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.InvalidMethod,
                "Method must be card or paypal.");

        if (!TryNormalizeClientUrl(request.ReturnUrl, out var returnUrl, out var returnUrlError))
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.InvalidReturnUrl,
                returnUrlError ?? "Return URL is invalid.");

        if (!TryNormalizeClientUrl(request.CancelUrl, out var cancelUrl, out var cancelUrlError))
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.InvalidReturnUrl,
                cancelUrlError ?? "Cancel URL is invalid.");

        returnUrl = PaymentReturnUrls.ToAbsoluteIfRelative(returnUrl, _httpContextAccessor.HttpContext);
        if (string.IsNullOrWhiteSpace(cancelUrl))
            cancelUrl = returnUrl;

        var principal = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var registerValidation = await _cashRegisterResolution.ValidatePaymentRegisterForCommitAsync(
            userId,
            request.CashRegisterId,
            principal,
            cancellationToken).ConfigureAwait(false);
        if (!registerValidation.Ok)
        {
            return PaymentGatewayOperationResult.Fail(
                registerValidation.Code ?? OnlinePaymentErrorCodes.InvalidRegister,
                registerValidation.Message);
        }

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.TenantContextRequired,
                "The requested resource could not be found");

        var idempotencyKey = ResolveIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await _context.CardPaymentTransactions
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing != null)
                return PaymentGatewayOperationResult.Success(Map(existing));
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "EUR"
            : request.Currency.Trim().ToUpperInvariant();
        var intentId = Guid.NewGuid();
        var metadata = request.Metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(request.Metadata);
        metadata["tenant_id"] = tenantId.ToString("D");
        metadata["cash_register_id"] = request.CashRegisterId.ToString("D");
        metadata["method"] = method;
        metadata["purpose"] = "pos_online_payment";
        metadata["online_payment_id"] = intentId.ToString("D");
        if (!string.IsNullOrWhiteSpace(returnUrl))
            metadata["return_url"] = returnUrl;
        if (!string.IsNullOrWhiteSpace(cancelUrl))
            metadata["cancel_url"] = cancelUrl;

        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.CreatePaymentIntentAsync(
                new CreatePaymentIntentRequest
                {
                    InternalIntentId = intentId,
                    Amount = request.Amount,
                    Currency = currency,
                    Description = request.Description,
                    ReturnUrl = returnUrl,
                    Metadata = metadata
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Online payment gateway CreatePaymentIntent failed");
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.GatewayError,
                "Payment gateway is unavailable.");
        }

        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.PaymentIntentId))
        {
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.CreateFailed,
                gatewayResult.ErrorMessage ?? "Failed to create payment intent.");
        }

        var status = CardPaymentTransactionStatuses.FromPaymentIntentStatus(gatewayResult.Status);
        var now = DateTime.UtcNow;
        var row = new CardPaymentTransaction
        {
            Id = intentId,
            TenantId = tenantId,
            CashRegisterId = registerValidation.ResolvedRegisterId!.Value,
            Gateway = _gateway.ProviderName,
            MethodCode = method,
            GatewayPaymentIntentId = gatewayResult.PaymentIntentId,
            GatewayTransactionId = gatewayResult.TransactionId ?? gatewayResult.PaymentIntentId,
            Status = status,
            Amount = request.Amount,
            Currency = currency,
            ClientSecret = gatewayResult.ClientSecret,
            RedirectUrl = gatewayResult.RedirectUrl,
            ReturnUrl = returnUrl,
            IdempotencyKey = idempotencyKey,
            CreatedByUserId = userId,
            Description = request.Description,
            MetadataJson = JsonSerializer.Serialize(metadata, JsonOptions),
            CompletedAt = status is CardPaymentTransactionStatuses.Succeeded ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.CardPaymentTransactions.Add(row);
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex) && idempotencyKey is not null)
        {
            var raced = await _context.CardPaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (raced != null)
                return PaymentGatewayOperationResult.Success(Map(raced));
            throw;
        }

        _logger.LogInformation(
            "Online payment initiated {OnlinePaymentId} provider={Provider} method={Method} status={Status} tenant={TenantId}",
            row.Id,
            row.Gateway,
            row.MethodCode,
            row.Status,
            tenantId);

        return PaymentGatewayOperationResult.Success(Map(row));
    }

    public async Task<PaymentGatewayWebhookResult> VerifyWebhookAsync(
        string provider,
        string payload,
        IHeaderDictionary headers,
        CancellationToken cancellationToken = default)
    {
        var normalized = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            return PaymentGatewayWebhookResult.Invalid(
                OnlinePaymentErrorCodes.UnknownProvider,
                "Payment provider is required.");

        WebhookEvent parsed;
        try
        {
            parsed = normalized switch
            {
                "stripe" => ParseStripeEvent(payload, headers),
                "mock" or "paypal" => ParseSignedGenericEvent(normalized, payload, headers),
                _ => throw new InvalidOperationException("unknown-provider")
            };
        }
        catch (InvalidOperationException ex) when (ex.Message == "unknown-provider")
        {
            return PaymentGatewayWebhookResult.Invalid(
                OnlinePaymentErrorCodes.UnknownProvider,
                "Unknown payment provider.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Online payment webhook signature verification failed for {Provider}", normalized);
            return PaymentGatewayWebhookResult.Invalid(
                OnlinePaymentErrorCodes.InvalidWebhook,
                "Invalid webhook signature.");
        }

        if (string.IsNullOrWhiteSpace(parsed.GatewayPaymentIntentId)
            || string.IsNullOrWhiteSpace(parsed.TargetStatus))
            return PaymentGatewayWebhookResult.Ignored();

        var row = await _context.CardPaymentTransactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.GatewayPaymentIntentId == parsed.GatewayPaymentIntentId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            _logger.LogInformation(
                "Online payment webhook for unknown intent {IntentId} provider={Provider}",
                parsed.GatewayPaymentIntentId,
                normalized);
            return PaymentGatewayWebhookResult.Ignored();
        }

        if (!string.IsNullOrWhiteSpace(parsed.EventId))
        {
            var duplicateEvent = await _context.GatewayWebhookEvents
                .AsNoTracking()
                .AnyAsync(
                    e => e.Provider == normalized && e.EventId == parsed.EventId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (duplicateEvent
                || string.Equals(row.LastWebhookEventId, parsed.EventId, StringComparison.Ordinal))
            {
                return PaymentGatewayWebhookResult.AppliedOk(Map(row));
            }
        }

        var targetStatus = parsed.TargetStatus;
        if (!CardPaymentTransactionStatuses.CanTransition(row.Status, targetStatus))
        {
            _logger.LogInformation(
                "Online payment webhook ignored illegal transition {From}->{To} for {OnlinePaymentId}",
                row.Status,
                targetStatus,
                row.Id);
            return PaymentGatewayWebhookResult.AppliedOk(Map(row));
        }

        ApplyStatus(row, targetStatus, parsed.ErrorMessage);
        row.LastWebhookEventId = parsed.EventId;
        if (!string.IsNullOrWhiteSpace(parsed.TransactionId))
            row.GatewayTransactionId = parsed.TransactionId;
        row.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(parsed.EventId))
        {
            _context.GatewayWebhookEvents.Add(new GatewayWebhookEvent
            {
                Id = Guid.NewGuid(),
                TenantId = row.TenantId,
                IntentId = row.Id,
                Provider = normalized,
                EventId = parsed.EventId,
                EventType = targetStatus,
                ReceivedAtUtc = DateTime.UtcNow,
                Applied = true
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Online payment {OnlinePaymentId} updated from webhook to {Status} tenant={TenantId}",
            row.Id,
            row.Status,
            row.TenantId);

        return PaymentGatewayWebhookResult.AppliedOk(Map(row));
    }

    public async Task<PaymentGatewayOperationResult> SimulateTestOutcomeAsync(
        SimulateOnlinePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = (request.Outcome ?? string.Empty).Trim().ToLowerInvariant();
        var target = outcome switch
        {
            "success" or "succeeded" or "completed" or "gateway_succeeded" =>
                CardPaymentTransactionStatuses.Succeeded,
            "failed" or "fail" or "failure" => CardPaymentTransactionStatuses.Failed,
            _ => null
        };
        if (target is null)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.InvalidOutcome,
                "Outcome must be success or failed.");

        var row = await _context.CardPaymentTransactions
            .FirstOrDefaultAsync(p => p.Id == request.OnlinePaymentId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.NotFound,
                "Online payment not found.");

        if (!CardPaymentTransactionStatuses.CanTransition(row.Status, target))
        {
            if (string.Equals(row.Status, target, StringComparison.Ordinal))
                return PaymentGatewayOperationResult.Success(Map(row));

            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.IllegalTransition,
                $"Cannot transition from {row.Status} to {target}.");
        }

        ApplyStatus(row, target, target == CardPaymentTransactionStatuses.Failed ? "Simulated failure" : null);
        row.LastWebhookEventId = target == CardPaymentTransactionStatuses.Failed
            ? "payment_intent.payment_failed"
            : "payment_intent.succeeded";
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Online payment {OnlinePaymentId} simulated to {Status}",
            row.Id,
            row.Status);

        return PaymentGatewayOperationResult.Success(Map(row));
    }

    public async Task<PaymentGatewayOperationResult> GetByIdAsync(
        Guid onlinePaymentId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.CardPaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == onlinePaymentId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.NotFound,
                "Online payment not found.");

        return PaymentGatewayOperationResult.Success(Map(row));
    }

    public Task<PaymentGatewayOperationResult> CreateIntentAsync(
        InitiateOnlinePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default) =>
        ProcessPaymentAsync(request, userId, cancellationToken);

    public Task<PaymentGatewayOperationResult> ConfirmAsync(
        Guid onlinePaymentId,
        CancellationToken cancellationToken = default) =>
        GetByIdAsync(onlinePaymentId, cancellationToken);

    public Task<PaymentGatewayWebhookResult> HandleWebhookAsync(
        string provider,
        string payload,
        IHeaderDictionary headers,
        CancellationToken cancellationToken = default) =>
        VerifyWebhookAsync(provider, payload, headers, cancellationToken);

    public async Task<PaymentGatewayOperationResult> RefundAsync(
        Guid onlinePaymentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount < 0.01m)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.InvalidAmount,
                "Amount must be greater than zero.");

        var row = await _context.CardPaymentTransactions
            .FirstOrDefaultAsync(p => p.Id == onlinePaymentId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.NotFound,
                "Online payment not found.");

        if (string.Equals(row.Status, CardPaymentTransactionStatuses.Refunded, StringComparison.Ordinal))
            return PaymentGatewayOperationResult.Success(Map(row));

        if (!CardPaymentTransactionStatuses.CanTransition(row.Status, CardPaymentTransactionStatuses.Refunded))
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.IllegalTransition,
                $"Cannot refund from {row.Status}.");

        if (string.IsNullOrWhiteSpace(row.GatewayPaymentIntentId))
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.GatewayRefundFailed,
                "Gateway payment intent is missing.");

        RefundResult refund;
        try
        {
            refund = await _gateway.RefundPaymentAsync(row.GatewayPaymentIntentId, amount, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Online payment refund failed for {OnlinePaymentId}", row.Id);
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.GatewayRefundFailed,
                "Payment gateway refund failed.");
        }

        if (!refund.Success)
            return PaymentGatewayOperationResult.Fail(
                OnlinePaymentErrorCodes.GatewayRefundFailed,
                refund.ErrorMessage ?? "Payment gateway refund failed.");

        ApplyStatus(row, CardPaymentTransactionStatuses.Refunded, null);
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PaymentGatewayOperationResult.Success(Map(row));
    }

    internal bool TryNormalizeClientUrl(string? raw, out string? normalized, out string? error) =>
        TryNormalizeClientUrl(raw, _environment is null || _environment.IsDevelopment(), out normalized, out error);

    internal static bool TryNormalizeClientUrl(
        string? raw,
        bool allowLoopback,
        out string? normalized,
        out string? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            normalized = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "Return URL is invalid.";
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("https" or "http" or "regkasse" or "cashregister" or "exp" or "exps"))
        {
            error = "Return URL scheme is not allowed.";
            return false;
        }

        if (!allowLoopback && IsLoopbackHost(uri))
        {
            error = "Return URL must not use a loopback host outside Development.";
            return false;
        }

        normalized = uri.ToString();
        return true;
    }

    private static bool IsLoopbackHost(Uri uri)
    {
        if (uri.IsLoopback)
            return true;
        var host = uri.IdnHost;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }

    private WebhookEvent ParseStripeEvent(string payload, IHeaderDictionary headers)
    {
        var webhookSecret = _options.ResolveStripeWebhookSecret();
        if (string.IsNullOrWhiteSpace(webhookSecret))
            throw new InvalidOperationException("Stripe webhook secret is not configured.");

        var signature = headers["Stripe-Signature"].ToString();
        var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
        var intentId = stripeEvent.Data.Object is PaymentIntent intent ? intent.Id : null;
        var transactionId = stripeEvent.Data.Object is PaymentIntent pi ? pi.Id : null;

        var target = stripeEvent.Type switch
        {
            "payment_intent.succeeded" => CardPaymentTransactionStatuses.Succeeded,
            "payment_intent.payment_failed" or "payment_intent.canceled" => CardPaymentTransactionStatuses.Failed,
            "charge.refunded" => CardPaymentTransactionStatuses.Refunded,
            _ => null
        };

        return new WebhookEvent(intentId, stripeEvent.Id, transactionId, target, null);
    }

    private WebhookEvent ParseSignedGenericEvent(string provider, string payload, IHeaderDictionary headers)
    {
        var secret = ResolveGenericWebhookSecret();
        var signature = headers["X-Payment-Webhook-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning(
                "Online payment webhook for {Provider} accepted without shared secret (Development/Mock only).",
                provider);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(signature) || !IsValidHmac(payload, secret, signature))
                throw new InvalidOperationException("hmac-mismatch");
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        var root = doc.RootElement;
        var intentId = ReadString(root, "paymentIntentId")
            ?? ReadString(root, "gatewayPaymentIntentId")
            ?? ReadString(root, "id");
        var eventId = ReadString(root, "eventId") ?? ReadString(root, "id");
        var transactionId = ReadString(root, "transactionId");
        var statusRaw = ReadString(root, "status") ?? ReadString(root, "eventType") ?? string.Empty;
        var target = MapWebhookStatus(statusRaw);
        var error = ReadString(root, "errorMessage") ?? ReadString(root, "error");
        return new WebhookEvent(intentId, eventId, transactionId, target, error);
    }

    private string? ResolveGenericWebhookSecret()
    {
        var stripe = _options.ResolveStripeWebhookSecret();
        return string.IsNullOrWhiteSpace(stripe) ? null : stripe;
    }

    private string? ResolveIdempotencyKey(string? requestKey)
    {
        var fromRequest = string.IsNullOrWhiteSpace(requestKey) ? null : requestKey.Trim();
        if (fromRequest != null)
            return fromRequest.Length > 64 ? fromRequest[..64] : fromRequest;

        var header = _httpContextAccessor.HttpContext?.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(header))
            header = _httpContextAccessor.HttpContext?.Request.Headers["Request-Id"].ToString();
        if (string.IsNullOrWhiteSpace(header))
            return null;
        var trimmed = header.Trim();
        return trimmed.Length > 64 ? trimmed[..64] : trimmed;
    }

    private static bool IsValidHmac(string payload, string secret, string providedHex)
    {
        try
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
            var provided = providedHex.Trim().Replace("sha256=", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (provided.Length % 2 != 0)
                return false;
            var providedBytes = Convert.FromHexString(provided);
            return CryptographicOperations.FixedTimeEquals(hash, providedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsIdempotencyKeyViolation(DbUpdateException ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg && pg.SqlState == "23505" &&
                (pg.ConstraintName?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) ?? false))
                return true;
        }

        return false;
    }

    private static string? MapWebhookStatus(string raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "succeeded" or "success" or "completed" or "paid" or "payment_intent.succeeded"
                or "gateway_succeeded" =>
                CardPaymentTransactionStatuses.Succeeded,
            "failed" or "fail" or "failure" or "cancelled" or "canceled" or "payment_intent.payment_failed" =>
                CardPaymentTransactionStatuses.Failed,
            "refunded" or "charge.refunded" => CardPaymentTransactionStatuses.Refunded,
            _ => null
        };
    }

    private static void ApplyStatus(CardPaymentTransaction row, string target, string? errorMessage)
    {
        var now = DateTime.UtcNow;
        row.Status = target;
        switch (target)
        {
            case CardPaymentTransactionStatuses.Succeeded:
                row.CompletedAt = now;
                row.ErrorMessage = null;
                break;
            case CardPaymentTransactionStatuses.Failed:
            case CardPaymentTransactionStatuses.Cancelled:
            case CardPaymentTransactionStatuses.Expired:
                row.ErrorMessage = errorMessage ?? row.ErrorMessage;
                break;
            case CardPaymentTransactionStatuses.Refunded:
                row.CompletedAt ??= now;
                row.RefundedAtUtc ??= now;
                break;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;
        if (!root.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static OnlinePaymentDto Map(CardPaymentTransaction row) =>
        new()
        {
            Id = row.Id,
            Status = OnlinePaymentStatuses.FromCardStatus(row.Status, row.PaymentId),
            Provider = row.Gateway,
            Method = string.IsNullOrWhiteSpace(row.MethodCode)
                ? OnlinePaymentMethods.Card
                : row.MethodCode,
            Amount = row.Amount,
            Currency = row.Currency,
            CashRegisterId = row.CashRegisterId,
            PaymentIntentId = row.GatewayPaymentIntentId,
            ClientSecret = row.ClientSecret,
            RedirectUrl = row.RedirectUrl,
            TransactionId = row.GatewayTransactionId,
            ErrorMessage = row.ErrorMessage,
            PaymentDetailsId = row.PaymentId,
            CreatedAtUtc = row.CreatedAt,
            CompletedAtUtc = row.CompletedAt
        };

    private sealed record WebhookEvent(
        string? GatewayPaymentIntentId,
        string? EventId,
        string? TransactionId,
        string? TargetStatus,
        string? ErrorMessage);
}

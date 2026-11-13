using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.OnlinePayments;

/// <summary>
/// Super Admin list + test console for POS gateway intents
/// (<c>gateway_payment_intents</c> / card + PayPal). Does not create fiscal POS receipts or TSE signatures.
/// </summary>
public sealed class OnlinePaymentAdminService : IOnlinePaymentAdminService
{
    public const string TenantRequiredCode = "TENANT_CONTEXT_REQUIRED";
    public const string ValidationCode = "VALIDATION_ERROR";
    public const string NotFoundCode = OnlinePaymentErrorCodes.NotFound;
    public const string GatewayErrorCode = OnlinePaymentErrorCodes.GatewayError;
    public const string InvalidRegisterCode = OnlinePaymentErrorCodes.InvalidRegister;

    internal const string SyntheticDescription = "Admin online-payment test";
    internal const string SyntheticPurpose = "admin_online_payment_test";
    internal const string WebhookSucceededEvent = "payment_intent.succeeded";
    internal const string WebhookFailedEvent = "payment_intent.payment_failed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IPaymentGateway _gateway;
    private readonly TimeProvider _time;
    private readonly ILogger<OnlinePaymentAdminService> _logger;

    public OnlinePaymentAdminService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenantAccessor,
        IPaymentGateway gateway,
        TimeProvider time,
        ILogger<OnlinePaymentAdminService> logger)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
        _gateway = gateway;
        _time = time;
        _logger = logger;
    }

    public async Task<AdminOnlinePaymentListResponse> ListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return new AdminOnlinePaymentListResponse
            {
                Items = Array.Empty<AdminOnlinePaymentDto>(),
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await LoadTenantLabelAsync(db, tenantId, ct);

        var query = db.CardPaymentTransactions.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AdminOnlinePaymentListResponse
        {
            Items = rows.Select(p => MapRow(p, tenant.Name, tenant.Slug)).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AdminOnlinePaymentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.CardPaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);
        if (row is null)
            return null;

        var tenant = await LoadTenantLabelAsync(db, tenantId, ct);
        return MapRow(row, tenant.Name, tenant.Slug);
    }

    public async Task<AdminOnlinePaymentTestResponse> RunTestAsync(
        AdminOnlinePaymentTestRequest request,
        CancellationToken ct = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Fail(TenantRequiredCode, "Tenant context is required.");

        var action = (request.Action ?? string.Empty).Trim();
        if (string.Equals(action, OnlinePaymentTestActions.Create, StringComparison.OrdinalIgnoreCase))
            return await CreateTestPaymentAsync(tenantId, request, ct);

        if (IsSuccessWebhookAction(action))
            return await SimulateWebhookAsync(tenantId, request, succeeded: true, ct);

        if (IsExpireWebhookAction(action))
            return await SimulateWebhookAsync(tenantId, request, succeeded: false, ct, expire: true);

        if (IsFailureWebhookAction(action))
            return await SimulateWebhookAsync(tenantId, request, succeeded: false, ct);

        return Fail(
            ValidationCode,
            "Action must be create, webhookSucceeded (success), webhookFailed (failed), or expire.");
    }

    private async Task<AdminOnlinePaymentTestResponse> CreateTestPaymentAsync(
        Guid tenantId,
        AdminOnlinePaymentTestRequest request,
        CancellationToken ct)
    {
        var amount = request.Amount ?? 0m;
        if (amount < 0.01m || amount > 10_000m)
            return Fail(ValidationCode, "Amount must be between 0.01 and 10000 EUR.");

        var method = OnlinePaymentMethods.Normalize(request.PaymentMethod);
        if (method is null)
            return Fail(ValidationCode, "Invalid payment method. Allowed: card, paypal.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var registerId = await db.CashRegisters
            .Where(r =>
                r.TenantId == tenantId
                && r.IsActive
                && r.DecommissionedAtUtc == null
                && r.Status != RegisterStatus.Decommissioned)
            .OrderByDescending(r => r.IsDefaultForTenant)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        if (registerId == Guid.Empty)
            return Fail(InvalidRegisterCode, "No active cash register was found for this tenant.");

        var intentId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["purpose"] = SyntheticPurpose,
            ["tenant_id"] = tenantId.ToString("D"),
            ["cash_register_id"] = registerId.ToString("D"),
            ["method"] = method,
            ["online_payment_id"] = intentId.ToString("D")
        };

        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.CreatePaymentIntentAsync(
                new CreatePaymentIntentRequest
                {
                    InternalIntentId = intentId,
                    Amount = amount,
                    Currency = "EUR",
                    Description = SyntheticDescription,
                    Metadata = metadata
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway CreatePaymentIntent failed for admin online-payment test");
            return Fail(GatewayErrorCode, "Payment gateway is unavailable.");
        }

        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.PaymentIntentId))
        {
            return Fail(
                GatewayErrorCode,
                gatewayResult.ErrorMessage ?? "Payment intent creation failed.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var status = CardPaymentTransactionStatuses.FromPaymentIntentStatus(gatewayResult.Status);
        var row = new CardPaymentTransaction
        {
            Id = intentId,
            TenantId = tenantId,
            CashRegisterId = registerId,
            Gateway = _gateway.ProviderName,
            MethodCode = method,
            GatewayPaymentIntentId = gatewayResult.PaymentIntentId,
            GatewayTransactionId = gatewayResult.TransactionId ?? gatewayResult.PaymentIntentId,
            Status = status,
            Amount = amount,
            Currency = "EUR",
            ClientSecret = gatewayResult.ClientSecret,
            RedirectUrl = gatewayResult.RedirectUrl,
            Description = SyntheticDescription,
            MetadataJson = JsonSerializer.Serialize(metadata, JsonOptions),
            CreatedByUserId = "admin-test",
            CompletedAt = status is CardPaymentTransactionStatuses.Succeeded ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.CardPaymentTransactions.Add(row);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Synthetic online payment created TenantId={TenantId} Id={Id} Amount={Amount} Method={Method}",
            tenantId,
            row.Id,
            amount,
            method);

        var tenant = await LoadTenantLabelAsync(db, tenantId, ct);
        return new AdminOnlinePaymentTestResponse
        {
            Succeeded = true,
            Transaction = MapRow(row, tenant.Name, tenant.Slug)
        };
    }

    private async Task<AdminOnlinePaymentTestResponse> SimulateWebhookAsync(
        Guid tenantId,
        AdminOnlinePaymentTestRequest request,
        bool succeeded,
        CancellationToken ct,
        bool expire = false)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var targetId = request.OnlinePaymentId ?? request.TransactionId;
        CardPaymentTransaction? row = null;
        if (targetId is Guid id && id != Guid.Empty)
        {
            row = await db.CardPaymentTransactions
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        }
        else
        {
            row = await db.CardPaymentTransactions
                .Where(x => x.TenantId == tenantId && x.Description == SyntheticDescription)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        if (row is null)
            return Fail(NotFoundCode, "No online payment was found to apply a webhook event.");

        var target = expire
            ? CardPaymentTransactionStatuses.Expired
            : succeeded
                ? CardPaymentTransactionStatuses.Succeeded
                : CardPaymentTransactionStatuses.Failed;
        if (!CardPaymentTransactionStatuses.CanTransition(row.Status, target))
        {
            if (string.Equals(row.Status, target, StringComparison.Ordinal))
            {
                var tenantSame = await LoadTenantLabelAsync(db, tenantId, ct);
                return new AdminOnlinePaymentTestResponse
                {
                    Succeeded = true,
                    Transaction = MapRow(row, tenantSame.Name, tenantSame.Slug)
                };
            }

            return Fail(
                OnlinePaymentErrorCodes.IllegalTransition,
                $"Cannot transition from {row.Status} to {target}.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = target;
        row.LastWebhookEventId = expire
            ? "payment_intent.canceled"
            : succeeded
                ? WebhookSucceededEvent
                : WebhookFailedEvent;
        row.UpdatedAt = now;
        if (succeeded)
        {
            row.CompletedAt = now;
            row.ErrorMessage = null;
        }
        else if (!expire)
        {
            row.ErrorMessage = "Simulated webhook failure.";
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Simulated online-payment webhook TenantId={TenantId} Id={Id} Succeeded={Succeeded}",
            tenantId,
            row.Id,
            succeeded);

        var tenant = await LoadTenantLabelAsync(db, tenantId, ct);
        return new AdminOnlinePaymentTestResponse
        {
            Succeeded = true,
            Transaction = MapRow(row, tenant.Name, tenant.Slug)
        };
    }

    private static bool IsSuccessWebhookAction(string action) =>
        string.Equals(action, OnlinePaymentTestActions.WebhookSucceeded, StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, OnlinePaymentTestActions.Success, StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, "succeeded", StringComparison.OrdinalIgnoreCase);

    private static bool IsExpireWebhookAction(string action) =>
        string.Equals(action, OnlinePaymentTestActions.Expire, StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, "expired", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailureWebhookAction(string action) =>
        string.Equals(action, OnlinePaymentTestActions.WebhookFailed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, OnlinePaymentTestActions.Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, "fail", StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, "failure", StringComparison.OrdinalIgnoreCase);

    private static async Task<(string Name, string Slug)> LoadTenantLabelAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Name, t.Slug })
            .FirstOrDefaultAsync(ct);
        return (tenant?.Name ?? string.Empty, tenant?.Slug ?? string.Empty);
    }

    private static AdminOnlinePaymentDto MapRow(
        CardPaymentTransaction row,
        string tenantName,
        string tenantSlug) =>
        new()
        {
            Id = row.Id,
            TenantId = row.TenantId,
            TenantName = tenantName,
            TenantSlug = tenantSlug,
            Amount = row.Amount,
            Currency = row.Currency,
            Status = row.Status,
            PaymentMethod = string.IsNullOrWhiteSpace(row.MethodCode)
                ? OnlinePaymentMethods.Card
                : row.MethodCode,
            Provider = row.Gateway,
            PaymentIntentId = row.GatewayPaymentIntentId,
            IsSynthetic = IsSynthetic(row),
            ErrorMessage = row.ErrorMessage,
            LastWebhookEvent = row.LastWebhookEventId,
            CreatedAtUtc = row.CreatedAt,
            CompletedAtUtc = row.CompletedAt
        };

    private static bool IsSynthetic(CardPaymentTransaction row) =>
        string.Equals(row.Description, SyntheticDescription, StringComparison.Ordinal)
        || (!string.IsNullOrEmpty(row.MetadataJson)
            && row.MetadataJson.Contains(SyntheticPurpose, StringComparison.Ordinal));

    private static AdminOnlinePaymentTestResponse Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}

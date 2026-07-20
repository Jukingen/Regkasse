using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Platform payment gateway status for FA (Stripe/Mock). Secrets are never returned;
/// API keys live in deployment config (<c>PaymentGateway:Stripe:*</c>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/settings/payment-gateway")]
[Produces("application/json")]
public sealed class AdminPaymentGatewaySettingsController : ControllerBase
{
    public const string WebhookPath = "/api/webhooks/stripe";

    private static readonly HashSet<string> AllowedOnlineMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "card", "paypal", "bank", "cash", "online"
    };

    private readonly IOptionsMonitor<PaymentGatewayOptions> _gateway;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<AdminPaymentGatewaySettingsController> _logger;

    public AdminPaymentGatewaySettingsController(
        IOptionsMonitor<PaymentGatewayOptions> gateway,
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AdminPaymentGatewaySettingsController> logger)
    {
        _gateway = gateway;
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(PaymentGatewaySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentGatewaySettingsDto>> Get(CancellationToken ct)
    {
        var o = _gateway.CurrentValue;
        var methods = await LoadOnlineMethodsAsync(ct);
        return Ok(Map(o, methods));
    }

    /// <summary>
    /// Updates tenant online-checkout payment methods only.
    /// Stripe secrets cannot be changed via API (deployment config).
    /// </summary>
    [HttpPut]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(PaymentGatewaySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentGatewaySettingsDto>> Put(
        [FromBody] UpdatePaymentGatewaySettingsRequestDto? body,
        CancellationToken ct)
    {
        if (body?.OnlinePaymentMethods is null)
        {
            return BadRequest(new { message = "OnlinePaymentMethods is required." });
        }

        var normalized = body.OnlinePaymentMethods
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Any(m => !AllowedOnlineMethods.Contains(m)))
        {
            return BadRequest(new
            {
                message = "Invalid payment method. Allowed: card, paypal, bank, cash, online."
            });
        }

        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return NotFound();
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var settings = await db.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (settings is null)
        {
            _logger.LogWarning(
                "Payment gateway settings PUT: SystemSettings missing for tenant {TenantId}",
                tenantId);
            return BadRequest(new { message = "System settings not found for tenant." });
        }

        settings.OnlineCheckoutPaymentMethods = string.Join(',', normalized);
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Online checkout payment methods updated by {Actor} for tenant {TenantId}: {Methods}",
            User.GetActorUserId() ?? "unknown",
            tenantId,
            settings.OnlineCheckoutPaymentMethods);

        return Ok(Map(_gateway.CurrentValue, normalized));
    }

    private async Task<IReadOnlyList<string>> LoadOnlineMethodsAsync(CancellationToken ct)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return DefaultMethods();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var raw = await db.SystemSettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.OnlineCheckoutPaymentMethods)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(raw))
            return DefaultMethods();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(m => AllowedOnlineMethods.Contains(m))
            .Select(m => m.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<string> DefaultMethods() =>
        new[] { "card", "cash", "online" };

    private static PaymentGatewaySettingsDto Map(
        PaymentGatewayOptions o,
        IReadOnlyList<string> methods) =>
        new()
        {
            Provider = string.IsNullOrWhiteSpace(o.Provider) ? "Mock" : o.Provider.Trim(),
            IsStripeProvider = o.IsStripeProvider,
            ApiKeyConfigured = !string.IsNullOrWhiteSpace(o.ResolveStripeApiKey()),
            WebhookSecretConfigured = !string.IsNullOrWhiteSpace(o.ResolveStripeWebhookSecret()),
            RequireCardIntentForPosPayments = o.RequireCardIntentForPosPayments,
            WebhookPath = WebhookPath,
            OnlinePaymentMethods = methods
        };
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin: product price / tax change journal and versions (RKSV trail).</summary>
[ApiController]
[Route("api/admin/price-history")]
public class AdminProductPriceHistoryController : ControllerBase
{
    private readonly IProductPriceHistoryService _priceHistoryService;
    private readonly IPriceChangeService _priceChangeService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<AdminProductPriceHistoryController> _logger;

    public AdminProductPriceHistoryController(
        IProductPriceHistoryService priceHistoryService,
        IPriceChangeService priceChangeService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminProductPriceHistoryController> logger)
    {
        _priceHistoryService = priceHistoryService;
        _priceChangeService = priceChangeService;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<IEnumerable<ProductPriceHistoryItemDto>>> GetHistory(
        [FromQuery] Guid? productId = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var items = await _priceHistoryService
                .GetHistoryAsync(tenantId, productId, take, cancellationToken)
                .ConfigureAwait(false);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing product price history");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("change")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<PriceChangeResult>> ChangePrice(
        [FromBody] PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null)
                return BadRequest(new { message = "Request body is required." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            request.TenantId = tenantId;

            if (request.ChangedBy == Guid.Empty
                && Guid.TryParse(
                    User.FindFirst("sub")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    out var actor))
            {
                request.ChangedBy = actor;
            }

            request.ChangedByRole ??= User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            var result = await _priceChangeService
                .ChangePriceAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
                return BadRequest(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing product price");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("validate")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<PriceChangeValidationResult>> ValidatePriceChange(
        [FromBody] PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request is null)
                return BadRequest(new { message = "Request body is required." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            request.TenantId = tenantId;

            var result = await _priceChangeService
                .ValidatePriceChangeAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsValid)
                return BadRequest(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating product price change");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("versions")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<IEnumerable<ProductPriceVersionItemDto>>> GetVersions(
        [FromQuery] Guid productId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (productId == Guid.Empty)
                return BadRequest(new { message = "productId is required." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var items = await _priceHistoryService
                .GetVersionsAsync(tenantId, productId, take, cancellationToken)
                .ConfigureAwait(false);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing product price versions");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("versions/current")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<ProductPriceVersionItemDto>> GetCurrentVersion(
        [FromQuery] Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (productId == Guid.Empty)
                return BadRequest(new { message = "productId is required." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var item = await _priceHistoryService
                .GetCurrentVersionAsync(tenantId, productId, cancellationToken)
                .ConfigureAwait(false);
            if (item is null)
                return NotFound(new { message = "No current price version found." });
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading current product price version");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

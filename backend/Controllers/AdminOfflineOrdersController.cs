using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin visibility and manual replay for full offline order queue (POS order snapshots).</summary>
[Authorize]
[ApiController]
[Route("api/admin/offline-orders")]
[Produces("application/json")]
public class AdminOfflineOrdersController : BaseController
{
    private readonly IOfflineOrderService _offlineOrderService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminOfflineOrdersController(
        IOfflineOrderService offlineOrderService,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<AdminOfflineOrdersController> logger) : base(logger)
    {
        _offlineOrderService = offlineOrderService;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminOfflineOrdersListResponse>> GetList(
        [FromQuery] string? status = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var response = await _offlineOrderService.ListOrdersForAdminAsync(
                new AdminOfflineOrdersListQuery
                {
                    Status = status,
                    CashRegisterId = cashRegisterId,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                },
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline orders list failed");
            return StatusCode(500, new { message = "Failed to list offline orders", code = "ADMIN_OFFLINE_ORDERS_LIST_ERROR" });
        }
    }

    [HttpPost("{id:guid}/replay")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<ReplayOfflineOrderResult>> ReplayOne(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(GetCurrentUserId()))
            return Unauthorized(new { message = "User not authenticated" });

        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var result = await _offlineOrderService.ReplayOrderByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Offline order not found", code = "ADMIN_OFFLINE_ORDER_NOT_FOUND" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, code = "ADMIN_OFFLINE_ORDER_REPLAY_INVALID" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline order replay failed for {OrderId}", id);
            return StatusCode(500, new { message = "Replay failed", code = "ADMIN_OFFLINE_ORDER_REPLAY_ERROR" });
        }
    }

    [HttpPost("replay-all")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<ReplayOfflineOrdersResult>> ReplayAll(
        [FromQuery] Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(GetCurrentUserId()))
            return Unauthorized(new { message = "User not authenticated" });

        try
        {
            await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var result = await _offlineOrderService.ReplayAllPendingForTenantAsync(cashRegisterId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin offline orders replay-all failed");
            return StatusCode(500, new { message = "Replay-all failed", code = "ADMIN_OFFLINE_ORDERS_REPLAY_ALL_ERROR" });
        }
    }
}

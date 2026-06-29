using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Offline;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS offline order queue — full order snapshots replayed when connectivity returns.</summary>
[ApiController]
[Route("api/pos/offline-orders")]
public class PosOfflineOrdersController : BaseController
{
    private readonly IOfflineOrderService _offlineOrderService;

    public PosOfflineOrdersController(
        ILogger<PosOfflineOrdersController> logger,
        IOfflineOrderService offlineOrderService) : base(logger)
    {
        _offlineOrderService = offlineOrderService;
    }

    [HttpPost]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> Save([FromBody] OfflineOrderRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _offlineOrderService.SaveOfflineOrderAsync(request, ct);
            return Ok(new { success = true, data = response, timestamp = DateTime.UtcNow });
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message, 400);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("pending")]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> GetPending([FromQuery] Guid cashRegisterId, CancellationToken ct)
    {
        if (cashRegisterId == Guid.Empty)
            return ErrorResponse("cashRegisterId is required.", 400);

        var orders = await _offlineOrderService.GetPendingOrdersAsync(cashRegisterId, ct);
        return Ok(new { success = true, data = orders, timestamp = DateTime.UtcNow });
    }

    [HttpPost("replay")]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> Replay([FromQuery] Guid cashRegisterId, CancellationToken ct)
    {
        if (cashRegisterId == Guid.Empty)
            return ErrorResponse("cashRegisterId is required.", 400);

        try
        {
            var result = await _offlineOrderService.ReplayPendingOrdersAsync(cashRegisterId, ct);
            return Ok(new { success = true, data = result, timestamp = DateTime.UtcNow });
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message, 401);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offline order replay failed for CashRegisterId={CashRegisterId}", cashRegisterId);
            return ErrorResponse("Offline order replay failed.", 400);
        }
    }

    [HttpGet("{offlineOrderId}/status")]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> GetStatus(string offlineOrderId, CancellationToken ct)
    {
        try
        {
            var response = await _offlineOrderService.GetOrderStatusAsync(offlineOrderId, ct);
            return Ok(new { success = true, data = response, timestamp = DateTime.UtcNow });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Offline order not found." });
        }
    }
}

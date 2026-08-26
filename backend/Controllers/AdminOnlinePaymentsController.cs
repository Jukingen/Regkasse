using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.OnlinePayments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin FA surface for POS online payments (card/PayPal intents) plus Super Admin test console.
/// Test actions do not create fiscal POS receipts or TSE signatures.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/online-payments")]
[Produces("application/json")]
[Tags("AdminOnlinePayments")]
[HasPermission(AppPermissions.OnlinePaymentsManage)]
public sealed class AdminOnlinePaymentsController : ControllerBase
{
    private readonly IOnlinePaymentAdminService _service;

    public AdminOnlinePaymentsController(IOnlinePaymentAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminOnlinePaymentListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOnlinePaymentListResponse>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(pageNumber, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminOnlinePaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOnlinePaymentDto>> GetById(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var row = await _service.GetByIdAsync(id, ct);
        if (row is null)
            return NotFound();
        return Ok(row);
    }

    /// <summary>
    /// Synthetic test payment or webhook simulation.
    /// <c>action</c>: <c>create</c>, <c>webhookSucceeded</c>/<c>success</c>, <c>webhookFailed</c>/<c>failed</c>, or <c>expire</c>.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(AdminOnlinePaymentTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminOnlinePaymentTestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AdminOnlinePaymentTestResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOnlinePaymentTestResponse>> Test(
        [FromBody] AdminOnlinePaymentTestRequest? body,
        CancellationToken ct = default)
    {
        body ??= new AdminOnlinePaymentTestRequest();
        var result = await _service.RunTestAsync(body, ct);
        if (result.Succeeded)
            return Ok(result);

        return result.Code switch
        {
            OnlinePaymentAdminService.NotFoundCode => NotFound(result),
            OnlinePaymentAdminService.TenantRequiredCode => NotFound(result),
            _ => BadRequest(result)
        };
    }
}

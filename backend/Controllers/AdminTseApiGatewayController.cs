using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE API gateway (provider load-balancing for HealthProbe only — not fiscal Sign).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/api-gateway")]
[Produces("application/json")]
public sealed class AdminTseApiGatewayController : ControllerBase
{
    private readonly ITseApiGatewayService _gateway;

    public AdminTseApiGatewayController(ITseApiGatewayService gateway)
    {
        _gateway = gateway;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseGatewayStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseGatewayStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _gateway.GetGatewayStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("config")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseGatewayConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseGatewayConfigDto>> GetConfig(CancellationToken cancellationToken)
    {
        return Ok(await _gateway.GetGatewayConfigAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpPut("config")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseGatewayConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseGatewayConfigDto>> Configure(
        [FromBody] ConfigureTseGatewayRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new ConfigureTseGatewayRequestDto();
        try
        {
            return Ok(await _gateway
                .ConfigureGatewayAsync(body, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("route")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseGatewayResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseGatewayResponseDto>> Route(
        [FromBody] TseGatewayRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new TseGatewayRequestDto();
        return Ok(await _gateway
            .RouteRequestAsync(body, User.GetActorUserId(), cancellationToken)
            .ConfigureAwait(false));
    }
}

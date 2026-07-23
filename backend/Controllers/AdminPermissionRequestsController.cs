using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/permission-requests")]
[Produces("application/json")]
public sealed class AdminPermissionRequestsController : ControllerBase
{
    private readonly IPermissionRequestService _requests;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminPermissionRequestsController(
        IPermissionRequestService requests,
        ICurrentTenantAccessor tenantAccessor)
    {
        _requests = requests;
        _tenantAccessor = tenantAccessor;
    }

    [HttpPost]
    [HasPermission(AppPermissions.UserView)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PermissionRequestMutationResult>> Create(
        [FromBody] CreatePermissionRequestBody body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _requests.CreateAsync(userId, _tenantAccessor.TenantId, body, cancellationToken);
        if (!result.Succeeded)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("mine")]
    [HasPermission(AppPermissions.UserView)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionRequestDto>>> ListMine(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        return Ok(await _requests.ListMineAsync(userId, cancellationToken));
    }

    [HttpGet("pending")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionRequestDto>>> ListPending(CancellationToken cancellationToken)
    {
        return Ok(await _requests.ListPendingAsync(cancellationToken));
    }

    [HttpGet("stats")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionRequestStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionRequestStatsDto>> Stats(CancellationToken cancellationToken)
    {
        return Ok(await _requests.GetStatsAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionRequestMutationResult>> Approve(
        [FromRoute] Guid id,
        [FromBody] ResolvePermissionRequestBody? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _requests.ApproveAsync(id, userId, body, cancellationToken);
        if (!result.Succeeded)
        {
            return result.Code == PermissionRequestService.NotFoundCode
                ? NotFound(result)
                : BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PermissionRequestMutationResult), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermissionRequestMutationResult>> Reject(
        [FromRoute] Guid id,
        [FromBody] ResolvePermissionRequestBody? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var result = await _requests.RejectAsync(id, userId, body, cancellationToken);
        if (!result.Succeeded)
        {
            return result.Code == PermissionRequestService.NotFoundCode
                ? NotFound(result)
                : BadRequest(result);
        }

        return Ok(result);
    }
}

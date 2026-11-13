using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE resource pools for multi-tenant capacity grouping.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/resource-pools")]
[Produces("application/json")]
public sealed class AdminTseResourcePoolsController : ControllerBase
{
    private readonly ITseResourcePoolService _pools;

    public AdminTseResourcePoolsController(ITseResourcePoolService pools)
    {
        _pools = pools;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseResourcePoolDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseResourcePoolDto>>> List(CancellationToken cancellationToken)
    {
        var pools = await _pools.ListResourcePoolsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(pools);
    }

    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseResourcePoolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseResourcePoolDto>> Create(
        [FromBody] CreateTseResourcePoolRequestDto body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var pool = await _pools.CreateResourcePoolAsync(body, actor, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(GetById), new { poolId = pool.Id }, pool);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{poolId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseResourcePoolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseResourcePoolDto>> GetById(
        Guid poolId,
        CancellationToken cancellationToken)
    {
        try
        {
            var pool = await _pools.GetPoolAsync(poolId, cancellationToken).ConfigureAwait(false);
            return Ok(pool);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{poolId:guid}/status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TsePoolStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TsePoolStatusDto>> GetStatus(
        Guid poolId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _pools.GetPoolStatusAsync(poolId, cancellationToken).ConfigureAwait(false);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{poolId:guid}/metrics")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TsePoolMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TsePoolMetricsDto>> GetMetrics(
        Guid poolId,
        CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _pools.GetPoolMetricsAsync(poolId, cancellationToken).ConfigureAwait(false);
            return Ok(metrics);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("assign")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TsePoolAssignmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TsePoolAssignmentResultDto>> Assign(
        [FromBody] AssignTenantToTsePoolRequestDto body,
        CancellationToken cancellationToken)
    {
        var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _pools
            .AssignTenantToPoolAsync(
                body.TenantId,
                body.PoolId,
                body.ReservedCapacity <= 0 ? 1 : body.ReservedCapacity,
                actor,
                cancellationToken)
            .ConfigureAwait(false);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("unassign")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TsePoolAssignmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TsePoolAssignmentResultDto>> Unassign(
        [FromBody] AssignTenantToTsePoolRequestDto body,
        CancellationToken cancellationToken)
    {
        var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _pools
            .UnassignTenantAsync(body.TenantId, actor, cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

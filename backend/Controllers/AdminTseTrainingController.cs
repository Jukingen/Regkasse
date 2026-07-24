using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE training modules + Development simulation drills (diagnostic only).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/training")]
[Produces("application/json")]
public sealed class AdminTseTrainingController : ControllerBase
{
    private readonly ITseTrainingService _training;

    public AdminTseTrainingController(ITseTrainingService training)
    {
        _training = training;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseTrainingEnvironmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseTrainingEnvironmentDto>> GetEnvironment(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        return Ok(await _training.GetEnvironmentAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("modules")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseTrainingModuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseTrainingModuleDto>>> ListModules(
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        return Ok(await _training.GetModulesAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("modules/{moduleId}/start")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseTrainingModuleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseTrainingModuleDto>> StartModule(
        string moduleId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            return Ok(await _training
                .StartModuleAsync(userId, moduleId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("console")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseTrainingConsoleEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseTrainingConsoleEntryDto>>> GetConsole(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var entries = await _training.GetConsoleAsync(userId, take).ConfigureAwait(false);
        return Ok(entries);
    }

    [HttpDelete("console")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ClearConsole()
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        _training.ClearConsole(userId);
        return NoContent();
    }

    [HttpPost("simulate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseTrainingSimulateResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseTrainingSimulateResultDto>> Simulate(
        [FromBody] TseTrainingSimulateRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        body ??= new TseTrainingSimulateRequestDto();
        var result = await _training
            .SimulateFailureAsync(userId, body.DeviceId, body.FailureType, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && string.Equals(result.Error, "TSE device not found.", StringComparison.Ordinal))
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("reset")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseTrainingSimulateResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseTrainingSimulateResultDto>> Reset(
        [FromBody] TseTrainingSimulateRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        body ??= new TseTrainingSimulateRequestDto();
        var result = await _training
            .ResetSimulationAsync(userId, body.DeviceId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && string.Equals(result.Error, "TSE device not found.", StringComparison.Ordinal))
            return NotFound(result);

        return Ok(result);
    }
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only TSE developer experience tools (diagnostics, synthetic traffic, config, seeds).
/// Mutations return <see cref="NotFoundResult"/> outside Development.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/developer-tools")]
[Produces("application/json")]
public sealed class AdminTseDeveloperToolsController : ControllerBase
{
    private readonly ITseDeveloperToolsService _tools;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminTseDeveloperToolsController> _logger;

    public AdminTseDeveloperToolsController(
        ITseDeveloperToolsService tools,
        IHostEnvironment environment,
        ILogger<AdminTseDeveloperToolsController> logger)
    {
        _tools = tools;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("availability")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDeveloperToolsAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseDeveloperToolsAvailabilityDto>> GetAvailability(
        CancellationToken cancellationToken)
    {
        // Available in all environments so FA can disable the UI outside Development.
        return Ok(await _tools.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("diagnostics")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDevToolResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDevToolResultDto>> RunDiagnostics(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _tools.RunDiagnosticsAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("simulate-traffic")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDevToolResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDevToolResultDto>> SimulateTraffic(
        [FromQuery] Guid tenantId,
        [FromBody] TseSimulateTrafficRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        body ??= new TseSimulateTrafficRequestDto();
        try
        {
            return Ok(await _tools
                .SimulateTrafficAsync(
                    tenantId,
                    body.TransactionCount,
                    User.GetActorUserId(),
                    cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("validate-config")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDevToolResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDevToolResultDto>> ValidateConfig(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _tools.ValidateConfigAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("generate-test-data")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseDevToolResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseDevToolResultDto>> GenerateTestData(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _tools
                .GenerateTestDataAsync(tenantId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private ActionResult? EnsureNotDevelopment()
    {
        if (_environment.IsDevelopment())
            return null;

        _logger.LogWarning("TSE developer tools API rejected: endpoint is only available in Development.");
        return NotFound();
    }
}

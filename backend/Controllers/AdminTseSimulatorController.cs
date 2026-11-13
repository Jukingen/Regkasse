using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only TSE QA simulator. Returns <see cref="NotFoundResult"/> outside Development.
/// Mutates device health fields only — never fiscal signature rows.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse-management/simulator")]
[Produces("application/json")]
public sealed class AdminTseSimulatorController : ControllerBase
{
    private readonly ITseSimulatorService _simulator;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminTseSimulatorController> _logger;

    public AdminTseSimulatorController(
        ITseSimulatorService simulator,
        IHostEnvironment environment,
        ILogger<AdminTseSimulatorController> logger)
    {
        _simulator = simulator;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("scenarios")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseSimulationScenarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TseSimulationScenarioDto>>> ListScenarios(
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        var scenarios = await _simulator.GetAvailableScenariosAsync(cancellationToken).ConfigureAwait(false);
        return Ok(scenarios);
    }

    [HttpGet("devices/{deviceId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSimulationDeviceSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSimulationDeviceSnapshotDto>> GetSnapshot(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        var snap = await _simulator.GetSnapshotAsync(deviceId, cancellationToken).ConfigureAwait(false);
        return snap is null ? NotFound() : Ok(snap);
    }

    [HttpPost("devices/{deviceId:guid}/failure")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSimulationResultDto>> SimulateFailure(
        Guid deviceId,
        [FromBody] TseSimulateFailureRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        body ??= new TseSimulateFailureRequestDto();
        if (!Enum.TryParse<TseSimulatorFailureType>(body.FailureType, ignoreCase: true, out var type))
        {
            return BadRequest(new
            {
                error = "Invalid failureType.",
                allowed = Enum.GetNames<TseSimulatorFailureType>(),
            });
        }

        var result = await _simulator
            .SimulateTseFailureAsync(deviceId, type, User.GetActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("devices/{deviceId:guid}/latency")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSimulationResultDto>> SimulateLatency(
        Guid deviceId,
        [FromBody] TseSimulateLatencyRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        body ??= new TseSimulateLatencyRequestDto();
        var result = await _simulator
            .SimulateNetworkLatencyAsync(deviceId, body.LatencyMs, User.GetActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("devices/{deviceId:guid}/certificate-expiry")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSimulationResultDto>> SimulateCertificateExpiry(
        Guid deviceId,
        [FromBody] TseSimulateCertificateExpiryRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        body ??= new TseSimulateCertificateExpiryRequestDto();
        if (body.ExpiryDateUtc == default)
            return BadRequest(new { error = "expiryDateUtc is required." });

        var result = await _simulator
            .SimulateCertificateExpiryAsync(
                deviceId,
                body.ExpiryDateUtc,
                User.GetActorUserId(),
                cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("devices/{deviceId:guid}/reset")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseSimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseSimulationResultDto>> Reset(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        var result = await _simulator
            .ResetSimulationAsync(deviceId, User.GetActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    private ActionResult ToActionResult(TseSimulationResultDto result)
    {
        if (!result.Success)
        {
            if (string.Equals(result.Error, "TSE device not found.", StringComparison.Ordinal))
                return NotFound(result);
            if (result.Error?.Contains("Development", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }

    private ActionResult? EnsureNotDevelopment()
    {
        if (_environment.IsDevelopment())
            return null;

        _logger.LogWarning("TSE simulator API rejected: endpoint is only available in Development.");
        return NotFound();
    }
}

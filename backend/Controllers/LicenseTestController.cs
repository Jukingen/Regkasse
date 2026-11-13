using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.LicenseTest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only Super Admin license QA API. All routes return <see cref="NotFoundResult"/> outside Development.
/// </summary>
[ApiController]
[Route("api/admin/license/test")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class LicenseTestController : ControllerBase
{
    private readonly ILicenseTestService _licenseTestService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<LicenseTestController> _logger;

    public LicenseTestController(
        ILicenseTestService licenseTestService,
        IHostEnvironment environment,
        ILogger<LicenseTestController> logger)
    {
        _licenseTestService = licenseTestService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Current tenant (optional) and deployment license snapshot.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(LicenseTestSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTestSnapshotDto>> GetSnapshot(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        var snapshot = await _licenseTestService.GetSnapshotAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(snapshot);
    }

    /// <summary>Unified tenant license update for the dev test panel.</summary>
    [HttpPost("update")]
    [ProducesResponseType(typeof(LicenseTestSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTestSnapshotDto>> Update(
        [FromBody] LicenseTestRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (request == null || request.TenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        try
        {
            var snapshot = await _licenseTestService.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(snapshot);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tenant not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Adjust mandant (tenant row) license expiry or toggle active/expired.</summary>
    [HttpPost("tenant")]
    [ProducesResponseType(typeof(LicenseTestSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTestSnapshotDto>> SetTenantExpiry(
        [FromBody] LicenseTestTenantRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (request == null || request.TenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        try
        {
            var snapshot = await _licenseTestService
                .SetTenantExpiryAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(snapshot);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tenant not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Adjust deployment (on-premise) license expiry or toggle active/expired.</summary>
    [HttpPost("deployment")]
    [ProducesResponseType(typeof(LicenseTestSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTestSnapshotDto>> SetDeploymentExpiry(
        [FromBody] LicenseTestSetExpiryRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (request == null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var snapshot = await _licenseTestService
                .SetDeploymentExpiryAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(snapshot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Apply a preset scenario (1 / 7 / 30 days or expired) to tenant, deployment, or both.</summary>
    [HttpPost("scenario")]
    [ProducesResponseType(typeof(LicenseTestSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTestSnapshotDto>> ApplyScenario(
        [FromBody] LicenseTestScenarioRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (request == null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var snapshot = await _licenseTestService
                .ApplyScenarioAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(snapshot);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Re-read license state from DB and disk without mutating expiry.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LicenseTestSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTestSnapshotDto>> Refresh(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        var snapshot = await _licenseTestService.GetSnapshotAsync(tenantId, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("License test snapshot refreshed for tenant {TenantId}", tenantId);
        return Ok(snapshot);
    }

    /// <summary>Returns <see cref="NotFoundResult"/> when the host is not Development (fail-closed for QA APIs).</summary>
    private ActionResult? EnsureNotDevelopment()
    {
        if (_environment.IsDevelopment())
            return null;

        _logger.LogWarning("License test API rejected: endpoint is only available in Development.");
        return NotFound();
    }
}

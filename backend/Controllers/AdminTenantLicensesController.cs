using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super-admin tenant row license management (SaaS metadata + issued_licenses cross-reference).</summary>
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/license")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminTenantLicensesController : ControllerBase
{
    private readonly IAdminTenantLicenseService _licenseService;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<AdminTenantLicensesController> _logger;

    public AdminTenantLicensesController(
        IAdminTenantLicenseService licenseService,
        IAuthorizationService authorization,
        ILogger<AdminTenantLicensesController> logger)
    {
        _licenseService = licenseService;
        _authorization = authorization;
        _logger = logger;
    }

    private string? ActorUserId => User.GetActorUserId();

    [HttpGet]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> Get(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var overview = await _licenseService.GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (overview == null)
            return NotFound(new { message = "Tenant not found." });
        return Ok(overview);
    }

    [HttpPost("trial")]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> ActivateTrial(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _licenseService.ActivateTrialAsync(tenantId, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("extend")]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> Extend(
        Guid tenantId,
        [FromBody] ExtendTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _licenseService.ExtendAsync(tenantId, request, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("tier")]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> SetTier(
        Guid tenantId,
        [FromBody] SetTenantLicenseTierRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (result, error) = await _licenseService.SetTierAsync(tenantId, request, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });

        _logger.LogInformation("Tenant {TenantId} license tier set to {Tier}", tenantId, request.Tier);
        return Ok(result);
    }

    /// <summary>
    /// Compares mandant <c>license_valid_until_utc</c> with linked <c>issued_licenses</c> rows (key, name, or <c>[tenant:guid]</c> marker).
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(TenantLicenseConsistencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseConsistencyDto>> SyncConsistency(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanSyncDeploymentLicenseAsync().ConfigureAwait(false))
            return Forbid();

        var (result, error) = await _licenseService
            .CheckDeploymentConsistencyAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    /// <summary>Issues a floating deployment JWT in <c>issued_licenses</c> aligned to the mandant end date.</summary>
    [HttpPost("sync/issue")]
    [ProducesResponseType(typeof(TenantLicenseIssueDeploymentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TenantLicenseIssueDeploymentResultDto>> IssueDeploymentLicense(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanSyncDeploymentLicenseAsync().ConfigureAwait(false))
            return Forbid();

        try
        {
            var (result, error) = await _licenseService
                .IssueDeploymentLicenseAsync(tenantId, ActorUserId, cancellationToken)
                .ConfigureAwait(false);
            if (error == "Tenant not found.")
                return NotFound(new { message = error });
            if (error != null && error.Contains("not configured", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = error });
            if (error != null)
                return BadRequest(new { message = error });
            return Ok(result);
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    private async Task<bool> CanSyncDeploymentLicenseAsync()
    {
        if (User.IsInRole(Roles.SuperAdmin))
            return true;

        var auth = await _authorization
            .AuthorizeAsync(User, null, PermissionCatalog.PolicyPrefix + AppPermissions.SettingsManage)
            .ConfigureAwait(false);
        return auth.Succeeded;
    }
}

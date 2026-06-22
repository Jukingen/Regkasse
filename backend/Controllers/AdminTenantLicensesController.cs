using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant row license management (SaaS Mandantenlizenz). Managers: own tenant GET/PUT only.</summary>
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/license")]
[Authorize]
[Produces("application/json")]
public sealed class AdminTenantLicensesController : ControllerBase
{
    private readonly IAdminTenantLicenseService _licenseService;
    private readonly ILicenseRenewalService _licenseRenewalService;
    private readonly IAuthorizationService _authorization;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ITenantLicenseExtensionRateLimiter _extensionRateLimiter;
    private readonly AppDbContext _db;
    private readonly ILogger<AdminTenantLicensesController> _logger;

    public AdminTenantLicensesController(
        IAdminTenantLicenseService licenseService,
        ILicenseRenewalService licenseRenewalService,
        IAuthorizationService authorization,
        ISettingsTenantResolver settingsTenantResolver,
        ITenantLicenseExtensionRateLimiter extensionRateLimiter,
        AppDbContext db,
        ILogger<AdminTenantLicensesController> logger)
    {
        _licenseService = licenseService;
        _licenseRenewalService = licenseRenewalService;
        _authorization = authorization;
        _settingsTenantResolver = settingsTenantResolver;
        _extensionRateLimiter = extensionRateLimiter;
        _db = db;
        _logger = logger;
    }

    private string? ActorUserId => User.GetActorUserId();
    private string? ActorRole => User.GetActorRole();

    [HttpGet]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> Get(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var accessError = await EnsureTenantLicenseAccessAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (accessError != null)
            return accessError;

        var overview = await _licenseService.GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (overview == null)
            return NotFound(new { message = "Tenant not found." });
        return Ok(overview);
    }

    /// <summary>Update mandant license (REGK key and/or valid-until). Managers: both fields required.</summary>
    [HttpPut]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> Put(
        Guid tenantId,
        [FromBody] ExtendTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var accessError = await EnsureTenantLicenseAccessAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (accessError != null)
            return accessError;

        if (!User.IsInRole(Roles.SuperAdmin))
        {
            if (string.IsNullOrWhiteSpace(request.LicenseKey))
                return BadRequest(new { message = "licenseKey is required." });
            if (!request.ValidUntilUtc.HasValue)
                return BadRequest(new { message = "validUntilUtc is required." });

            var rateError = _extensionRateLimiter.TryAcquireOrError(ActorUserId, tenantId);
            if (rateError != null)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = rateError });
        }

        if (!string.IsNullOrWhiteSpace(request.LicenseKey)
            && !RegkTenantLicenseKeyFormat.IsValid(request.LicenseKey))
        {
            return BadRequest(new { message = RegkTenantLicenseKeyFormat.InvalidFormatMessage });
        }

        var (result, error) = await _licenseService
            .ExtendAsync(tenantId, request, ActorUserId, ActorRole, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });

        _logger.LogInformation("Tenant license updated for tenant {TenantId} by {ActorUserId}", tenantId, ActorUserId);
        return Ok(result);
    }

    [HttpPost("trial")]
    [Authorize(Roles = Roles.SuperAdmin)]
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
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> Extend(
        Guid tenantId,
        [FromBody] ExtendTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _licenseService.ExtendAsync(tenantId, request, ActorUserId, ActorRole, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("renew")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(LicenseRenewalResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseRenewalResult>> Renew(
        Guid tenantId,
        [FromBody] RenewTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _licenseRenewalService
            .RenewLicenseAsync(tenantId, request.AdditionalMonths, ActorUserId, ActorRole, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (string.Equals(result.Message, "Tenant not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("reminder")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(TenantLicenseReminderResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TenantLicenseReminderResultDto>> SendReminder(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _licenseService.SendReminderEmailAsync(tenantId, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null && error.Contains("SMTP", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("tier")]
    [Authorize(Roles = Roles.SuperAdmin)]
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
    [Authorize(Roles = Roles.SuperAdmin)]
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
    [Authorize(Roles = Roles.SuperAdmin)]
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

    private async Task<ActionResult?> EnsureTenantLicenseAccessAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return NotFound(new { message = "Tenant not found." });

        var exists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            return NotFound(new { message = "Tenant not found." });

        if (User.IsInRole(Roles.SuperAdmin))
            return null;

        Guid effectiveTenantId;
        try
        {
            effectiveTenantId = await _settingsTenantResolver
                .ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve effective tenant for license API");
            return NotFound(new { message = "Tenant not found." });
        }

        if (effectiveTenantId != tenantId)
            return NotFound(new { message = "Tenant not found." });

        return null;
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

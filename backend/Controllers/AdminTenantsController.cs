using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Controllers;

/// <summary>Super-admin SaaS tenant management and support impersonation.</summary>
[ApiController]
[Route("api/admin/tenants")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminTenantsController : ControllerBase
{
    private readonly IAdminTenantService _tenantService;
    private readonly IAdminTenantLicenseService _tenantLicenseService;
    private readonly ITenantDeletionService _tenantDeletionService;
    private readonly IAuditLogService _auditLogService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminTenantsController> _logger;

    public AdminTenantsController(
        IAdminTenantService tenantService,
        IAdminTenantLicenseService tenantLicenseService,
        ITenantDeletionService tenantDeletionService,
        IAuditLogService auditLogService,
        IHostEnvironment environment,
        ILogger<AdminTenantsController> logger)
    {
        _tenantService = tenantService;
        _tenantLicenseService = tenantLicenseService;
        _tenantDeletionService = tenantDeletionService;
        _auditLogService = auditLogService;
        _environment = environment;
        _logger = logger;
    }

    private string? ActorUserId => User.GetActorUserId();

    /// <summary>List all tenants (optionally include soft-deleted).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTenantListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminTenantListItemDto>>> List(
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _tenantService.ListAsync(includeDeleted, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Super Admin mandant license inventory for <c>/admin/license</c> overview table.</summary>
    [HttpGet("license-overview")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantLicenseOverviewListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantLicenseOverviewListItemDto>>> ListLicenseOverview(
        CancellationToken cancellationToken = default)
    {
        var items = await _tenantLicenseService.ListOverviewAsync(cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Suggest available subdomain slugs from company name and/or preferred slug.</summary>
    [HttpGet("slug-suggestions")]
    [ProducesResponseType(typeof(TenantSlugSuggestionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantSlugSuggestionsDto>> GetSlugSuggestions(
        [FromQuery] string? name,
        [FromQuery] string? slug,
        [FromQuery] int max = 5,
        CancellationToken cancellationToken = default)
    {
        var suggestions = await _tenantService
            .GetSlugSuggestionsAsync(name, slug, Math.Clamp(max, 1, 10), cancellationToken)
            .ConfigureAwait(false);
        return Ok(new TenantSlugSuggestionsDto(suggestions));
    }

    /// <summary>Check whether a tenant slug is valid and not already taken.</summary>
    [HttpGet("slug-availability")]
    [ProducesResponseType(typeof(TenantSlugAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantSlugAvailabilityDto>> CheckSlugAvailability(
        [FromQuery] string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Ok(new TenantSlugAvailabilityDto(string.Empty, IsValid: false, Available: false));

        var result = await _tenantService.CheckSlugAvailabilityAsync(slug, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Get tenant by id.</summary>
    [HttpGet("{tenantId:guid}")]
    [ProducesResponseType(typeof(AdminTenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminTenantDetailDto>> GetById(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var item = await _tenantService.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (item == null)
            return NotFound(new { message = "Tenant not found." });
        return Ok(item);
    }

    /// <summary>List cash registers for a tenant (super-admin cross-tenant view).</summary>
    [HttpGet("{tenantId:guid}/cash-registers")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTenantCashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AdminTenantCashRegisterDto>>> ListCashRegisters(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await _tenantService.ListCashRegistersAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (items == null)
            return NotFound(new { message = "Tenant not found." });
        return Ok(items);
    }

    /// <summary>Return tenant decommission preflight checks for the super-admin wizard.</summary>
    [HttpGet("{tenantId:guid}/decommission-checks")]
    [ProducesResponseType(typeof(TenantDecommissionChecksDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantDecommissionChecksDto>> GetDecommissionChecks(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var checks = await _tenantService.GetDecommissionChecksAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (checks == null)
            return NotFound(new { message = "Tenant not found." });

        return Ok(checks);
    }

    /// <summary>Create a new tenant (generates id, unique slug).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminTenantDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminTenantDetailDto>> Create(
        [FromBody] CreateAdminTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (result, failure) = await _tenantService
            .CreateWithFailureDetailAsync(request, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (failure != null)
        {
            return BadRequest(new
            {
                message = failure.Message,
                code = failure.Code,
                suggestions = failure.SlugSuggestions,
            });
        }

        return CreatedAtAction(nameof(GetById), new { tenantId = result!.Id }, result);
    }

    /// <summary>Import the demo menu catalog (Salate, Pizzas, Pasta, …) for an existing tenant.</summary>
    [HttpPost("{tenantId:guid}/demo-products/import")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResult>> ImportDemoProducts(
        Guid tenantId,
        [FromBody] DemoImportRequest? request,
        [FromServices] IDemoProductImportService importService,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantService.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return NotFound(new { message = "Tenant not found." });

        var result = await importService
            .ImportDemoProductsAsync(tenantId, request ?? new DemoImportRequest(), progress: null, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && string.Equals(result.ErrorMessage, "Tenant not found.", StringComparison.Ordinal))
            return NotFound(new { message = result.ErrorMessage });

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage ?? "Demo product import failed.", result });

        return Ok(result);
    }

    /// <summary>Start background demo catalog import with SignalR progress for a tenant.</summary>
    [HttpPost("{tenantId:guid}/demo-products/import/jobs")]
    [ProducesResponseType(typeof(DemoImportJobStartResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DemoImportJobStartResponseDto>> StartDemoImportJob(
        Guid tenantId,
        [FromBody] DemoImportRequest? request,
        [FromServices] IDemoProductImportJobManager jobManager,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantService.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return NotFound(new { message = "Tenant not found." });

        var started = await jobManager
            .StartCatalogImportAsync(tenantId, request ?? new DemoImportRequest(), User, cancellationToken)
            .ConfigureAwait(false);

        return Accepted(started);
    }

    /// <summary>Poll demo import job progress for a tenant import.</summary>
    [HttpGet("demo-products/import/jobs/{jobId}")]
    [ProducesResponseType(typeof(DemoImportJobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DemoImportJobStatusDto> GetDemoImportJobStatus(
        string jobId,
        [FromServices] IDemoProductImportJobManager jobManager)
    {
        if (!jobManager.TryAuthorizeSubscription(User, jobId))
            return NotFound();

        var status = jobManager.GetStatus(jobId);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>Import demo products from uploaded CSV/Excel template for a tenant (super-admin).</summary>
    [HttpPost("{tenantId:guid}/demo-products/template/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResult>> ImportDemoTemplateForTenant(
        Guid tenantId,
        IFormFile? file,
        [FromForm] bool overwriteExisting = false,
        [FromForm] string? priceAdjustmentMode = null,
        [FromForm] decimal? priceAdjustmentPercent = null,
        [FromForm] decimal? priceRoundIncrement = null,
        [FromForm] string? imageMode = null,
        [FromServices] IDemoProductImportService importService = null!,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantService.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return NotFound(new { message = "Tenant not found." });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        var request = new DemoImportRequest
        {
            OverwriteExisting = overwriteExisting,
            PriceAdjustmentMode = priceAdjustmentMode,
            PriceAdjustmentPercent = priceAdjustmentPercent,
            PriceRoundIncrement = priceRoundIncrement,
            ImageMode = imageMode,
        };

        await using var stream = file.OpenReadStream();
        var result = await importService
            .ImportFromTemplateFileAsync(tenantId, stream, file.FileName, request, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && string.Equals(result.ErrorMessage, "Tenant not found.", StringComparison.Ordinal))
            return NotFound(new { message = result.ErrorMessage });

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage ?? "Template import failed.", result });

        return Ok(result);
    }

    /// <summary>Update tenant metadata or status.</summary>
    [HttpPut("{tenantId:guid}")]
    [ProducesResponseType(typeof(AdminTenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminTenantDetailDto>> Update(
        Guid tenantId,
        [FromBody] UpdateAdminTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var (result, error) = await _tenantService.UpdateAsync(tenantId, request, ActorUserId, cancellationToken).ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    /// <summary>Return tenant dependency counts and permanent-delete eligibility for Super Admin UI.</summary>
    [HttpGet("{tenantId:guid}/delete-dependencies")]
    [ProducesResponseType(typeof(TenantDeleteDependenciesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeleteDependencies(
        Guid tenantId,
        CancellationToken ct = default)
    {
        try
        {
            var dto = await _tenantDeletionService
                .GetDependencySummaryAsync(tenantId, ct)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tenant not found." });
        }
    }

    /// <summary>Permanently delete a soft-deleted tenant without fiscal data (requires slug confirmation).</summary>
    [HttpDelete("{tenantId:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(TenantPermanentDeleteErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TenantPermanentDeleteErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDelete(
        Guid tenantId,
        [FromBody] HardDeleteAdminTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        TenantDeleteDependenciesDto dependencies;
        try
        {
            dependencies = await _tenantDeletionService
                .GetDependencySummaryAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tenant not found." });
        }

        if (!_environment.IsDevelopment())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new TenantPermanentDeleteErrorResponse(
                    "Permanent tenant deletion is disabled outside Development. Use soft-delete to archive the tenant.",
                    TenantPermanentDeleteFailureCodes.ProductionPolicy,
                    dependencies));
        }

        var validation = await _tenantDeletionService
            .ValidateHardDeleteAsync(tenantId, forceDelete: false, cancellationToken)
            .ConfigureAwait(false);
        if (!validation.Success)
        {
            if (string.Equals(
                    validation.ErrorCode,
                    TenantPermanentDeleteFailureCodes.TenantNotFound,
                    StringComparison.Ordinal))
            {
                return NotFound(new { message = validation.ErrorMessage ?? "Tenant not found." });
            }

            return BadRequest(new TenantPermanentDeleteErrorResponse(
                validation.ErrorMessage ?? "Permanent delete is not allowed for this tenant.",
                validation.ErrorCode ?? TenantPermanentDeleteFailureCodes.RemainingDependencies,
                dependencies));
        }

        var result = await _tenantService
            .HardDeleteAsync(tenantId, request, ActorUserId, cancellationToken)
            .ConfigureAwait(false);

        return MapPermanentDeleteResult(result);
    }

    /// <summary>
    /// Development-only shortcut: soft-delete the tenant when needed, then run the existing permanent delete flow
    /// using the tenant slug as confirmation.
    /// </summary>
    [HttpDelete("{tenantId:guid}/hard")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDeleteDevelopment(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            return BadRequest(new { message = "Hard delete only allowed in development." });

        var tenant = await _tenantService.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return NotFound(new { message = "Tenant not found." });

        var (softDeleteSuccess, softDeleteError) = await _tenantService
            .SoftDeleteAsync(tenantId, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (softDeleteError == "Tenant not found.")
            return NotFound(new { message = softDeleteError });
        if (!softDeleteSuccess)
            return BadRequest(new { message = softDeleteError });

        var hardDeleteResult = await _tenantService
            .HardDeleteAsync(
                tenantId,
                new HardDeleteAdminTenantRequest { ConfirmSlug = tenant.Slug },
                ActorUserId,
                cancellationToken)
            .ConfigureAwait(false);

        return MapPermanentDeleteResult(hardDeleteResult);
    }

    /// <summary>Soft-delete tenant (status=deleted, is_active=false).</summary>
    [HttpDelete("{tenantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var (success, error) = await _tenantService.SoftDeleteAsync(tenantId, ActorUserId, cancellationToken).ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (!success)
            return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Restore a soft-deleted tenant (status=active, is_active=true).</summary>
    [HttpPost("{tenantId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var (success, error) = await _tenantService.RestoreAsync(tenantId, ActorUserId, cancellationToken).ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (!success)
            return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Decommission all tenant cash registers and then soft-delete the tenant.</summary>
    [HttpPost("{tenantId:guid}/decommission")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Decommission(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var actorUserId = ActorUserId;
        if (string.IsNullOrWhiteSpace(actorUserId))
            return Unauthorized(new { message = "User not authenticated." });

        var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;
        var (success, error, checks) = await _tenantService
            .DecommissionAsync(tenantId, actorUserId, actorRole, cancellationToken)
            .ConfigureAwait(false);

        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (!success)
            return BadRequest(new { message = error, checks });

        return Ok();
    }

    /// <summary>Issue a short-lived admin JWT scoped to the target tenant (support impersonation).</summary>
    [HttpPost("{tenantId:guid}/impersonate")]
    [ProducesResponseType(typeof(TenantImpersonationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantImpersonationResponseDto>> Impersonate(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorId = ActorUserId;
        if (string.IsNullOrEmpty(actorId))
            return Unauthorized(new { message = "User not authenticated." });

        var (result, error) = await _tenantService.ImpersonateAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });

        _logger.LogInformation("Impersonation token issued for tenant {TenantId} by {ActorUserId}", tenantId, actorId);

        try
        {
            await _auditLogService
                .LogImpersonationSessionStartedAsync(
                    actorId,
                    Roles.SuperAdmin,
                    tenantId,
                    result!.TenantSlug,
                    HttpContext.TraceIdentifier)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write impersonation start audit for tenant {TenantId}", tenantId);
        }

        return Ok(result);
    }

    private IActionResult MapPermanentDeleteResult(TenantPermanentDeleteResult result)
    {
        if (result.Success)
            return NoContent();

        if (string.Equals(result.Code, TenantPermanentDeleteFailureCodes.TenantNotFound, StringComparison.Ordinal))
            return NotFound(new { message = result.Message, code = result.Code });

        var body = new TenantPermanentDeleteErrorResponse(
            result.Message ?? "Permanent delete failed.",
            result.Code ?? TenantPermanentDeleteFailureCodes.RemainingDependencies,
            result.Dependencies);

        if (string.Equals(result.Code, TenantPermanentDeleteFailureCodes.ProductionPolicy, StringComparison.Ordinal)
            || string.Equals(result.Code, TenantPermanentDeleteFailureCodes.ProductionDisabled, StringComparison.Ordinal))
            return StatusCode(StatusCodes.Status403Forbidden, body);

        return BadRequest(body);
    }
}

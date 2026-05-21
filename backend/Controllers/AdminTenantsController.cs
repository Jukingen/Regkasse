using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super-admin SaaS tenant management and support impersonation.</summary>
[ApiController]
[Route("api/admin/tenants")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminTenantsController : ControllerBase
{
    private readonly IAdminTenantService _tenantService;
    private readonly ILogger<AdminTenantsController> _logger;

    public AdminTenantsController(IAdminTenantService tenantService, ILogger<AdminTenantsController> logger)
    {
        _tenantService = tenantService;
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

    /// <summary>Permanently delete a soft-deleted tenant without fiscal data (requires slug confirmation).</summary>
    [HttpDelete("{tenantId:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HardDelete(
        Guid tenantId,
        [FromBody] HardDeleteAdminTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (success, error) = await _tenantService
            .HardDeleteAsync(tenantId, request, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (!success)
            return BadRequest(new { message = error });
        return NoContent();
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
        return Ok(result);
    }
}

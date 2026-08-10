using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Onboarding;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Mandant / Super Admin guided onboarding checklist.</summary>
[ApiController]
[Route("api/admin/onboarding")]
[Authorize]
[Produces("application/json")]
public sealed class AdminOnboardingController : ControllerBase
{
    private readonly ITenantOnboardingChecklistService _checklist;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminOnboardingController(
        ITenantOnboardingChecklistService checklist,
        ICurrentTenantAccessor tenantAccessor)
    {
        _checklist = checklist;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("{tenantId:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Manager}")]
    [ProducesResponseType(typeof(TenantOnboardingOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantOnboardingOverviewDto>> Get(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!CanAccessTenant(tenantId))
            return NotFound();

        var overview = await _checklist.EnsureAndGetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(overview);
    }

    [HttpPost("{tenantId:guid}/steps/{step}/complete")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Manager}")]
    [ProducesResponseType(typeof(TenantOnboardingOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantOnboardingOverviewDto>> CompleteStep(
        Guid tenantId,
        string step,
        CancellationToken cancellationToken)
    {
        if (!CanAccessTenant(tenantId))
            return NotFound();

        try
        {
            var overview = await _checklist
                .CompleteStepAsync(tenantId, step, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false);
            return Ok(overview);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool CanAccessTenant(Guid tenantId)
    {
        if (User.IsInRole(Roles.SuperAdmin))
            return true;
        return _tenantAccessor.TenantId == tenantId;
    }
}

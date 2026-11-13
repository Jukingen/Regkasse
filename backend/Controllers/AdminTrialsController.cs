using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Trial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin SaaS trial dashboard, analytics, and conversion APIs.</summary>
[ApiController]
[Route("api/admin/trials")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminTrialsController : ControllerBase
{
    private readonly ITrialService _trialService;
    private readonly ITrialConversionService _conversionService;

    public AdminTrialsController(
        ITrialService trialService,
        ITrialConversionService conversionService)
    {
        _trialService = trialService;
        _conversionService = conversionService;
    }

    private string? ActorUserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;

    private string? ActorRole =>
        User.IsInRole(Roles.SuperAdmin) ? Roles.SuperAdmin
        : User.IsInRole(Roles.Manager) ? Roles.Manager
        : null;

    [HttpGet]
    [ProducesResponseType(typeof(TrialDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrialDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _trialService.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        return Ok(dashboard);
    }

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(TrialAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrialAnalyticsDto>> GetAnalytics(CancellationToken cancellationToken)
    {
        var stats = await _trialService.GetAnalyticsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(stats);
    }

    [HttpGet("tenants/{tenantId:guid}")]
    [ProducesResponseType(typeof(TrialTenantSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrialTenantSummaryDto>> GetTenantTrial(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var summary = await _trialService.GetTenantTrialAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (summary == null)
            return NotFound(new { message = "Tenant not found." });
        return Ok(summary);
    }

    [HttpPost("tenants/{tenantId:guid}/extend")]
    [ProducesResponseType(typeof(TrialTenantSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrialTenantSummaryDto>> Extend(
        Guid tenantId,
        [FromBody] ExtendTrialRequest request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _trialService
            .ExtendTrialAsync(tenantId, request.AdditionalDays, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("tenants/{tenantId:guid}/convert-to-paid")]
    [ProducesResponseType(typeof(TrialConversionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrialConversionResult>> ConvertToPaid(
        Guid tenantId,
        [FromBody] ConvertToPaidRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LicenseSaleId == Guid.Empty)
            return BadRequest(new { message = "licenseSaleId is required." });

        var (result, error) = await _conversionService
            .ConvertToPaidAsync(
                tenantId,
                request.LicenseSaleId,
                request.AddRemainingTrialDays ?? true,
                request.Notes,
                ActorUserId,
                ActorRole,
                cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("tenants/{tenantId:guid}/delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDeleteTrial(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var (ok, error) = await _trialService
            .SoftDeleteTrialAsync(tenantId, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (!ok)
            return BadRequest(new { message = error });
        return NoContent();
    }

    [HttpPost("tenants/{tenantId:guid}/grant")]
    [ProducesResponseType(typeof(TrialTenantSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrialTenantSummaryDto>> Grant(
        Guid tenantId,
        [FromBody] GrantTrialRequest? request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _trialService
            .GrantOrRestartTrialAsync(tenantId, request?.DurationDays, ActorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (error == "Tenant not found.")
            return NotFound(new { message = error });
        if (error != null)
            return BadRequest(new { message = error });
        return Ok(result);
    }
}

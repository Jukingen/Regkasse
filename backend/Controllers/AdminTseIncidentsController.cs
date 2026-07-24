using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE operational incident management.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/incidents")]
[Produces("application/json")]
public sealed class AdminTseIncidentsController : ControllerBase
{
    private readonly ITseIncidentService _incidents;

    public AdminTseIncidentsController(ITseIncidentService incidents)
    {
        _incidents = incidents;
    }

    [HttpGet("dashboard")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseIncidentDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseIncidentDashboardDto>> GetDashboard(
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await _incidents.GetDashboardAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(dashboard);
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseIncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TseIncidentDto>>> List(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);
        try
        {
            var list = await _incidents.GetIncidentsAsync(tenantId, from, to, cancellationToken)
                .ConfigureAwait(false);
            return Ok(list);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseIncidentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseIncidentDto>> Create(
        [FromBody] CreateTseIncidentRequestDto body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var incident = await _incidents.CreateIncidentAsync(body, actor, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(GetById), new { incidentId = incident.Id }, incident);
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

    [HttpGet("{incidentId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseIncidentDto>> GetById(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var incident = await _incidents.GetIncidentAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(incident);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{incidentId:guid}/status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseIncidentDto>> UpdateStatus(
        Guid incidentId,
        [FromBody] UpdateTseIncidentStatusRequestDto body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var incident = await _incidents
                .UpdateIncidentStatusAsync(
                    incidentId,
                    body.Status,
                    body.Resolution,
                    body.Note,
                    actor,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(incident);
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

    [HttpPost("{incidentId:guid}/actions")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseIncidentDto>> AddAction(
        Guid incidentId,
        [FromBody] AddTseIncidentActionRequestDto body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.GetActorUserId() ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var incident = await _incidents
                .AddIncidentActionAsync(incidentId, body, actor, cancellationToken)
                .ConfigureAwait(false);
            return Ok(incident);
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

    [HttpGet("{incidentId:guid}/report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseIncidentReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseIncidentReportDto>> GenerateReport(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _incidents.GenerateIncidentReportAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Appointments;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/pos/appointments")]
[Produces("application/json")]
[HasPermission(AppPermissions.CartView)]
public sealed class PosAppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosAppointmentsController(
        IAppointmentService appointments,
        ICurrentTenantAccessor tenantAccessor)
    {
        _appointments = appointments;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? staffId,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var items = await _appointments.ListAsync(from, to, staffId, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AppointmentConflictDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> Create(
        [FromBody] CreatePosAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _appointments
            .CreateAsync(request, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentConflictDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> Update(
        Guid id,
        [FromBody] UpdatePosAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _appointments
            .UpdateAsync(id, request, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _appointments
            .CancelAsync(id, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    private ActionResult<AppointmentDto> ToActionResult(AppointmentWriteResult result)
    {
        if (result.StatusCode == StatusCodes.Status404NotFound)
            return NotFound();
        if (result.StatusCode == StatusCodes.Status400BadRequest)
            return BadRequest(new { code = result.ErrorCode, message = "Invalid appointment request." });
        if (result.StatusCode == StatusCodes.Status409Conflict && result.Conflict is not null)
        {
            return Conflict(new AppointmentConflictDto
            {
                Code = AppointmentConflictCodes.Conflict,
                Message = "Appointment slot conflict.",
                Appointment = result.Conflict,
            });
        }

        if (result.Appointment is null)
            return NotFound();

        if (result.StatusCode == StatusCodes.Status201Created)
            return Created($"/api/pos/appointments/{result.Appointment.Id:D}", result.Appointment);

        return Ok(result.Appointment);
    }
}

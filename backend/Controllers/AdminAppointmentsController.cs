using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Appointments;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Read-only appointment list for the admin panel. Writes stay on <c>/api/pos/appointments</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/appointments")]
[Produces("application/json")]
[HasPermission(AppPermissions.AppointmentView)]
public sealed class AdminAppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminAppointmentsController(
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
        [FromQuery] AppointmentStatus? status,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var items = await _appointments.ListAsync(from, to, staffId, cancellationToken).ConfigureAwait(false);
        if (status.HasValue)
            items = items.Where(row => row.Status == status.Value).ToList();
        return Ok(items);
    }
}

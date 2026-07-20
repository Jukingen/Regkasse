using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Anonymous customer portal: order history + loyalty snapshot (phone gate).</summary>
[AllowAnonymous]
[ApiController]
[Route("api/public/customer")]
[Produces("application/json")]
public sealed class PublicCustomerController : ControllerBase
{
    private readonly IPublicCustomerDashboardService _dashboard;

    public PublicCustomerController(IPublicCustomerDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>
    /// Customer dashboard by tenant slug + phone. Returns 404 when unknown (no enumeration).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PublicCustomerDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicCustomerDashboardDto>> GetDashboard(
        [FromQuery] string? tenant = null,
        [FromQuery] string? phone = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { message = "tenant and phone are required." });
        }

        var digits = phone.Count(char.IsDigit);
        if (digits < PublicCustomerDashboardService.MinPhoneDigits)
        {
            return BadRequest(new
            {
                message = $"phone must contain at least {PublicCustomerDashboardService.MinPhoneDigits} digits."
            });
        }

        var dto = await _dashboard.GetDashboardAsync(tenant, phone, ct);
        if (dto is null)
            return NotFound();

        return Ok(dto);
    }
}

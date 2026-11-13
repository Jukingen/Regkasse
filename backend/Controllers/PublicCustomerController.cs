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
    private readonly PublicCustomerLookupRateLimiter _rateLimiter;

    public PublicCustomerController(
        IPublicCustomerDashboardService dashboard,
        PublicCustomerLookupRateLimiter rateLimiter)
    {
        _dashboard = dashboard;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Customer dashboard by tenant slug, full phone number, and order number.
    /// Returns 404 when unknown (no enumeration).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PublicCustomerDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PublicCustomerDashboardDto>> GetDashboard(
        [FromQuery] string? tenant = null,
        [FromQuery] string? phone = null,
        [FromQuery] string? orderNumber = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenant)
            || string.IsNullOrWhiteSpace(phone)
            || string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest(new { message = "tenant, phone, and orderNumber are required." });
        }

        var digits = phone.Count(char.IsDigit);
        if (digits < PublicCustomerDashboardService.MinPhoneDigits)
        {
            return BadRequest(new
            {
                message = $"phone must contain at least {PublicCustomerDashboardService.MinPhoneDigits} digits."
            });
        }

        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!_rateLimiter.TryConsume(clientKey))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Too many requests. Please try again later."
            });
        }

        var dto = await _dashboard.GetDashboardAsync(tenant, phone, orderNumber, ct);
        if (dto is null)
            return NotFound();

        return Ok(dto);
    }
}

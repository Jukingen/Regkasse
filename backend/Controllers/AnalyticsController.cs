using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin analytics endpoints (non-fiscal SaaS metrics).</summary>
[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ICustomerAnalyticsService _analytics;

    public AnalyticsController(ICustomerAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    /// <summary>Customer / mandant KPI snapshot for the Super Admin dashboard.</summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(CustomerAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerAnalyticsDto>> GetCustomers(CancellationToken cancellationToken)
    {
        var dto = await _analytics.GetCustomerAnalyticsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(dto);
    }
}

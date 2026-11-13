using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin analytics endpoints (SaaS KPIs + diagnostic fleet usage).</summary>
[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ICustomerAnalyticsService _customers;
    private readonly ITseUsageAnalyticsService _tse;
    private readonly IPaymentVolumeAnalyticsService _payments;

    public AnalyticsController(
        ICustomerAnalyticsService customers,
        ITseUsageAnalyticsService tse,
        IPaymentVolumeAnalyticsService payments)
    {
        _customers = customers;
        _tse = tse;
        _payments = payments;
    }

    /// <summary>Customer / mandant KPI snapshot for the Super Admin dashboard.</summary>
    [HttpGet("customers")]
    [ProducesResponseType(typeof(CustomerAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerAnalyticsDto>> GetCustomers(CancellationToken cancellationToken)
    {
        var dto = await _customers.GetCustomerAnalyticsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(dto);
    }

    /// <summary>Fleet TSE usage (registers, signatures). Diagnostic only — not DEP evidence.</summary>
    [HttpGet("tse")]
    [ProducesResponseType(typeof(TseAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseAnalyticsDto>> GetTse(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { error = "fromDate must be <= toDate." });

        var dto = await _tse.GetTseAnalyticsAsync(fromDate, toDate, cancellationToken).ConfigureAwait(false);
        return Ok(dto);
    }

    /// <summary>Cross-tenant POS payment volume (fiscal GMV). Not license MRR.</summary>
    [HttpGet("payment-volume")]
    [ProducesResponseType(typeof(PaymentVolumeAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentVolumeAnalyticsDto>> GetPaymentVolume(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? groupBy = "month",
        CancellationToken cancellationToken = default)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(new { error = "fromDate must be <= toDate." });

        if (!IsAllowedGroupBy(groupBy))
            return BadRequest(new { error = "groupBy must be day, week, or month." });

        var dto = await _payments
            .GetPaymentVolumeAnalyticsAsync(fromDate, toDate, groupBy ?? "month", cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }

    private static bool IsAllowedGroupBy(string? groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
            return true;

        return groupBy.Equals("day", StringComparison.OrdinalIgnoreCase)
            || groupBy.Equals("week", StringComparison.OrdinalIgnoreCase)
            || groupBy.Equals("month", StringComparison.OrdinalIgnoreCase);
    }
}

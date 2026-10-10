using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Read-only taxi trips stored on payment rows (route fields). No separate trip table.</summary>
[Authorize]
[ApiController]
[Route("api/admin/taxi-trips")]
[Produces("application/json")]
[HasPermission(AppPermissions.TaxiTripView)]
public sealed class AdminTaxiTripsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTaxiTripsController(AppDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaxiTripListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaxiTripListItemDto>>> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var rangeEnd = to ?? DateTime.UtcNow;
        var rangeStart = from ?? rangeEnd.AddDays(-31);
        if (rangeEnd < rangeStart)
            return BadRequest(new { message = "to must be on or after from." });

        var registerIds = _db.CashRegisters.AsNoTracking().Select(register => register.Id);
        var rows = await _db.PaymentDetails
            .AsNoTracking()
            .Where(payment => registerIds.Contains(payment.CashRegisterId))
            .Where(payment =>
                payment.RouteFrom != null
                || payment.RouteTo != null
                || payment.RouteKm != null
                || payment.TripStartedAtUtc != null)
            .Where(payment => (payment.TripStartedAtUtc ?? payment.CreatedAt) >= rangeStart)
            .Where(payment => (payment.TripStartedAtUtc ?? payment.CreatedAt) <= rangeEnd)
            .OrderByDescending(payment => payment.TripStartedAtUtc ?? payment.CreatedAt)
            .Select(payment => new TaxiTripListItemDto
            {
                PaymentId = payment.Id,
                OccurredAtUtc = payment.TripStartedAtUtc ?? payment.CreatedAt,
                CustomerName = payment.CustomerName,
                RouteFrom = payment.RouteFrom,
                RouteTo = payment.RouteTo,
                RouteKm = payment.RouteKm,
                Amount = payment.TotalAmount,
                CashRegisterNumber = payment.CashRegister != null
                    ? payment.CashRegister.RegisterNumber
                    : null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }
}

public sealed class TaxiTripListItemDto
{
    public Guid PaymentId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? CustomerName { get; set; }
    public string? RouteFrom { get; set; }
    public string? RouteTo { get; set; }
    public decimal? RouteKm { get; set; }
    public decimal Amount { get; set; }
    public string? CashRegisterNumber { get; set; }
}

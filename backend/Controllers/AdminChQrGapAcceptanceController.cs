using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin acknowledgement of open CH QR print gaps for one mandant.
/// Does not change the PDF and does not enable bank submission.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/ch-qr-gap-acceptance")]
[Produces("application/json")]
public sealed class AdminChQrGapAcceptanceController : ControllerBase
{
    private readonly IChQrGapAcceptanceService _acceptance;
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenant;

    public AdminChQrGapAcceptanceController(
        IChQrGapAcceptanceService acceptance,
        AppDbContext db,
        ICurrentTenantAccessor tenant)
    {
        _acceptance = acceptance;
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Current acceptance plus every gap id from the known-gap catalog.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(ChQrGapAcceptanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChQrGapAcceptanceResponse>> Get(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!await CanSeeTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var current = await _acceptance.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(ToResponse(current));
    }

    /// <summary>Records which catalog gap ids the operator has acknowledged.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(ChQrGapAcceptanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChQrGapAcceptanceResponse>> Accept(
        Guid tenantId,
        [FromBody] AcceptChQrGapsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.AcceptedGaps is null)
            return BadRequest(new { message = "Accepted gaps are required.", invalidGapIds = Array.Empty<string>() });

        if (!await CanSeeTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        try
        {
            var saved = await _acceptance.AcceptAsync(
                tenantId,
                request.AcceptedGaps,
                ActorUserId(),
                cancellationToken).ConfigureAwait(false);
            return Ok(ToResponse(saved));
        }
        catch (ChQrUnknownGapException ex)
        {
            return BadRequest(new { message = ex.Message, invalidGapIds = ex.InvalidGapIds });
        }
    }

    private ChQrGapAcceptanceResponse ToResponse(ChQrGapAcceptance? acceptance) =>
        new(
            _acceptance.KnownGaps.Select(gap => new ChQrKnownGapDto(gap.Id, gap.Present)).ToArray(),
            acceptance is null
                ? null
                : new ChQrGapAcceptanceDto(acceptance.AcceptedGaps, acceptance.AcceptedBy, acceptance.AcceptedAtUtc));

    private string ActorUserId()
    {
        var name = User.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(id) ? "system" : id;
    }

    private async Task<bool> CanSeeTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is Guid ambient && ambient != Guid.Empty && ambient != tenantId)
            return false;

        return await _db.Tenants.AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record AcceptChQrGapsRequest(IReadOnlyList<string>? AcceptedGaps);

public sealed record ChQrKnownGapDto(string Id, bool Present);

public sealed record ChQrGapAcceptanceDto(
    IReadOnlyList<string> AcceptedGaps,
    string AcceptedBy,
    DateTime AcceptedAtUtc);

public sealed record ChQrGapAcceptanceResponse(
    IReadOnlyList<ChQrKnownGapDto> KnownGaps,
    ChQrGapAcceptanceDto? Acceptance);

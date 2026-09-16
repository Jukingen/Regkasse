using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mandanten-Admin list of TSE-signed Monatsbelege (and December Jahresbelege) plus failed auto-create rows.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/rksv/monatsbelege")]
[Produces("application/json")]
public sealed class AdminRksvMonatsbelegController : ControllerBase
{
    private readonly IRksvMonatsbelegService _list;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminRksvMonatsbelegController(
        IRksvMonatsbelegService list,
        ISettingsTenantResolver tenantResolver)
    {
        _list = list;
        _tenantResolver = tenantResolver;
    }

    [HttpGet]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(typeof(MonatsbelegListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsbelegListResponse>> Get(
        [FromQuery] int? year,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var result = await _list.ListAsync(
                new MonatsbelegListQuery
                {
                    Year = year,
                    CashRegisterId = cashRegisterId,
                    Status = status,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                },
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}

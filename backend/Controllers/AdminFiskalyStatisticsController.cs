using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Tenant-scoped Fiskaly usage statistics. Manager: ambient tenant only.
/// SuperAdmin: all tenants (optional <c>tenantId</c>). Hidden from OpenAPI.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly/statistics")]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AdminFiskalyStatisticsController : ControllerBase
{
    private readonly IFiskalyStatisticsService _statistics;

    public AdminFiskalyStatisticsController(IFiskalyStatisticsService statistics)
    {
        _statistics = statistics;
    }

    [HttpGet]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(FiskalyStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyStatisticsDto>> Get(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? operationType,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _statistics
                .GetAsync(ToQuery(fromUtc, toUtc, operationType, tenantId), User.IsInRole(Roles.SuperAdmin), cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, code = "STATISTICS_VALIDATION", message = ex.Message });
        }
    }

    [HttpGet("export")]
    [HasPermission(AppPermissions.FiskalyHistoryView)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? operationType,
        [FromQuery] Guid? tenantId,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _statistics
                .ExportAsync(
                    ToQuery(fromUtc, toUtc, operationType, tenantId),
                    format,
                    User.IsInRole(Roles.SuperAdmin),
                    cancellationToken)
                .ConfigureAwait(false);
            return File(result.Bytes, result.ContentType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, code = "STATISTICS_VALIDATION", message = ex.Message });
        }
    }

    private static FiskalyStatisticsQuery ToQuery(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? operationType,
        Guid? tenantId) =>
        new()
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            OperationType = operationType,
            TenantId = tenantId
        };
}

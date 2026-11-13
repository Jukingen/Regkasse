using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin centralized TSE operational log aggregation / search / analysis.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/logs")]
[Produces("application/json")]
public sealed class AdminTseLogsController : ControllerBase
{
    private readonly ITseLogAggregationService _logs;

    public AdminTseLogsController(ITseLogAggregationService logs)
    {
        _logs = logs;
    }

    [HttpGet("aggregate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseLogAggregationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseLogAggregationResultDto>> Aggregate(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-7);
        try
        {
            return Ok(await _logs.AggregateLogsAsync(tenantId, from, to, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("search")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseLogSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseLogSearchResultDto>> Search(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? query = null,
        [FromQuery] string? level = null,
        [FromQuery] string? provider = null,
        [FromQuery] string? source = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            var result = await _logs.SearchLogsAsync(
                    new TseLogSearchRequestDto
                    {
                        TenantId = tenantId,
                        FromUtc = fromUtc,
                        ToUtc = toUtc,
                        Query = query,
                        Level = level,
                        Provider = provider,
                        Source = source,
                        DeviceId = deviceId,
                        Skip = skip,
                        Take = take,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("analyze")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseLogAnalysisReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseLogAnalysisReportDto>> Analyze(
        [FromQuery] Guid tenantId,
        [FromBody] TseLogAnalysisRequestDto? body = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _logs
                .AnalyzeLogsAsync(tenantId, body ?? new TseLogAnalysisRequestDto(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

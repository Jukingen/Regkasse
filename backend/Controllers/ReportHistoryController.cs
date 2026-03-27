using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/reports/history")]
public class ReportHistoryController : ControllerBase
{
    private readonly IReportHistoryService _historyService;

    public ReportHistoryController(IReportHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet("{reportType}/{id:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(ReportHistoryTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportHistoryTimelineDto>> GetHistory(
        string reportType,
        Guid id,
        CancellationToken cancellationToken)
    {
        var data = await _historyService.GetHistoryAsync(reportType, id, cancellationToken);
        if (data == null) return NotFound();
        return Ok(data);
    }
}

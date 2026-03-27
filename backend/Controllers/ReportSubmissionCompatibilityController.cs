using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/reports/submissions")]
public class ReportSubmissionCompatibilityController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IReportSubmissionCompatibilityService _compat;

    public ReportSubmissionCompatibilityController(
        AppDbContext db,
        IReportSubmissionCompatibilityService compat)
    {
        _db = db;
        _compat = compat;
    }

    [HttpGet("{reportType}/{reportId:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(ReportSubmissionEnvelopeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportSubmissionEnvelopeDto>> GetEnvelope(
        string reportType,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        BuildReportSubmissionEnvelopeRequest? req = null;
        if (string.Equals(reportType, "tagesbericht", StringComparison.OrdinalIgnoreCase))
        {
            var row = await _db.Set<TagesberichtReport>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
            if (row == null) return NotFound();
            req = new BuildReportSubmissionEnvelopeRequest
            {
                ReportType = "Tagesbericht",
                ReportId = row.Id,
                ReportState = row.ReportStatus,
                OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
                SupersedesReportId = row.SupersedesReportId,
                SupersededByReportId = row.SupersededByReportId
            };
        }
        else if (string.Equals(reportType, "monatsbericht", StringComparison.OrdinalIgnoreCase))
        {
            var row = await _db.Set<MonatsberichtReport>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
            if (row == null) return NotFound();
            req = new BuildReportSubmissionEnvelopeRequest
            {
                ReportType = "Monatsbericht",
                ReportId = row.Id,
                ReportState = row.ReportStatus,
                OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
                SupersedesReportId = row.SupersedesReportId,
                SupersededByReportId = row.SupersededByReportId
            };
        }
        else if (string.Equals(reportType, "jahresbericht", StringComparison.OrdinalIgnoreCase))
        {
            var row = await _db.Set<JahresberichtReport>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);
            if (row == null) return NotFound();
            req = new BuildReportSubmissionEnvelopeRequest
            {
                ReportType = "Jahresbericht",
                ReportId = row.Id,
                ReportState = row.ReportStatus,
                OutboxMessageId = row.LastFinanzOnlineOutboxMessageId,
                SupersedesReportId = row.SupersedesReportId,
                SupersededByReportId = row.SupersededByReportId
            };
        }

        if (req == null)
            return NotFound();

        var envelope = await _compat.BuildEnvelopeAsync(req, cancellationToken);
        return Ok(envelope);
    }
}

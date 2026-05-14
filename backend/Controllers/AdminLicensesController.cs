using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Issued-license exports and aggregate reports (<c>/api/admin/licenses/*</c>).</summary>
[Authorize]
[ApiController]
[Route("api/admin/licenses")]
[Produces("application/json")]
public sealed class AdminLicensesController : ControllerBase
{
    private readonly ILicenseExportReportService _exportReportService;

    public AdminLicensesController(ILicenseExportReportService exportReportService)
    {
        _exportReportService = exportReportService;
    }

    private static LicenseExportFilters ParseFilters(
        DateTime? fromUtc,
        DateTime? toUtc,
        bool? includeActivationHistory,
        bool? maskLicenseKeys) =>
        new(
            fromUtc,
            toUtc,
            includeActivationHistory ?? false,
            maskLicenseKeys ?? true);

    /// <summary>CSV export: issued licenses, matching activations, optional activation attempts (JWT never included).</summary>
    [HttpGet("export/csv")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] bool? includeActivationHistory = null,
        [FromQuery] bool? maskLicenseKeys = null,
        CancellationToken cancellationToken = default)
    {
        var filters = ParseFilters(fromUtc, toUtc, includeActivationHistory, maskLicenseKeys);
        var bytes = await _exportReportService.BuildCsvAsync(filters, cancellationToken).ConfigureAwait(false);
        var fileName = $"licenses_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}_UTC.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>JSON backup export (same filter semantics as CSV).</summary>
    [HttpGet("export/json")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> ExportJson(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] bool? includeActivationHistory = null,
        [FromQuery] bool? maskLicenseKeys = null,
        CancellationToken cancellationToken = default)
    {
        var filters = ParseFilters(fromUtc, toUtc, includeActivationHistory, maskLicenseKeys);
        var (bytes, contentType) = await _exportReportService.BuildJsonAsync(filters, cancellationToken).ConfigureAwait(false);
        var fileName = $"licenses_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}_UTC.json";
        return File(bytes, contentType, fileName);
    }

    /// <summary>Summary counts for dashboard / scheduled mail (issued date filter optional).</summary>
    [HttpGet("report/summary")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseReportSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseReportSummaryDto>> ReportSummary(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] bool? includeActivationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var filters = ParseFilters(fromUtc, toUtc, includeActivationHistory, maskLicenseKeys: true);
        var dto = await _exportReportService.GetSummaryAsync(filters, cancellationToken).ConfigureAwait(false);
        return Ok(dto);
    }
}

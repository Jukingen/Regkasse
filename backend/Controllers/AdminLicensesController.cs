using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Issued-license exports and aggregate reports (<c>/api/admin/licenses/*</c>).</summary>
[Authorize]
[ApiController]
[Route("api/admin/licenses")]
[Produces("application/json")]
public sealed class AdminLicensesController : ControllerBase
{
    private readonly ILicenseExportService _licenseExport;
    private readonly ILicenseExportReportService _exportReportService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly AppDbContext _db;

    public AdminLicensesController(
        ILicenseExportService licenseExport,
        ILicenseExportReportService exportReportService,
        ISettingsTenantResolver settingsTenantResolver,
        AppDbContext db)
    {
        _licenseExport = licenseExport;
        _exportReportService = exportReportService;
        _settingsTenantResolver = settingsTenantResolver;
        _db = db;
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

    private async Task<string?> ResolveTenantSlugAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            if (tenantId == Guid.Empty)
                return null;
            return await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// CSV export: issued licenses, matching activations, optional activation attempts.
    /// Filename: <c>licenses_{tenantSlug}_{stamp}.csv</c>.
    /// </summary>
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
        var tenantSlug = await ResolveTenantSlugAsync(cancellationToken).ConfigureAwait(false);
        var result = await _licenseExport
            .ExportMultipleAsync(tenantSlug, "csv", filters, cancellationToken)
            .ConfigureAwait(false);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// JSON backup export (same filter semantics as CSV).
    /// Filename: <c>licenses_{tenantSlug}_{stamp}.json</c>.
    /// </summary>
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
        var tenantSlug = await ResolveTenantSlugAsync(cancellationToken).ConfigureAwait(false);
        var result = await _licenseExport
            .ExportMultipleAsync(tenantSlug, "json", filters, cancellationToken)
            .ConfigureAwait(false);
        return File(result.Content, result.ContentType, result.FileName);
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

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// DEP-like fiscal export: receipts, RKSV/TSE signatures, chain state, daily/monthly/yearly closings.
/// JSON by default; optional CSV fragments when includeCsv=true.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiscal-export")]
[Produces("application/json")]
public class FiscalExportController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IFiscalExportService _exportService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<FiscalExportController> _logger;

    public FiscalExportController(
        IFiscalExportService exportService,
        IAuditLogService auditLogService,
        ILogger<FiscalExportController> logger)
    {
        _exportService = exportService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Export fiscal package for one cash register and UTC time range.
    /// </summary>
    /// <param name="cashRegisterId">Target register.</param>
    /// <param name="fromUtc">Inclusive start (UTC).</param>
    /// <param name="toUtc">Inclusive end (UTC).</param>
    /// <param name="includeCsv">When true, adds receiptsCsv and closingsCsv (UTF-8 text, comma-separated).</param>
    /// <param name="format">json (default) or jsonDownload — download sets Content-Disposition attachment.</param>
    [HttpGet]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<IActionResult> GetExport(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] bool includeCsv = false,
        [FromQuery] string format = "json",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var package = await _exportService.BuildExportAsync(
                cashRegisterId, fromUtc, toUtc, includeCsv, cancellationToken);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
                await _auditLogService.LogSystemOperationAsync(
                    "FiscalExportRequested",
                    "FiscalExport",
                    userId,
                    userRole,
                    description: $"Fiscal export register {cashRegisterId}",
                    requestData: new { cashRegisterId, fromUtc, toUtc, includeCsv },
                    responseData: new
                    {
                        package.ReceiptCount,
                        package.ClosingCount,
                        chainWarningCount = package.ChainContinuityWarnings?.Count ?? 0
                    });
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "Fiscal export audit log failed (export succeeded)");
            }

            if (string.Equals(format, "jsonDownload", StringComparison.OrdinalIgnoreCase))
            {
                var fileName =
                    $"fiscal-export-{cashRegisterId:D}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.json";
                var bytes = JsonSerializer.SerializeToUtf8Bytes(package, JsonOptions);
                return File(bytes, "application/json", fileName);
            }

            return Ok(package);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "FISCAL_EXPORT_INVALID_RANGE" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "FISCAL_EXPORT_REGISTER_NOT_FOUND" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fiscal export failed for register {RegisterId}", cashRegisterId);
            return StatusCode(500, new { message = "Fiscal export failed.", code = "FISCAL_EXPORT_ERROR" });
        }
    }
}

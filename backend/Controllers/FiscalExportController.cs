using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// DEP-benzeri fiscal export: fişler, RKSV/TSE imzaları, zincir durumu, kapanışlar.
/// exportProfile: operational_preview (default), accounting_report, legal_compliance_export, diagnostic_package.
/// Bütünlük bayrakları tanılama amaçlıdır; yasal RKSV garantisi değildir (payload notLegalProofNotice).
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
    /// <param name="exportProfile">operational_preview (default) | accounting_report | legal_compliance_export | diagnostic_package</param>
    [HttpGet]
    public async Task<IActionResult> GetExport(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] bool includeCsv = false,
        [FromQuery] string format = "json",
        [FromQuery] string? exportProfile = null,
        CancellationToken cancellationToken = default)
    {
        if (!FiscalExportProfileRules.TryParseProfile(exportProfile, out var profile))
        {
            return BadRequest(new
            {
                message = "Invalid exportProfile. Use: operational_preview, accounting_report, legal_compliance_export, or diagnostic_package.",
                code = "FISCAL_EXPORT_INVALID_PROFILE"
            });
        }

        if (!FiscalExportProfileRules.CanExport(User, profile))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = FiscalExportProfileRules.ForbiddenDetail(profile),
                code = "FISCAL_EXPORT_PROFILE_FORBIDDEN",
                exportProfile = profile.ToString()
            });
        }

        try
        {
            var package = await _exportService.BuildExportAsync(
                cashRegisterId, fromUtc, toUtc, includeCsv, profile, cancellationToken);

            try
            {
                var userId = User.GetActorUserId() ?? "unknown";
                var userRole = User.GetActorRole() ?? "Unknown";
                var auditAction = profile switch
                {
                    FiscalExportProfile.OperationalPreview => "FiscalExportOperationalPreview",
                    FiscalExportProfile.AccountingReport => "FiscalExportAccountingReport",
                    FiscalExportProfile.LegalComplianceExport => "FiscalExportLegalComplianceExport",
                    FiscalExportProfile.DiagnosticPackage => "FiscalExportDiagnosticPackage",
                    _ => "FiscalExportOperationalPreview"
                };

                await _auditLogService.LogSystemOperationAsync(
                    auditAction,
                    "FiscalExport",
                    userId,
                    userRole,
                    description: $"Fiscal export ({package.ExportProfile}) register {cashRegisterId}",
                    requestData: new { cashRegisterId, fromUtc, toUtc, includeCsv, exportProfile = package.ExportProfile },
                    responseData: new
                    {
                        package.ReceiptCount,
                        package.ClosingCount,
                        chainWarningCount = package.ChainContinuityWarnings?.Count ?? 0,
                        package.ExportProfile
                    });
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "Fiscal export audit log failed (export succeeded)");
            }

            if (string.Equals(format, "jsonDownload", StringComparison.OrdinalIgnoreCase))
            {
                var fileName =
                    $"fiscal-export-{package.ExportProfile}-{cashRegisterId:D}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.json";
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

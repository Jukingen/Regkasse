using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Filters;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// DEP-benzeri fiscal export: fişler, RKSV/TSE imzaları, zincir durumu, kapanışlar.
/// exportProfile: operational_preview (default), accounting_report, legal_compliance_export, diagnostic_package.
/// JSON responses wrap payload in <see cref="FiscalExportJsonEnvelopeDto"/> (legalNotice + exports).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/fiscal-export")]
[Produces("application/json")]
public class FiscalExportController : ControllerBase
{
    /// <summary>Legacy name; prefer <see cref="FiscalExportDisclaimerHeaders.AcknowledgedHeaderName"/>.</summary>
    public const string DisclaimerAcknowledgedHeaderName = FiscalExportDisclaimerHeaders.AcknowledgedHeaderName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IFiscalExportService _exportService;
    private readonly IDisclaimerService _disclaimer;
    private readonly IAuditLogService _auditLogService;
    private readonly IFiscalExportDownloadTicketStore _downloadTickets;
    private readonly ILogger<FiscalExportController> _logger;

    public FiscalExportController(
        IFiscalExportService exportService,
        IDisclaimerService disclaimer,
        IAuditLogService auditLogService,
        IFiscalExportDownloadTicketStore downloadTickets,
        ILogger<FiscalExportController> logger)
    {
        _exportService = exportService;
        _disclaimer = disclaimer;
        _auditLogService = auditLogService;
        _downloadTickets = downloadTickets;
        _logger = logger;
    }

    /// <summary>
    /// RKSV § 8 disclaimer texts for fiscal export (German + English). No acknowledgment header required.
    /// </summary>
    [HttpGet("disclaimer")]
    [ProducesResponseType(typeof(RksvExportDisclaimerResponseDto), StatusCodes.Status200OK)]
    public ActionResult<RksvExportDisclaimerResponseDto> GetDisclaimer()
    {
        return Ok(new RksvExportDisclaimerResponseDto
        {
            De = _disclaimer.GetRksvDisclaimer("de"),
            En = _disclaimer.GetRksvDisclaimer("en")
        });
    }

    /// <summary>
    /// Starts a fiscal export job. JSON format returns envelope inline (same as GET). jsonDownload/pdf create a ticket; use GET download/{exportId}.
    /// </summary>
    [HttpPost("generate")]
    [RequireDisclaimerAcknowledgment]
    [ProducesResponseType(typeof(FiscalExportDisclaimerRequiredResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateExport(
        [FromBody] FiscalExportGenerateRequestDto body,
        CancellationToken cancellationToken = default)
        => await RunExportAsync(body.CashRegisterId, body.FromUtc, body.ToUtc, body.IncludeCsv, body.Format, body.ExportProfile, body.Lang,
            deferLargePayloadToTicket: true, cancellationToken);

    /// <summary>
    /// Export fiscal package for one cash register and UTC time range (synchronous preview or direct file download).
    /// </summary>
    /// <param name="exportProfile">operational_preview (default) | accounting_report | legal_compliance_export | diagnostic_package</param>
    /// <param name="format">json (default) | jsonDownload | pdf</param>
    /// <param name="lang">Disclaimer language for CSV, PDF, and embedded notices: de (default) | en</param>
    [HttpGet]
    [RequireDisclaimerAcknowledgment]
    [ProducesResponseType(typeof(FiscalExportDisclaimerRequiredResponseDto), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetExport(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] bool includeCsv = false,
        [FromQuery] string format = "json",
        [FromQuery] string? exportProfile = null,
        [FromQuery] string lang = "de",
        CancellationToken cancellationToken = default)
        => RunExportAsync(cashRegisterId, fromUtc, toUtc, includeCsv, format, exportProfile, lang, deferLargePayloadToTicket: false, cancellationToken);

    /// <summary>Downloads a deferred export created via POST generate (single use).</summary>
    [HttpGet("download/{exportId:guid}")]
    [RequireDisclaimerAcknowledgment]
    [ProducesResponseType(typeof(FiscalExportDisclaimerRequiredResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public IActionResult DownloadExport(Guid exportId)
    {
        if (!_downloadTickets.TryConsume(exportId, out var ticket) || ticket == null)
        {
            return NotFound(new
            {
                code = "FISCAL_EXPORT_DOWNLOAD_NOT_FOUND",
                message = "Export ticket expired or unknown."
            });
        }

        var userId = User.GetActorUserId() ?? "unknown";
        if (!string.Equals(ticket.PreparedForUserId, userId, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "FISCAL_EXPORT_DOWNLOAD_FORBIDDEN",
                message = "Export ticket was issued for another user."
            });
        }

        var package = ticket.Envelope.Exports.FirstOrDefault();
        if (package == null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Export ticket malformed." });

        if (ticket.NormalizedExportFormat == "pdf")
        {
            var pdfBytes = FiscalExportPdfGenerator.Generate(package, package.NotLegalProofNotice);
            var pdfName =
                $"fiscal-export-{package.ExportProfile}-{package.CashRegisterId:D}-{package.Period.FromUtc:yyyyMMdd}-{package.Period.ToUtc:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", pdfName);
        }

        if (ticket.NormalizedExportFormat == "jsondownload")
        {
            var fileName =
                $"fiscal-export-{package.ExportProfile}-{package.CashRegisterId:D}-{package.Period.FromUtc:yyyyMMdd}-{package.Period.ToUtc:yyyyMMdd}.json";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(ticket.Envelope, JsonOptions);
            return File(bytes, "application/json", fileName);
        }

        return BadRequest(new
        {
            code = "FISCAL_EXPORT_DOWNLOAD_INVALID_TICKET_FORMAT",
            message = "Deferred download supports jsonDownload and pdf only."
        });
    }

    private async Task<IActionResult> RunExportAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeCsv,
        string format,
        string? exportProfile,
        string lang,
        bool deferLargePayloadToTicket,
        CancellationToken cancellationToken)
    {
        if (!FiscalExportProfileRules.TryParseProfile(exportProfile, out var profile))
        {
            return BadRequest(new
            {
                message =
                    "Invalid exportProfile. Use: operational_preview, accounting_report, legal_compliance_export, or diagnostic_package.",
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

        var disclaimerLang = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "de";
        var exportFormatRaw = string.IsNullOrWhiteSpace(format) ? "json" : format.Trim();
        var exportFormat = exportFormatRaw.ToLowerInvariant();

        if (exportFormat != "json" && exportFormat != "jsondownload" && exportFormat != "pdf")
        {
            return BadRequest(new
            {
                message = "Invalid format. Use: json, jsonDownload, or pdf.",
                code = "FISCAL_EXPORT_INVALID_FORMAT"
            });
        }

        try
        {
            var package = await _exportService.BuildExportAsync(
                    cashRegisterId,
                    fromUtc,
                    toUtc,
                    includeCsv,
                    profile,
                    disclaimerLang,
                    cancellationToken)
                .ConfigureAwait(false);

            var envelope = new FiscalExportJsonEnvelopeDto
            {
                LegalNotice = package.NotLegalProofNotice,
                Exports = new[] { package }
            };

            await LogFiscalExportAuditAsync(package, profile, exportFormat, disclaimerLang, cashRegisterId, fromUtc, toUtc,
                includeCsv).ConfigureAwait(false);

            var deferredEligible = deferLargePayloadToTicket && exportFormat is "jsondownload" or "pdf";

            if (deferredEligible)
            {
                var userIdForTicket = User.GetActorUserId() ?? "unknown";
                var exportId = _downloadTickets.CreateTicket(new FiscalExportDownloadTicket
                {
                    Envelope = envelope,
                    NormalizedExportFormat = exportFormat,
                    PreparedForUserId = userIdForTicket,
                });

                return Ok(new FiscalExportGenerateDeferredResponseDto
                {
                    ExportId = exportId,
                    Format = exportFormat,
                });
            }

            if (exportFormat == "pdf")
            {
                var pdfBytes = FiscalExportPdfGenerator.Generate(package, package.NotLegalProofNotice);
                var pdfName =
                    $"fiscal-export-{package.ExportProfile}-{cashRegisterId:D}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", pdfName);
            }

            if (exportFormat == "jsondownload")
            {
                var fileName =
                    $"fiscal-export-{package.ExportProfile}-{cashRegisterId:D}-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.json";
                var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
                return File(bytes, "application/json", fileName);
            }

            return Ok(envelope);
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

    private async Task LogFiscalExportAuditAsync(
        FiscalExportPackageDto package,
        FiscalExportProfile profile,
        string exportFormat,
        string disclaimerLanguage,
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeCsv)
    {
        try
        {
            var userId = User.GetActorUserId() ?? "unknown";
            var userRole = User.GetActorRole() ?? "Unknown";
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            var auditAction = (profile, exportFormat) switch
            {
                (FiscalExportProfile.OperationalPreview, "json") => "FiscalExportOperationalPreviewJson",
                (FiscalExportProfile.OperationalPreview, "jsondownload") => "FiscalExportOperationalPreviewJsonDownload",
                (FiscalExportProfile.OperationalPreview, "pdf") => "FiscalExportOperationalPreviewPdf",
                (FiscalExportProfile.AccountingReport, "json") => "FiscalExportAccountingReportJson",
                (FiscalExportProfile.AccountingReport, "jsondownload") => "FiscalExportAccountingReportJsonDownload",
                (FiscalExportProfile.AccountingReport, "pdf") => "FiscalExportAccountingReportPdf",
                (FiscalExportProfile.LegalComplianceExport, "json") => "FiscalExportLegalComplianceExportJson",
                (FiscalExportProfile.LegalComplianceExport, "jsondownload") => "FiscalExportLegalComplianceExportJsonDownload",
                (FiscalExportProfile.LegalComplianceExport, "pdf") => "FiscalExportLegalComplianceExportPdf",
                (FiscalExportProfile.DiagnosticPackage, "json") => "FiscalExportDiagnosticPackageJson",
                (FiscalExportProfile.DiagnosticPackage, "jsondownload") => "FiscalExportDiagnosticPackageJsonDownload",
                (FiscalExportProfile.DiagnosticPackage, "pdf") => "FiscalExportDiagnosticPackagePdf",
                _ => $"FiscalExport{profile}Generic",
            };

            await _auditLogService.LogSystemOperationAsync(
                    auditAction,
                    AuditLogEntityTypes.FISCAL_EXPORT,
                    userId,
                    userRole,
                    description:
                    $"Fiscal export access ({package.ExportProfile}, format={exportFormat}, lang={disclaimerLanguage}) register {cashRegisterId}",
                    requestData: new
                    {
                        cashRegisterId,
                        fromUtc,
                        toUtc,
                        includeCsv,
                        exportProfile = package.ExportProfile,
                        exportFormat,
                        disclaimerLanguage,
                        disclaimerAcknowledged = true,
                        clientIp,
                        accessTimestampUtc = DateTime.UtcNow,
                    },
                    responseData: new
                    {
                        package.ReceiptCount,
                        package.ClosingCount,
                        chainWarningCount = package.ChainContinuityWarnings?.Count ?? 0,
                        package.ExportProfile,
                        exportFormat,
                    })
                .ConfigureAwait(false);
        }
        catch (Exception auditEx)
        {
            _logger.LogWarning(auditEx, "Fiscal export audit log failed (export succeeded)");
        }
    }
}

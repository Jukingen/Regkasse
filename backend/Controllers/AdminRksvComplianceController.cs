using System.Globalization;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Filters;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin diagnostic RKSV compliance test report (special receipts, chain continuity, sequence gaps,
/// TSE signature presence, QR payload format). Read-only; no DB mutations.
/// Not a legally binding RKSV / Finanzamt proof — see <see cref="RksvComplianceReportDto.LegalNoticeDe"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/rksv")]
[Produces("application/json")]
public sealed class AdminRksvComplianceController : ControllerBase
{
    private readonly IRksvComplianceReportService _reportService;
    private readonly IRksvEvidenceBundleService _evidenceBundleService;
    private readonly ILogger<AdminRksvComplianceController> _logger;

    public AdminRksvComplianceController(
        IRksvComplianceReportService reportService,
        IRksvEvidenceBundleService evidenceBundleService,
        ILogger<AdminRksvComplianceController> logger)
    {
        _reportService = reportService;
        _evidenceBundleService = evidenceBundleService;
        _logger = logger;
    }

    /// <summary>
    /// Diagnostic RKSV compliance test report. Defaults to JSON; pass <c>format=pdf</c> for the PDF rendering.
    /// </summary>
    /// <param name="cashRegisterId">Optional: scope to a single cash register.</param>
    /// <param name="fromUtc">Optional: inclusive lower bound on <c>receipts.issued_at</c> (UTC).</param>
    /// <param name="toUtc">Optional: exclusive upper bound on <c>receipts.issued_at</c> (UTC).</param>
    /// <param name="format"><c>json</c> (default) or <c>pdf</c>.</param>
    [HttpGet("compliance-report")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(RksvComplianceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetComplianceReport(
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string format = "json",
        CancellationToken cancellationToken = default)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "json" : format.Trim().ToLowerInvariant();
        if (normalizedFormat != "json" && normalizedFormat != "pdf")
        {
            return BadRequest(new
            {
                code = "RKSV_COMPLIANCE_INVALID_FORMAT",
                message = "Invalid format. Use: json or pdf.",
            });
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value >= toUtc.Value)
        {
            return BadRequest(new
            {
                code = "RKSV_COMPLIANCE_INVALID_RANGE",
                message = "fromUtc must be strictly less than toUtc.",
            });
        }

        var normalizedCashRegisterId = cashRegisterId == Guid.Empty ? null : cashRegisterId;

        var report = await _reportService
            .BuildReportAsync(normalizedCashRegisterId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RKSV compliance report served: format={Format} register={Register} from={FromUtc} to={ToUtc} pass={OverallPass}",
            normalizedFormat,
            normalizedCashRegisterId,
            fromUtc,
            toUtc,
            report.Summary.OverallPass);

        if (normalizedFormat == "pdf")
        {
            var pdfBytes = RksvComplianceReportPdfGenerator.Generate(report);
            var fileName = BuildPdfFileName(normalizedCashRegisterId, fromUtc, toUtc, report.GeneratedAtUtc);
            return File(pdfBytes, "application/pdf", fileName);
        }

        return Ok(report);
    }

    /// <summary>
    /// Builds an RKSV evidence ZIP bundle for auditor / BMF review (compliance report + payment_details CSV +
    /// receipts JSON + signature chain state + TSE signature log + manifest + NOTICE).
    /// Internal compliance evidence only — official DEP export is a separate workflow.
    /// </summary>
    [HttpPost("evidence-bundle")]
    [HasPermission(AppPermissions.FiscalExportCompliance)]
    [RequireDisclaimerAcknowledgment]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEvidenceBundle(
        [FromBody] RksvEvidenceBundleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new
            {
                code = "RKSV_EVIDENCE_BUNDLE_BODY_REQUIRED",
                message = "Request body is required.",
            });
        }

        if (request.FromUtc == default || request.ToUtc == default)
        {
            return BadRequest(new
            {
                code = "RKSV_EVIDENCE_BUNDLE_RANGE_REQUIRED",
                message = "fromUtc and toUtc are required.",
            });
        }

        if (request.FromUtc >= request.ToUtc)
        {
            return BadRequest(new
            {
                code = "RKSV_EVIDENCE_BUNDLE_INVALID_RANGE",
                message = "fromUtc must be strictly less than toUtc.",
            });
        }

        var actorUserId = User.GetActorUserId() ?? "unknown";
        var bundle = await _evidenceBundleService
            .BuildBundleAsync(request, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "RKSV evidence bundle served: register={Register} from={FromUtc} to={ToUtc} bundleSizeBytes={Size} actor={Actor}",
            request.CashRegisterId,
            request.FromUtc,
            request.ToUtc,
            bundle.ZipBytes.LongLength,
            actorUserId);

        // Manifest counts surfaced via headers so the admin UI can show progress without parsing the zip.
        Response.Headers.Append("X-Rksv-Bundle-Payment-Rows", bundle.Manifest.Counts.PaymentDetailRows.ToString(CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Rksv-Bundle-Receipt-Rows", bundle.Manifest.Counts.ReceiptRows.ToString(CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Rksv-Bundle-Tse-Rows", bundle.Manifest.Counts.TseSignatureRows.ToString(CultureInfo.InvariantCulture));
        Response.Headers.Append("X-Rksv-Bundle-Chain-State-Rows", bundle.Manifest.Counts.SignatureChainStateRows.ToString(CultureInfo.InvariantCulture));

        return File(bundle.ZipBytes, "application/zip", bundle.FileName);
    }

    private static string BuildPdfFileName(Guid? cashRegisterId, DateTime? fromUtc, DateTime? toUtc, DateTime generatedAtUtc)
    {
        var inv = CultureInfo.InvariantCulture;
        var registerSegment = cashRegisterId.HasValue
            ? cashRegisterId.Value.ToString("D", inv)
            : "all-registers";
        var fromSegment = fromUtc?.ToString("yyyyMMdd", inv) ?? "any";
        var toSegment = toUtc?.ToString("yyyyMMdd", inv) ?? "any";
        var stamp = generatedAtUtc.ToString("yyyyMMdd_HHmmss", inv);
        return $"rksv-compliance-report_{registerSegment}_{fromSegment}-{toSegment}_{stamp}_UTC.pdf";
    }
}

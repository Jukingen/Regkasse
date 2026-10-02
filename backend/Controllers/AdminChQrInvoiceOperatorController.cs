using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin QR-Rechnung PDF download and a manual bank-portal upload note.
/// Does not call a bank. Does not change the PDF.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/ch-qr-invoices/{invoiceId:guid}")]
[Produces("application/json")]
public sealed class AdminChQrInvoiceOperatorController : ControllerBase
{
    public const string GapsNotAcceptedCode = "CH_QR_GAPS_NOT_ACCEPTED";
    public const string BankSubmitEnabledCode = "CH_QR_BANK_SUBMIT_ENABLED";

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IChQrGapAcceptanceService _gaps;
    private readonly IQrRechnungBuilder _qr;
    private readonly QrRechnungOptions _qrOptions;
    private readonly IAuditLogService _audit;

    public AdminChQrInvoiceOperatorController(
        AppDbContext db,
        ICurrentTenantAccessor tenant,
        IChQrGapAcceptanceService gaps,
        IQrRechnungBuilder qr,
        IOptions<QrRechnungOptions> qrOptions,
        IAuditLogService audit)
    {
        _db = db;
        _tenant = tenant;
        _gaps = gaps;
        _qr = qr;
        _qrOptions = qrOptions.Value;
        _audit = audit;
    }

    [HttpGet("pdf")]
    [HasPermission(AppPermissions.SystemCritical)]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadPdf(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        if (!await CanSeeTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var company = await CompanyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (company is null || !string.Equals(company.Country, "CH", StringComparison.Ordinal))
            return NotFound();

        var invoice = await InvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
            return NotFound();

        if (_qrOptions.BankSubmit.Enabled)
        {
            return Conflict(new { code = BankSubmitEnabledCode });
        }

        var outstanding = await OutstandingGapIdsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (outstanding.Count > 0)
        {
            return Conflict(new ChQrGapsNotAcceptedResponse(GapsNotAcceptedCode, outstanding));
        }

        byte[] pdf;
        try
        {
            pdf = await _qr.BuildPdfAsync(
                new QrRechnungRequest(
                    Iban: company.BankAccountNumber ?? string.Empty,
                    Creditor: new QrRechnungParty(
                        company.CompanyName,
                        company.CompanyAddress,
                        null,
                        string.Empty,
                        string.Empty,
                        "CH"),
                    Debtor: null,
                    Amount: invoice.TotalAmount,
                    Currency: string.IsNullOrWhiteSpace(company.Currency) ? "CHF" : company.Currency.Trim(),
                    Reference: null,
                    AdditionalInfo: invoice.InvoiceNumber,
                    ReferenceType: QrRechnungReferenceType.Non,
                    InvoiceId: invoice.Id,
                    TenantId: tenantId,
                    ActorUserId: ActorUserId()),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return BadRequest(new { message = "QR-Rechnung PDF could not be built." });
        }

        await _audit.LogSystemOperationAsync(
            action: "QR_RECHNUNG_PDF_DOWNLOADED",
            entityType: "Invoice",
            userId: ActorUserId(),
            userRole: "SuperAdmin",
            description: "QR-Rechnung PDF downloaded",
            actionType: AuditEventType.QrRechnungPdfDownloaded,
            entityId: invoice.Id,
            tenantId: tenantId,
            newValues: new { invoiceId = invoice.Id, tenantId }).ConfigureAwait(false);

        var fileName = "qr-rechnung-" + invoice.InvoiceNumber + ".pdf";
        return File(pdf, "application/pdf", fileName);
    }

    [HttpPost("upload-confirmation")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmUpload(
        Guid tenantId,
        Guid invoiceId,
        [FromBody] ChQrBankUploadConfirmationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.UploadedBy)
            || string.IsNullOrWhiteSpace(request.UploadedAtUtc)
            || string.IsNullOrWhiteSpace(request.BankReference))
        {
            return BadRequest(new { message = "uploadedBy, uploadedAtUtc, and bankReference are required." });
        }

        if (!await CanSeeTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var company = await CompanyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (company is null || !string.Equals(company.Country, "CH", StringComparison.Ordinal))
            return NotFound();

        var invoice = await InvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
            return NotFound();

        var generated = await _db.AuditLogs.AsNoTracking()
            .AnyAsync(
                row => row.TenantId == tenantId
                    && row.EntityId == invoiceId
                    && row.ActionType == AuditEventType.QrRechnungPdfGenerated,
                cancellationToken)
            .ConfigureAwait(false);
        if (!generated)
        {
            return BadRequest(new { message = "QR-Rechnung PDF has not been generated for this invoice." });
        }

        await _audit.LogSystemOperationAsync(
            action: "QR_RECHNUNG_BANK_UPLOAD_CONFIRMED",
            entityType: "Invoice",
            userId: ActorUserId(),
            userRole: "SuperAdmin",
            description: "Operator recorded a bank-portal PDF upload",
            actionType: AuditEventType.QrRechnungBankUploadConfirmed,
            entityId: invoice.Id,
            tenantId: tenantId,
            newValues: new
            {
                invoiceId = invoice.Id,
                uploadedBy = request.UploadedBy.Trim(),
                uploadedAtUtc = request.UploadedAtUtc.Trim(),
                bankReference = request.BankReference.Trim(),
            }).ConfigureAwait(false);

        return Ok(new { recorded = true });
    }

    private async Task<IReadOnlyList<string>> OutstandingGapIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var acceptance = await _gaps.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var accepted = new HashSet<string>(acceptance?.AcceptedGaps ?? [], StringComparer.Ordinal);
        return _gaps.KnownGaps
            .Where(gap => !gap.Present && !accepted.Contains(gap.Id))
            .Select(gap => gap.Id)
            .ToArray();
    }

    private Task<CompanySettings?> CompanyAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);

    private Task<Invoice?> InvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        _db.Invoices.AsNoTracking().FirstOrDefaultAsync(row => row.Id == invoiceId, cancellationToken);

    private async Task<bool> CanSeeTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not Guid ambient || ambient == Guid.Empty || ambient != tenantId)
            return false;

        return await _db.Tenants.AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    private string ActorUserId()
    {
        var name = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(name) ? "system" : name;
    }
}

public sealed record ChQrGapsNotAcceptedResponse(string Code, IReadOnlyList<string> OutstandingGapIds);

public sealed record ChQrBankUploadConfirmationRequest(
    string? UploadedBy,
    string? UploadedAtUtc,
    string? BankReference);

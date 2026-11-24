using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Streaming CSV/XLSX audit exports for storno, refund, and voucher redemption.
/// Each export needs <c>audit.export</c> plus the view permission of the data it reads.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/audit-logs/export")]
public sealed class AdminAuditTypedExportsController : ControllerBase
{
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IAuditTypedExportService _exportService;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IFileNamingService _fileNaming;
    private readonly IDownloadSecurityService _downloadSecurity;
    private readonly AppDbContext _db;
    private readonly ILogger<AdminAuditTypedExportsController> _logger;

    public AdminAuditTypedExportsController(
        IAuditTypedExportService exportService,
        ISettingsTenantResolver tenantResolver,
        IFileNamingService fileNaming,
        IDownloadSecurityService downloadSecurity,
        AppDbContext db,
        ILogger<AdminAuditTypedExportsController> logger)
    {
        _exportService = exportService;
        _tenantResolver = tenantResolver;
        _fileNaming = fileNaming;
        _downloadSecurity = downloadSecurity;
        _db = db;
        _logger = logger;
    }

    /// <summary>Storno receipts (cancellation reversals) with a link to the original receipt.</summary>
    /// <param name="format"><c>csv</c> (default) or <c>xlsx</c>.</param>
    /// <param name="startDate">Inclusive calendar start date.</param>
    /// <param name="endDate">Inclusive calendar end date.</param>
    /// <param name="userId">Cashier id.</param>
    /// <param name="cashRegisterId">Cash register id.</param>
    /// <param name="stornoReason"><c>FalscherBetrag</c>, <c>KundeStorniert</c>, <c>TechnischerFehler</c>, or <c>Anderes</c>.</param>
    /// <param name="minAmount">Lower bound on the absolute total.</param>
    /// <param name="maxAmount">Upper bound on the absolute total.</param>
    /// <param name="page">1-based page. Omit with <paramref name="pageSize"/> to export the filtered set.</param>
    /// <param name="pageSize">Page size. Omit with <paramref name="page"/> to export the filtered set.</param>
    /// <response code="200">CSV or XLSX attachment.</response>
    /// <response code="400">Invalid format or filter, or the export exceeds the row cap.</response>
    [HttpGet(AuditExportTypes.Storno)]
    [HasPermission(AppPermissions.AuditExport)]
    [HasPermission(AppPermissions.PaymentView)]
    [Produces("text/csv", XlsxContentType)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task ExportStorno(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? userId = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] string? stornoReason = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        StreamExportAsync(
            AuditExportType.Storno,
            format,
            new AuditTypedExportFilters
            {
                StartDate = startDate,
                EndDate = endDate,
                UserId = userId,
                CashRegisterId = cashRegisterId,
                StornoReason = stornoReason,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                Page = page,
                PageSize = pageSize,
            },
            cancellationToken);

    /// <summary>Refund receipts with refund amount and reason.</summary>
    /// <param name="format"><c>csv</c> (default) or <c>xlsx</c>.</param>
    /// <param name="startDate">Inclusive calendar start date.</param>
    /// <param name="endDate">Inclusive calendar end date.</param>
    /// <param name="userId">Cashier id.</param>
    /// <param name="cashRegisterId">Cash register id.</param>
    /// <param name="minAmount">Lower bound on the absolute total.</param>
    /// <param name="maxAmount">Upper bound on the absolute total.</param>
    /// <param name="page">1-based page. Omit with <paramref name="pageSize"/> to export the filtered set.</param>
    /// <param name="pageSize">Page size. Omit with <paramref name="page"/> to export the filtered set.</param>
    /// <response code="200">CSV or XLSX attachment.</response>
    /// <response code="400">Invalid format or filter, or the export exceeds the row cap.</response>
    [HttpGet(AuditExportTypes.Refund)]
    [HasPermission(AppPermissions.AuditExport)]
    [HasPermission(AppPermissions.PaymentView)]
    [Produces("text/csv", XlsxContentType)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task ExportRefund(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? userId = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        StreamExportAsync(
            AuditExportType.Refund,
            format,
            new AuditTypedExportFilters
            {
                StartDate = startDate,
                EndDate = endDate,
                UserId = userId,
                CashRegisterId = cashRegisterId,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                Page = page,
                PageSize = pageSize,
            },
            cancellationToken);

    /// <summary>Voucher redemption lines from the voucher ledger. The voucher code is never exported.</summary>
    /// <param name="format"><c>csv</c> (default) or <c>xlsx</c>.</param>
    /// <param name="startDate">Inclusive calendar start date.</param>
    /// <param name="endDate">Inclusive calendar end date.</param>
    /// <param name="userId">User who booked the redemption.</param>
    /// <param name="voucherId">Voucher id.</param>
    /// <param name="minAmount">Lower bound on the redeemed amount.</param>
    /// <param name="maxAmount">Upper bound on the redeemed amount.</param>
    /// <param name="page">1-based page. Omit with <paramref name="pageSize"/> to export the filtered set.</param>
    /// <param name="pageSize">Page size. Omit with <paramref name="page"/> to export the filtered set.</param>
    /// <response code="200">CSV or XLSX attachment.</response>
    /// <response code="400">Invalid format or filter, or the export exceeds the row cap.</response>
    [HttpGet(AuditExportTypes.VoucherRedemption)]
    [HasPermission(AppPermissions.AuditExport)]
    [HasPermission(AppPermissions.VoucherAuditView)]
    [Produces("text/csv", XlsxContentType)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task ExportVoucherRedemption(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? userId = null,
        [FromQuery] Guid? voucherId = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default) =>
        StreamExportAsync(
            AuditExportType.VoucherRedemption,
            format,
            new AuditTypedExportFilters
            {
                StartDate = startDate,
                EndDate = endDate,
                UserId = userId,
                VoucherId = voucherId,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                Page = page,
                PageSize = pageSize,
            },
            cancellationToken);

    private async Task StreamExportAsync(
        AuditExportType type,
        string format,
        AuditTypedExportFilters filters,
        CancellationToken cancellationToken)
    {
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        if (normalized is not ("csv" or "xlsx"))
        {
            await WriteErrorAsync(
                StatusCodes.Status400BadRequest, "format must be csv or xlsx.", "AUDIT_EXPORT_FORMAT", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);

            var tenantSlug = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var fileName = _fileNaming.GenerateFileName(
                $"audit-{AuditExportTypes.ToWireName(type).Replace('_', '-')}",
                normalized,
                registerId: ExportFileNameSegments.DateOnly(filters.StartDate),
                additional: ExportFileNameSegments.DateOnly(filters.EndDate),
                tenantSlug: tenantSlug);

            Response.ContentType = normalized == "xlsx" ? XlsxContentType : CsvContentType;
            Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

            await using var counting = new CountingStream(Response.Body);
            await _exportService.StreamAsync(type, tenantId, filters, normalized, counting, cancellationToken)
                .ConfigureAwait(false);

            await LogExportAsync(tenantId, fileName, counting.BytesWritten, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            if (Response.HasStarted)
                throw;
            await WriteErrorAsync(StatusCodes.Status400BadRequest, ex.Message, "AUDIT_EXPORT_INVALID_FILTER", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (!Response.HasStarted)
        {
            await WriteErrorAsync(StatusCodes.Status400BadRequest, ex.Message, "EXPORT_TOO_LARGE", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Client went away mid-stream; nothing left to send.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting {ExportType} audit export", AuditExportTypes.ToWireName(type));
            if (Response.HasStarted)
                return;
            await WriteErrorAsync(
                StatusCodes.Status500InternalServerError,
                "Internal server error while exporting audit data.",
                "AUDIT_EXPORT_ERROR",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LogExportAsync(Guid tenantId, string fileName, long bytes, CancellationToken cancellationToken)
    {
        try
        {
            await _downloadSecurity.LogDownloadAuditAsync(
                User.GetActorUserId() ?? "unknown",
                User.GetActorRole() ?? "Unknown",
                tenantId,
                SensitiveExportKinds.AuditLogExport,
                fileName,
                bytes,
                resourceId: null,
                correlationId: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The export already reached the client; a failed audit write must not turn it into an error.
            _logger.LogWarning(ex, "Could not write the download audit entry for {FileName}", fileName);
        }
    }

    private async Task WriteErrorAsync(int statusCode, string message, string code, CancellationToken cancellationToken)
    {
        Response.Clear();
        Response.StatusCode = statusCode;
        await Response.WriteAsJsonAsync(new { message, code }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts bytes passed to the inner stream without buffering them.</summary>
    private sealed class CountingStream : Stream
    {
        private readonly Stream _inner;

        public CountingStream(Stream inner) => _inner = inner;

        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            BytesWritten += buffer.Length;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}

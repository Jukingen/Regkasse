using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/export-email")]
[Produces("application/json")]
public sealed class AdminExportEmailController : ControllerBase
{
    private readonly IExportEmailDeliveryService _delivery;
    private readonly IDepExportHistoryService _depHistory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<AdminExportEmailController> _logger;

    public AdminExportEmailController(
        IExportEmailDeliveryService delivery,
        IDepExportHistoryService depHistory,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AdminExportEmailController> logger)
    {
        _delivery = delivery;
        _depHistory = depHistory;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Send or schedule an export email. Provide a file upload and/or <c>sourceKind</c>+<c>sourceId</c>
    /// (currently <c>dep-export</c>). Large files are sent as a time-limited download link.
    /// </summary>
    [HttpPost("send")]
    [HasPermission(AppPermissions.ReportExport)]
    [RequestSizeLimit(512L * 1024 * 1024)]
    [ProducesResponseType(typeof(SendExportEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendExportEmailResponse>> Send(
        [FromForm] SendExportEmailRequest request,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { message = "Authenticated user is required.", code = "USER_REQUIRED" });

        if (request is null || string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { message = "To and Subject are required.", code = "VALIDATION" });

        byte[] content;
        string fileName;
        string contentType;

        try
        {
            (content, fileName, contentType) = await ResolveContentAsync(
                    tenantId.Value,
                    request,
                    file,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "Export source not found.", code = "SOURCE_NOT_FOUND" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "VALIDATION" });
        }

        try
        {
            var result = await _delivery
                .SendOrScheduleAsync(
                    new ExportEmailSendInput
                    {
                        TenantId = tenantId.Value,
                        UserId = userId,
                        To = request.To,
                        Subject = request.Subject,
                        Message = request.Message,
                        ScheduledForUtc = request.ScheduledForUtc,
                        SourceKind = request.SourceKind,
                        SourceId = request.SourceId,
                        PreferLink = request.PreferLink,
                        FileName = fileName,
                        ContentType = contentType,
                        Content = content,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Export email {Status} id={Id} mode={Mode} size={Size}",
                result.Status,
                result.Id,
                result.DeliveryMode,
                result.FileSizeBytes);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "VALIDATION" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, code = "SMTP_OR_STATE" });
        }
    }

    [HttpGet("history")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(ExportEmailDeliveryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExportEmailDeliveryListResponse>> History(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var result = await _delivery
            .ListAsync(tenantId.Value, status, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("history/{id:guid}/cancel")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(SendExportEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendExportEmailResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        try
        {
            var result = await _delivery
                .CancelScheduledAsync(tenantId.Value, id, cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                return NotFound();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, code = "INVALID_STATE" });
        }
    }

    private async Task<(byte[] Content, string FileName, string ContentType)> ResolveContentAsync(
        Guid tenantId,
        SendExportEmailRequest request,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is { Length: > 0 })
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var name = string.IsNullOrWhiteSpace(file.FileName) ? "export.bin" : Path.GetFileName(file.FileName);
            var ct = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            return (ms.ToArray(), name, ct);
        }

        var kind = request.SourceKind?.Trim().ToLowerInvariant();
        if ((kind is "dep-export" or "dep-export-live") && request.SourceId is Guid sourceId)
        {
            var opened = await _depHistory.TryOpenDownloadAsync(sourceId, cancellationToken).ConfigureAwait(false);
            if (opened is null)
                throw new FileNotFoundException("DEP export not found.");

            await using var stream = opened.Value.Stream;
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var meta = await _depHistory.GetByIdAsync(sourceId, cancellationToken).ConfigureAwait(false);
            if (meta is null)
                throw new FileNotFoundException("DEP export not found.");

            return (ms.ToArray(), opened.Value.FileName, opened.Value.ContentType);
        }

        throw new ArgumentException(
            "Provide a file upload or a supported sourceKind/sourceId (dep-export).");
    }
}

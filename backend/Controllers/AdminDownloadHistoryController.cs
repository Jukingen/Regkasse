using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/download-history")]
[Produces("application/json")]
public sealed class AdminDownloadHistoryController : ControllerBase
{
    private readonly IDownloadHistoryService _history;
    private readonly IDepExportHistoryService _depHistory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly DownloadHistoryOptions _options;
    private readonly ILogger<AdminDownloadHistoryController> _logger;

    public AdminDownloadHistoryController(
        IDownloadHistoryService history,
        IDepExportHistoryService depHistory,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<DownloadHistoryOptions> options,
        ILogger<AdminDownloadHistoryController> logger)
    {
        _history = history;
        _depHistory = depHistory;
        _tenantAccessor = tenantAccessor;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Lists recent export downloads for the ambient tenant (newest first).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(DownloadHistoryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadHistoryListResponse>> List(
        [FromQuery] string? userId = null,
        [FromQuery] string? fileType = null,
        [FromQuery] string? sourceKind = null,
        [FromQuery] string? q = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var result = await _history
            .ListAsync(
                tenantId.Value,
                userId,
                fileType,
                sourceKind,
                q,
                fromUtc,
                toUtc,
                page,
                pageSize,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Aggregate download stats for the ambient tenant (optionally scoped to current user).</summary>
    [HttpGet("stats")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(DownloadHistoryStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadHistoryStatsDto>> Stats(
        [FromQuery] bool mineOnly = true,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        string? userId = null;
        if (mineOnly)
        {
            userId = User.GetActorUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { message = "Authenticated user is required.", code = "USER_REQUIRED" });
        }

        var retentionDays = Math.Clamp(_options.RetentionDays, 1, 365);
        var stats = await _history
            .GetStatsAsync(tenantId.Value, userId, retentionDays, cancellationToken)
            .ConfigureAwait(false);
        return Ok(stats);
    }

    /// <summary>
    /// Download analytics dashboard: totals, top kinds/users, usage trends, slow exports.
    /// Super Admin may request <c>platform=true</c> for cross-tenant popularity.
    /// </summary>
    [HttpGet("analytics")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(DownloadHistoryAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadHistoryAnalyticsDto>> Analytics(
        [FromQuery] bool platform = false,
        CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var includePlatform = platform && isSuperAdmin;
        var tenantId = _tenantAccessor.TenantId;

        if (!includePlatform && tenantId is null)
            return NotFound();

        var retentionDays = Math.Clamp(_options.RetentionDays, 1, 365);
        var analytics = await _history
            .GetAnalyticsAsync(
                includePlatform ? null : tenantId,
                includePlatform,
                retentionDays,
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(analytics);
    }

    /// <summary>Deletes download-history rows older than the configured retention window for this tenant.</summary>
    [HttpPost("cleanup-old")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(DownloadHistoryCleanupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadHistoryCleanupResultDto>> CleanupOld(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var retentionDays = Math.Clamp(_options.RetentionDays, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await _history
            .CleanupTenantOlderThanAsync(tenantId.Value, cutoff, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new DownloadHistoryCleanupResultDto
        {
            DeletedCount = deleted,
            RetentionDays = retentionDays,
        });
    }

    /// <summary>Records a client-side (or server) download for the download-history UI.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(DownloadHistoryListItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadHistoryListItemDto>> Record(
        [FromBody] RecordDownloadHistoryRequest body,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { message = "Authenticated user is required.", code = "USER_REQUIRED" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var row = await _history.RecordAsync(
                new DownloadHistoryRecordRequest
                {
                    TenantId = tenantId.Value,
                    UserId = userId,
                    FileName = body.FileName,
                    FileType = body.FileType,
                    FileSize = body.FileSize,
                    DownloadUrl = body.DownloadUrl,
                    IpAddress = ResolveClientIpAddress(),
                    UserAgent = ResolveUserAgent(),
                    SourceKind = body.SourceKind,
                    SourceId = body.SourceId,
                    DurationMs = body.DurationMs,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Created(
            $"/api/admin/download-history/{row.Id}",
            ToDto(row));
    }

    /// <summary>
    /// Re-downloads a previously recorded export when a resolvable <c>sourceKind</c>/<c>sourceId</c> exists.
    /// Currently supports <c>dep-export</c> → stored DEP history artifact.
    /// </summary>
    [HttpGet("{id:guid}/redownload")]
    [HasPermission(AppPermissions.AuditView)]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Redownload(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var row = await _history.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null || row.TenantId != tenantId.Value)
            return NotFound();

        if (string.IsNullOrWhiteSpace(row.SourceKind) || row.SourceId is null)
        {
            return NotFound(new
            {
                message = "Original file is not available for re-download.",
                code = "DOWNLOAD_REDOWNLOAD_UNAVAILABLE",
            });
        }

        if (string.Equals(row.SourceKind, "dep-export", StringComparison.OrdinalIgnoreCase))
        {
            var file = await _depHistory.TryOpenDownloadAsync(row.SourceId.Value, cancellationToken).ConfigureAwait(false);
            if (file is null)
            {
                return NotFound(new
                {
                    message = "Stored DEP export file not available.",
                    code = "RKSV_DEP_EXPORT_FILE_NOT_FOUND",
                });
            }

            var userId = User.GetActorUserId() ?? "unknown";
            var downloadUrl = $"/api/admin/download-history/{id}/redownload";
            try
            {
                await _history.RecordAsync(
                        new DownloadHistoryRecordRequest
                        {
                            TenantId = tenantId.Value,
                            UserId = userId,
                            FileName = file.Value.FileName,
                            FileType = "json",
                            FileSize = row.FileSize,
                            DownloadUrl = downloadUrl,
                            IpAddress = ResolveClientIpAddress(),
                            UserAgent = ResolveUserAgent(),
                            SourceKind = "dep-export",
                            SourceId = row.SourceId,
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record re-download history for {Id}", id);
            }

            return File(file.Value.Stream, file.Value.ContentType, file.Value.FileName);
        }

        return NotFound(new
        {
            message = $"Re-download is not supported for source kind '{row.SourceKind}'.",
            code = "DOWNLOAD_REDOWNLOAD_UNSUPPORTED",
        });
    }

    private string? ResolveClientIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? ResolveUserAgent()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua;
    }

    private static DownloadHistoryListItemDto ToDto(Models.DownloadHistory row) =>
        new()
        {
            Id = row.Id,
            FileName = row.FileName,
            FileType = row.FileType,
            FileSize = row.FileSize,
            DownloadUrl = row.DownloadUrl,
            DownloadedAt = row.DownloadedAt,
            UserId = row.UserId,
            IpAddress = row.IpAddress,
            UserAgent = row.UserAgent,
            SourceKind = row.SourceKind,
            SourceId = row.SourceId,
            CanRedownload = row.SourceKind != null && row.SourceId != null,
        };
}

public sealed class RecordDownloadHistoryRequest
{
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string FileType { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long? FileSize { get; set; }

    /// <summary>Optional observed download duration in milliseconds.</summary>
    [Range(0, int.MaxValue)]
    public int? DurationMs { get; set; }

    /// <summary>Optional API path / opaque reference (no secrets).</summary>
    [MaxLength(2000)]
    public string? DownloadUrl { get; set; }

    [MaxLength(64)]
    public string? SourceKind { get; set; }

    public Guid? SourceId { get; set; }
}

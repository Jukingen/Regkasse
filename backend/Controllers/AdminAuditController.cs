using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/audit")]
[Produces("application/json")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditExportService _exportService;
    private readonly IAuditExportJobManager _exportJobs;
    private readonly IAuditReportScheduler _reportScheduler;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IFileNamingService _fileNaming;
    private readonly AppDbContext _db;
    private readonly AuditRetentionOptions _retentionOptions;
    private readonly ILogger<AdminAuditController> _logger;
    private readonly IDownloadSecurityService _downloadSecurity;
    private readonly IActorDisplayNameResolver _actorDisplayNameResolver;

    public AdminAuditController(
        IAuditExportService exportService,
        IAuditExportJobManager exportJobs,
        IAuditReportScheduler reportScheduler,
        ICurrentTenantAccessor tenantAccessor,
        IFileNamingService fileNaming,
        AppDbContext db,
        IOptions<AuditRetentionOptions> retentionOptions,
        ILogger<AdminAuditController> logger,
        IDownloadSecurityService downloadSecurity,
        IActorDisplayNameResolver actorDisplayNameResolver)
    {
        _exportService = exportService;
        _exportJobs = exportJobs;
        _reportScheduler = reportScheduler;
        _tenantAccessor = tenantAccessor;
        _fileNaming = fileNaming;
        _db = db;
        _retentionOptions = retentionOptions.Value;
        _logger = logger;
        _downloadSecurity = downloadSecurity;
        _actorDisplayNameResolver = actorDisplayNameResolver;
    }

    [HttpGet("retention")]
    [HasPermission(AppPermissions.AuditView)]
    public ActionResult<AuditRetentionInfoResponse> GetRetention()
    {
        var years = _retentionOptions.RetentionYears > 0 ? _retentionOptions.RetentionYears : 7;
        var minCutoff = DateTime.UtcNow.Date.AddYears(-years);
        return Ok(new AuditRetentionInfoResponse
        {
            RetentionYears = years,
            MinCutoffDate = minCutoff,
            Message = $"Audit logs must be retained for at least {years} years (RKSV compliance).",
        });
    }

    /// <summary>
    /// Super Admin: paginated audit log for a specific tenant (cross-tenant; IgnoreQueryFilters).
    /// </summary>
    [HttpGet("tenant/{tenantId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(TenantAuditLogsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantAuditLogsResponse>> GetTenantAuditLogs(
        Guid tenantId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? action = null,
        [FromQuery] string? userId = null,
        [FromQuery] string sortBy = "timestamp",
        [FromQuery] string sortOrder = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantExists = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            return NotFound(new { message = "Tenant not found." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 100);

        var filters = new AuditLogQueryFilters
        {
            StartDate = startDate,
            EndDate = endDate,
            Action = action,
            UserId = userId,
        };

        var query = _db.AuditLogs.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ApplyFilters(filters);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var sortAction = string.Equals(sortBy, "action", StringComparison.OrdinalIgnoreCase);
        var asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<AuditLog> ordered = sortAction
            ? (asc
                ? query.OrderBy(a => a.Action).ThenByDescending(a => a.Timestamp)
                : query.OrderByDescending(a => a.Action).ThenByDescending(a => a.Timestamp))
            : (asc
                ? query.OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
                : query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id));

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                a.UserId,
                a.Action,
                a.Description,
                a.Notes,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var displayNames = await _actorDisplayNameResolver
            .ResolveAsync(rows.Select(r => r.UserId).Distinct().ToList())
            .ConfigureAwait(false);

        var items = rows.Select(r => new TenantAuditLogItemDto
        {
            Id = r.Id,
            Timestamp = r.Timestamp,
            UserId = r.UserId,
            UserDisplayName = displayNames.TryGetValue(r.UserId, out var name) ? name : null,
            Action = r.Action,
            Details = r.Description ?? r.Notes,
        }).ToList();

        _logger.LogInformation(
            "Super Admin retrieved {Count} audit logs for tenant {TenantId} (page {Page})",
            items.Count,
            tenantId,
            page);

        return Ok(new TenantAuditLogsResponse
        {
            Success = true,
            TenantId = tenantId,
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0,
            Message = "Tenant audit logs retrieved successfully",
        });
    }

    [HttpPost("export")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult> StartExport([FromBody] AuditExportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var filters = request.ToFilters();
        var count = await _exportService.CountForExportAsync(filters, cancellationToken).ConfigureAwait(false);
        if (count > IAuditExportService.MaxExportRows)
            return BadRequest(new { message = $"Export exceeds {IAuditExportService.MaxExportRows} rows ({count} matched). Narrow filters.", code = "EXPORT_TOO_LARGE" });

        var userId = User.GetActorUserId() ?? "unknown";
        var role = User.GetActorRole() ?? "Unknown";
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);

        if (count >= IAuditExportService.BackgroundExportThreshold)
        {
            // Gate at job start so approval/2FA happens before enqueue; download still re-checks.
            var startGate = await EvaluateAuditExportGateAsync(
                userId, role, tenantId, resourceId: null, fileSizeBytes: null, isSuperAdmin, cancellationToken)
                .ConfigureAwait(false);
            if (!startGate.Allowed)
                return StatusCode(startGate.StatusCode, startGate.Body);

            var jobId = await _exportJobs.StartJobAsync(tenantId.Value, filters, request.Format, cancellationToken).ConfigureAwait(false);
            return Accepted(new
            {
                jobId,
                matchedRows = count,
                message = "Background export started. Poll GET export/jobs/{jobId}.",
                downloadTicket = startGate.DownloadTicket,
                ticketExpiresAtUtc = startGate.TicketExpiresAtUtc,
            });
        }

        var format = request.Format.Trim().ToLowerInvariant();
        var contentType = AuditExportFileNames.ContentTypeForFormat(format);
        var tenantSlug = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId.Value)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var fileName = _fileNaming.GenerateFileName(
            AuditExportFileNames.Prefix,
            AuditExportFileNames.NormalizeExtension(format),
            registerId: ExportFileNameSegments.DateOnly(filters.StartDate),
            additional: ExportFileNameSegments.DateOnly(filters.EndDate),
            tenantSlug: tenantSlug);
        await using var ms = new MemoryStream();
        await _exportService.StreamExportAsync(filters, request.Format, ms, cancellationToken).ConfigureAwait(false);
        var bytes = ms.ToArray();

        var gate = await EvaluateAuditExportGateAsync(
                userId, role, tenantId, resourceId: null, fileSizeBytes: bytes.LongLength, isSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (!gate.Allowed)
            return StatusCode(gate.StatusCode, gate.Body);

        if (!string.IsNullOrWhiteSpace(gate.DownloadTicket))
        {
            Response.Headers[DownloadSecurityService.HeaderDownloadTicket] = gate.DownloadTicket;
            if (gate.TicketExpiresAtUtc.HasValue)
                Response.Headers["X-Download-Ticket-Expires"] = gate.TicketExpiresAtUtc.Value.ToString("O");
        }

        await _downloadSecurity.LogDownloadAuditAsync(
            userId,
            role,
            tenantId,
            SensitiveExportKinds.AuditLogExport,
            fileName,
            bytes.LongLength,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        return File(bytes, contentType, fileName);
    }

    [HttpGet("export/jobs/{jobId}")]
    [HasPermission(AppPermissions.AuditExport)]
    public ActionResult<AuditExportJobStatusDto> GetExportJob(string jobId)
    {
        var status = _exportJobs.GetStatus(jobId);
        if (status == null)
            return NotFound();
        return Ok(status);
    }

    [HttpGet("export/jobs/{jobId}/download")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult> DownloadExportJob(string jobId, CancellationToken cancellationToken)
    {
        if (!_exportJobs.TryOpenDownload(jobId, out var stream, out var fileName, out var contentType) || stream == null)
            return NotFound(new { message = "Export not ready or expired." });

        var userId = User.GetActorUserId() ?? "unknown";
        var role = User.GetActorRole() ?? "Unknown";
        var tenantId = _tenantAccessor.TenantId;
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        long? size = stream.CanSeek ? stream.Length : null;

        var gate = await EvaluateAuditExportGateAsync(
                userId, role, tenantId, resourceId: jobId, fileSizeBytes: size, isSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        if (!gate.Allowed)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            return StatusCode(gate.StatusCode, gate.Body);
        }

        if (!string.IsNullOrWhiteSpace(gate.DownloadTicket))
        {
            Response.Headers[DownloadSecurityService.HeaderDownloadTicket] = gate.DownloadTicket;
            if (gate.TicketExpiresAtUtc.HasValue)
                Response.Headers["X-Download-Ticket-Expires"] = gate.TicketExpiresAtUtc.Value.ToString("O");
        }

        await _downloadSecurity.LogDownloadAuditAsync(
            userId,
            role,
            tenantId,
            SensitiveExportKinds.AuditLogExport,
            fileName ?? "audit-export",
            size,
            jobId,
            null,
            cancellationToken).ConfigureAwait(false);

        return File(stream, contentType ?? "text/csv", fileName);
    }

    private Task<DownloadSecurityEvaluateResult> EvaluateAuditExportGateAsync(
        string userId,
        string role,
        Guid? tenantId,
        string? resourceId,
        long? fileSizeBytes,
        bool isSuperAdmin,
        CancellationToken cancellationToken) =>
        _downloadSecurity.EvaluateAsync(
            new DownloadSecurityEvaluateRequest
            {
                UserId = userId,
                UserRole = role,
                TenantId = tenantId,
                ExportKind = SensitiveExportKinds.AuditLogExport,
                ResourceId = resourceId,
                FileSizeBytes = fileSizeBytes,
                PrivacyAck = DownloadSecurityHttp.ReadPrivacyAck(Request),
                TwoFactorCode = DownloadSecurityHttp.ReadTwoFactorCode(Request),
                ApprovalId = DownloadSecurityHttp.ReadApprovalId(Request),
                DownloadTicket = DownloadSecurityHttp.ReadDownloadTicket(Request),
                IsSuperAdmin = isSuperAdmin,
            },
            cancellationToken);

    [HttpPost("schedule-report")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult<AuditReportScheduleResponse>> ScheduleReport(
        [FromBody] ScheduleAuditReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var actorId = User.GetActorUserId() ?? "system";
        try
        {
            var schedule = await _reportScheduler.CreateScheduleAsync(
                tenantId.Value,
                actorId,
                request.Name,
                request.Filters ?? new AuditLogQueryFilters(),
                request.Schedule,
                request.Recipients,
                request.Format,
                cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, ToScheduleDto(schedule));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "INVALID_SCHEDULE" });
        }
    }

    [HttpGet("schedules")]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<IEnumerable<AuditReportScheduleResponse>>> ListSchedules(CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return Ok(Array.Empty<AuditReportScheduleResponse>());

        var list = await _reportScheduler.GetSchedulesAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        return Ok(list.Select(ToScheduleDto));
    }

    [HttpGet("schedules/{id:guid}")]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<AuditReportScheduleResponse>> GetSchedule(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await _reportScheduler.GetScheduleByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule == null)
            return NotFound();
        return Ok(ToScheduleDto(schedule));
    }

    [HttpDelete("schedules/{id:guid}")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult> DeactivateSchedule(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _reportScheduler.DeactivateScheduleAsync(id, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return NotFound();
        return NoContent();
    }

    private static AuditReportScheduleResponse ToScheduleDto(AuditReportSchedule s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ScheduleCron = s.ScheduleCron,
        Format = s.Format,
        IsActive = s.IsActive,
        Recipients = JsonSerializer.Deserialize<List<string>>(s.RecipientsJson) ?? new List<string>(),
        Filters = JsonSerializer.Deserialize<AuditLogQueryFilters>(s.FiltersJson),
        LastRunUtc = s.LastRunUtc,
        NextRunUtc = s.NextRunUtc,
        CreatedAtUtc = s.CreatedAtUtc,
    };
}

public sealed class AuditRetentionInfoResponse
{
    public int RetentionYears { get; set; }
    public DateTime MinCutoffDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class AuditExportRequest
{
    [Required]
    public string Format { get; set; } = "csv";

    public AuditLogQueryFilters? Filters { get; set; }

    public AuditLogQueryFilters ToFilters() => Filters ?? new AuditLogQueryFilters();
}

public sealed class ScheduleAuditReportRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AuditLogQueryFilters? Filters { get; set; }

    [Required]
    public string Schedule { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> Recipients { get; set; } = new();

    [Required]
    public string Format { get; set; } = "csv";
}

public sealed class AuditReportScheduleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScheduleCron { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Recipients { get; set; } = new();
    public AuditLogQueryFilters? Filters { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

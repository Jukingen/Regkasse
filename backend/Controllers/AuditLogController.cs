using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Comprehensive audit log controller for managing and accessing audit logs
    /// This controller provides detailed audit trail access for compliance and security monitoring
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IAuditExportService _auditExportService;
        private readonly IActorDisplayNameResolver _actorDisplayNameResolver;
        private readonly ILogger<AuditLogController> _logger;

        public AuditLogController(
            IAuditLogService auditLogService,
            IAuditExportService auditExportService,
            IActorDisplayNameResolver actorDisplayNameResolver,
            ILogger<AuditLogController> logger)
        {
            _auditLogService = auditLogService;
            _auditExportService = auditExportService;
            _actorDisplayNameResolver = actorDisplayNameResolver;
            _logger = logger;
        }

        private bool IsActorSuperAdmin() =>
            string.Equals(
                RoleCanonicalization.GetCanonicalRole(User.FindFirstValue(ClaimTypes.Role) ?? string.Empty),
                Roles.SuperAdmin,
                StringComparison.Ordinal);

        private static void ApplyTenantManagerVisibility(AuditLogQueryFilters filters, bool actorIsSuperAdmin)
        {
            if (!actorIsSuperAdmin)
                filters.ExcludePlatformOperatorActors = true;
        }

        /// <summary>
        /// GET: api/auditlog - Get all audit logs with filtering and pagination
        /// </summary>
        [HttpGet]
        [HasPermission(AppPermissions.AuditView)]
        public async Task<ActionResult<AuditLogsResponse>> GetAuditLogs(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? targetUserId = null,
            [FromQuery] string? userRole = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] Guid? entityId = null,
            [FromQuery] string? ipAddress = null,
            [FromQuery] AuditLogStatus? status = null,
            [FromQuery] string? statusOutcome = null,
            [FromQuery] bool? hasChanges = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? afterCursor = null,
            [FromQuery] bool includeTotalCount = true)
        {
            try
            {
                if (page < 1)
                    page = 1;
                if (pageSize < 1 || pageSize > 100)
                    pageSize = 50;

                var filters = AuditLogQueryExtensions.ToFilters(
                    startDate, endDate, userId, userRole, targetUserId, action, entityType, entityId,
                    ipAddress, status, statusOutcome, hasChanges);
                filters.Search = search;
                ApplyTenantManagerVisibility(filters, IsActorSuperAdmin());

                var includeTotal = includeTotalCount && page == 1 && string.IsNullOrWhiteSpace(afterCursor);
                var (items, meta) = await _auditLogService.GetAuditLogsPagedAsync(
                    filters, pageSize, afterCursor, page, includeTotal);

                var totalCount = meta.TotalCount ?? 0;
                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = AuditLogEntryMapper.ToDtoList(items.ToList()),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0,
                    NextCursor = meta.NextCursor,
                    HasMore = meta.HasMore,
                    Message = "Audit logs retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} audit logs (page {Page}, hasMore={HasMore})",
                    items.Count, page, meta.HasMore);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs");
                return StatusCode(500, new { message = "Internal server error while retrieving audit logs.", code = "AUDIT_LOG_LIST_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/{id} - Get specific audit log by ID
        /// </summary>
        [HttpGet("{id}")]
        [HasPermission(AppPermissions.AuditView)]
        public async Task<ActionResult<AuditLogResponse>> GetAuditLog(Guid id)
        {
            try
            {
                var auditLog = await _auditLogService.GetAuditLogByIdAsync(id);

                if (auditLog == null)
                {
                    return NotFound(new { message = "Audit log not found" });
                }

                var response = new AuditLogResponse
                {
                    Success = true,
                    AuditLog = auditLog,
                    Message = "Audit log retrieved successfully"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit log {AuditLogId}", id);
                return StatusCode(500, new { message = "Internal server error while retrieving audit log.", code = "AUDIT_LOG_GET_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/payment/{paymentId} - Get audit logs for specific payment
        /// </summary>
        [HttpGet("payment/{paymentId}")]
        [HasPermission(AppPermissions.AuditView)]
        public async Task<ActionResult<AuditLogsResponse>> GetPaymentAuditLogs(
            Guid paymentId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1)
                    page = 1;
                if (pageSize < 1 || pageSize > 100)
                    pageSize = 50;

                var auditLogs = await _auditLogService.GetPaymentAuditLogsAsync(
                    paymentId, startDate, endDate, page, pageSize);
                var paymentList = auditLogs.ToList();

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = AuditLogEntryMapper.ToDtoList(paymentList),
                    TotalCount = paymentList.Count,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 1, // For payment-specific logs, we don't need pagination
                    Message = $"Payment audit logs for payment {paymentId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} payment audit logs for payment {PaymentId}",
                    paymentList.Count, paymentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment audit logs for payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Internal server error while retrieving payment audit logs.", code = "AUDIT_LOG_PAYMENT_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/user/{userId} - Get audit logs for specific user
        /// </summary>
        [HttpGet("user/{userId}")]
        [HasPermission(AppPermissions.UserView)]
        public async Task<ActionResult<AuditLogsResponse>> GetUserAuditLogs(
            string userId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { message = "UserId is required.", code = "VALIDATION_ERROR" });
            }
            try
            {
                if (page < 1)
                    page = 1;
                if (pageSize < 1 || pageSize > 100)
                    pageSize = 50;

                var auditLogs = await _auditLogService.GetUserAuditLogsAsync(
                    userId, startDate, endDate, page, pageSize);
                var list = auditLogs?.ToList() ?? new List<AuditLog>();

                var totalCount = await _auditLogService.GetUserLifecycleAuditLogsCountAsync(
                    userId, startDate, endDate);

                var actorDisplayNames = await _actorDisplayNameResolver.ResolveAsync(list.Select(l => l.UserId).Distinct().ToList());

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = AuditLogEntryMapper.ToDtoList(list, actorDisplayNames),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize),
                    Message = $"User audit logs for user {userId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} user audit logs for user {UserId} (page {Page} of {TotalPages})",
                    list.Count, userId, page, response.TotalPages);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user audit logs for user {UserId}", userId);
                return StatusCode(500, new { message = "Internal server error while retrieving user audit logs.", code = "AUDIT_LOG_USER_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/correlation/{correlationId} - Get audit logs by correlation ID
        /// </summary>
        [HttpGet("correlation/{correlationId}")]
        [HasPermission(AppPermissions.AuditView)]
        public async Task<ActionResult<AuditLogsResponse>> GetAuditLogsByCorrelationId(string correlationId)
        {
            try
            {
                var auditLogs = await _auditLogService.GetAuditLogsByCorrelationIdAsync(correlationId);
                var corrList = auditLogs.ToList();

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = AuditLogEntryMapper.ToDtoList(corrList),
                    TotalCount = corrList.Count,
                    Page = 1,
                    PageSize = corrList.Count,
                    TotalPages = 1,
                    Message = $"Audit logs for correlation ID {correlationId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} audit logs for correlation ID {CorrelationId}",
                    corrList.Count, correlationId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for correlation ID {CorrelationId}", correlationId);
                return StatusCode(500, new { message = "Internal server error while retrieving correlation audit logs.", code = "AUDIT_LOG_CORRELATION_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/suspicious-admin-actions - Incident playbook: high-risk user-lifecycle actions (deactivate, reactivate, password reset, role change, create).
        /// </summary>
        [HttpGet("suspicious-admin-actions")]
        [HasPermission(AppPermissions.UserView)]
        public async Task<ActionResult<AuditLogsResponse>> GetSuspiciousAdminActions(
            [FromQuery] DateTime? since = null,
            [FromQuery] int limit = 100)
        {
            try
            {
                var auditLogs = await _auditLogService.GetSuspiciousAdminActionsAsync(since, limit);
                var list = auditLogs.ToList();
                return Ok(new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = AuditLogEntryMapper.ToDtoList(list),
                    TotalCount = list.Count,
                    Page = 1,
                    PageSize = list.Count,
                    TotalPages = 1,
                    Message = "Suspicious admin actions retrieved for incident review"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suspicious admin actions");
                return StatusCode(500, new { message = "Internal server error while retrieving suspicious admin actions.", code = "AUDIT_LOG_SUSPICIOUS_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/transaction/{transactionId} - Get audit logs by transaction ID
        /// </summary>
        [HttpGet("transaction/{transactionId}")]
        [HasPermission(AppPermissions.AuditView)]
        public async Task<ActionResult<AuditLogsResponse>> GetAuditLogsByTransactionId(string transactionId)
        {
            try
            {
                var auditLogs = await _auditLogService.GetAuditLogsByTransactionIdAsync(transactionId);
                var txList = auditLogs.ToList();

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = AuditLogEntryMapper.ToDtoList(txList),
                    TotalCount = txList.Count,
                    Page = 1,
                    PageSize = txList.Count,
                    TotalPages = 1,
                    Message = $"Audit logs for transaction ID {transactionId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} audit logs for transaction ID {TransactionId}",
                    txList.Count, transactionId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for transaction ID {TransactionId}", transactionId);
                return StatusCode(500, new { message = "Internal server error while retrieving transaction audit logs.", code = "AUDIT_LOG_TRANSACTION_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/statistics - Get audit log statistics
        /// </summary>
        [HttpGet("statistics")]
        [HasPermission(AppPermissions.AuditView)]
        public async Task<ActionResult<AuditLogStatisticsResponse>> GetAuditLogStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var statistics = await _auditLogService.GetAuditLogStatisticsAsync(startDate, endDate);

                var response = new AuditLogStatisticsResponse
                {
                    Success = true,
                    Statistics = statistics,
                    StartDate = startDate,
                    EndDate = endDate,
                    Message = "Audit log statistics retrieved successfully"
                };

                _logger.LogInformation("Retrieved audit log statistics for period {StartDate} to {EndDate}",
                    startDate, endDate);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit log statistics");
                return StatusCode(500, new { message = "Internal server error while retrieving audit log statistics.", code = "AUDIT_LOG_STATISTICS_ERROR" });
            }
        }

        /// <summary>
        /// DELETE: api/auditlog/cleanup - Delete audit logs older than specified date. Sprint 5: respects 7-year retention and legal hold.
        /// </summary>
        [HttpDelete("cleanup")]
        [HasPermission(AppPermissions.AuditCleanup)]
        public async Task<ActionResult<AuditLogCleanupResponse>> CleanupOldAuditLogs(
            [FromBody] AuditLogCleanupRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState });
                }

                var result = await _auditLogService.DeleteAuditLogsOlderThanAsync(request.CutoffDate);

                if (!result.Success)
                {
                    _logger.LogWarning("Audit cleanup rejected: {Message}", result.ErrorMessage);
                    return BadRequest(new AuditLogCleanupResponse
                    {
                        Success = false,
                        CutoffDate = request.CutoffDate,
                        Message = result.ErrorMessage ?? "Cleanup rejected.",
                        DeletedCount = 0,
                        SkippedDueToLegalHoldCount = 0,
                        MinCutoffDate = result.MinCutoffDate
                    });
                }

                var response = new AuditLogCleanupResponse
                {
                    Success = true,
                    CutoffDate = request.CutoffDate,
                    Message = "Old audit logs cleaned up successfully",
                    DeletedCount = result.DeletedCount,
                    SkippedDueToLegalHoldCount = result.SkippedDueToLegalHoldCount,
                    MinCutoffDate = result.MinCutoffDate
                };

                _logger.LogInformation("Audit log cleanup completed for logs older than {CutoffDate}: deleted {DeletedCount}, skipped {SkippedCount} due to legal hold",
                    request.CutoffDate, result.DeletedCount, result.SkippedDueToLegalHoldCount);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit log cleanup");
                return StatusCode(500, new { message = "Internal server error during audit log cleanup.", code = "AUDIT_LOG_CLEANUP_ERROR" });
            }
        }

        /// <summary>
        /// GET: api/auditlog/export - Export audit logs to CSV/JSON
        /// </summary>
        [HttpGet("export")]
        [HasPermission(AppPermissions.AuditExport)]
        public async Task ExportAuditLogs(
            [FromQuery] string format = "json",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? targetUserId = null,
            [FromQuery] string? userRole = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] Guid? entityId = null,
            [FromQuery] string? ipAddress = null,
            [FromQuery] AuditLogStatus? status = null,
            [FromQuery] string? statusOutcome = null,
            [FromQuery] bool? hasChanges = null,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var filters = AuditLogQueryExtensions.ToFilters(
                    startDate, endDate, userId, userRole, targetUserId, action, entityType, entityId,
                    ipAddress, status, statusOutcome, hasChanges);
                filters.Search = search;
                ApplyTenantManagerVisibility(filters, IsActorSuperAdmin());

                var normalized = (format ?? "csv").Trim().ToLowerInvariant();
                var ext = normalized == "json" ? "json" : "csv";
                var contentType = normalized == "json" ? "application/json" : "text/csv";
                var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{ext}";

                Response.ContentType = contentType;
                Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
                await _auditExportService.StreamExportAsync(filters, format, Response.Body, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(new { message = ex.Message, code = "EXPORT_TOO_LARGE" }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs");
                if (!Response.HasStarted)
                {
                    Response.StatusCode = 500;
                    await Response.WriteAsJsonAsync(new { message = "Internal server error while exporting audit logs.", code = "AUDIT_LOG_EXPORT_ERROR" }, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public class AuditLogResponse
    {
        public bool Success { get; set; }
        public AuditLog AuditLog { get; set; } = null!;
        public string Message { get; set; } = string.Empty;
    }

    public class AuditLogStatisticsResponse
    {
        public bool Success { get; set; }
        public Dictionary<string, int> Statistics { get; set; } = new();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class AuditLogCleanupRequest
    {
        [Required]
        public DateTime CutoffDate { get; set; }
    }

    public class AuditLogCleanupResponse
    {
        public bool Success { get; set; }
        public DateTime CutoffDate { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>Sprint 5: number of logs deleted.</summary>
        public int DeletedCount { get; set; }
        /// <summary>Sprint 5: number of logs skipped because they fall within an active legal hold.</summary>
        public int SkippedDueToLegalHoldCount { get; set; }
        /// <summary>Sprint 5: earliest allowed cutoff date (today minus retention years).</summary>
        public DateTime? MinCutoffDate { get; set; }
    }
}

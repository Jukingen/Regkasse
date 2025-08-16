using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Services;
using KasseAPI_Final.Models;
using System.ComponentModel.DataAnnotations;

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
        private readonly ILogger<AuditLogController> _logger;

        public AuditLogController(IAuditLogService auditLogService, ILogger<AuditLogController> logger)
        {
            _auditLogService = auditLogService;
            _logger = logger;
        }

        /// <summary>
        /// GET: api/auditlog - Get all audit logs with filtering and pagination
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<AuditLogsResponse>> GetAuditLogs(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? userRole = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] Guid? entityId = null,
            [FromQuery] AuditLogStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var auditLogs = await _auditLogService.GetAuditLogsAsync(
                    startDate, endDate, userId, userRole, action, entityType, entityId, status, page, pageSize);

                var totalCount = await _auditLogService.GetAuditLogsCountAsync(
                    startDate, endDate, userId, userRole, action, entityType, entityId, status);

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = auditLogs.ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    Message = "Audit logs retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} audit logs (page {Page} of {TotalPages})", 
                    auditLogs.Count(), page, response.TotalPages);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs");
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving audit logs",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/{id} - Get specific audit log by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrator,Manager")]
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
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving audit log",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/payment/{paymentId} - Get audit logs for specific payment
        /// </summary>
        [HttpGet("payment/{paymentId}")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<AuditLogsResponse>> GetPaymentAuditLogs(
            Guid paymentId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var auditLogs = await _auditLogService.GetPaymentAuditLogsAsync(
                    paymentId, startDate, endDate, page, pageSize);

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = auditLogs.ToList(),
                    TotalCount = auditLogs.Count(),
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 1, // For payment-specific logs, we don't need pagination
                    Message = $"Payment audit logs for payment {paymentId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} payment audit logs for payment {PaymentId}", 
                    auditLogs.Count(), paymentId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment audit logs for payment {PaymentId}", paymentId);
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving payment audit logs",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/user/{userId} - Get audit logs for specific user
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<AuditLogsResponse>> GetUserAuditLogs(
            string userId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var auditLogs = await _auditLogService.GetUserAuditLogsAsync(
                    userId, startDate, endDate, page, pageSize);

                var totalCount = await _auditLogService.GetAuditLogsCountAsync(
                    startDate, endDate, userId, null, null, null, null, null);

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = auditLogs.ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    Message = $"User audit logs for user {userId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} user audit logs for user {UserId} (page {Page} of {TotalPages})", 
                    auditLogs.Count(), userId, page, response.TotalPages);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user audit logs for user {UserId}", userId);
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving user audit logs",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/correlation/{correlationId} - Get audit logs by correlation ID
        /// </summary>
        [HttpGet("correlation/{correlationId}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<AuditLogsResponse>> GetAuditLogsByCorrelationId(string correlationId)
        {
            try
            {
                var auditLogs = await _auditLogService.GetAuditLogsByCorrelationIdAsync(correlationId);

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = auditLogs.ToList(),
                    TotalCount = auditLogs.Count(),
                    Page = 1,
                    PageSize = auditLogs.Count(),
                    TotalPages = 1,
                    Message = $"Audit logs for correlation ID {correlationId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} audit logs for correlation ID {CorrelationId}", 
                    auditLogs.Count(), correlationId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for correlation ID {CorrelationId}", correlationId);
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving correlation audit logs",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/transaction/{transactionId} - Get audit logs by transaction ID
        /// </summary>
        [HttpGet("transaction/{transactionId}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<AuditLogsResponse>> GetAuditLogsByTransactionId(string transactionId)
        {
            try
            {
                var auditLogs = await _auditLogService.GetAuditLogsByTransactionIdAsync(transactionId);

                var response = new AuditLogsResponse
                {
                    Success = true,
                    AuditLogs = auditLogs.ToList(),
                    TotalCount = auditLogs.Count(),
                    Page = 1,
                    PageSize = auditLogs.Count(),
                    TotalPages = 1,
                    Message = $"Audit logs for transaction ID {transactionId} retrieved successfully"
                };

                _logger.LogInformation("Retrieved {Count} audit logs for transaction ID {TransactionId}", 
                    auditLogs.Count(), transactionId);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for transaction ID {TransactionId}", transactionId);
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving transaction audit logs",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/statistics - Get audit log statistics
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "Administrator,Manager")]
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
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving audit log statistics",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// DELETE: api/auditlog/cleanup - Delete audit logs older than specified date
        /// </summary>
        [HttpDelete("cleanup")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<AuditLogCleanupResponse>> CleanupOldAuditLogs(
            [FromBody] AuditLogCleanupRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Invalid request data", errors = ModelState });
                }

                var success = await _auditLogService.DeleteAuditLogsOlderThanAsync(request.CutoffDate);

                var response = new AuditLogCleanupResponse
                {
                    Success = success,
                    CutoffDate = request.CutoffDate,
                    Message = success ? "Old audit logs cleaned up successfully" : "Failed to cleanup old audit logs"
                };

                _logger.LogInformation("Audit log cleanup completed for logs older than {CutoffDate}", 
                    request.CutoffDate);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit log cleanup");
                return StatusCode(500, new { 
                    message = "Internal server error during audit log cleanup",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// GET: api/auditlog/export - Export audit logs to CSV/JSON
        /// </summary>
        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> ExportAuditLogs(
            [FromQuery] string format = "json",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? userRole = null,
            [FromQuery] string? action = null,
            [FromQuery] string? entityType = null,
            [FromQuery] Guid? entityId = null,
            [FromQuery] AuditLogStatus? status = null)
        {
            try
            {
                // Get all audit logs without pagination for export
                var auditLogs = await _auditLogService.GetAuditLogsAsync(
                    startDate, endDate, userId, userRole, action, entityType, entityId, status, 1, int.MaxValue);

                if (format.ToLower() == "csv")
                {
                    var csvContent = GenerateCsvContent(auditLogs);
                    var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    
                    return File(System.Text.Encoding.UTF8.GetBytes(csvContent), "text/csv", fileName);
                }
                else
                {
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(auditLogs, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    
                    return File(System.Text.Encoding.UTF8.GetBytes(jsonContent), "application/json", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs");
                return StatusCode(500, new { 
                    message = "Internal server error while exporting audit logs",
                    error = ex.Message 
                });
            }
        }

        /// <summary>
        /// Generate CSV content for audit logs export
        /// </summary>
        private string GenerateCsvContent(IEnumerable<AuditLog> auditLogs)
        {
            var csvBuilder = new System.Text.StringBuilder();
            
            // CSV header
            csvBuilder.AppendLine("ID,SessionId,UserId,UserRole,Action,EntityType,EntityId,EntityName,Status,Timestamp,Description,Notes,IpAddress,UserAgent,Endpoint,HttpMethod,HttpStatusCode,ProcessingTimeMs,ErrorDetails,CorrelationId,TransactionId,Amount,PaymentMethod,TseSignature");
            
            // CSV data rows
            foreach (var log in auditLogs)
            {
                csvBuilder.AppendLine($"\"{log.Id}\",\"{log.SessionId}\",\"{log.UserId}\",\"{log.UserRole}\",\"{log.Action}\",\"{log.EntityType}\",\"{log.EntityId}\",\"{log.EntityName}\",\"{log.Status}\",\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.Description}\",\"{log.Notes}\",\"{log.IpAddress}\",\"{log.UserAgent}\",\"{log.Endpoint}\",\"{log.HttpMethod}\",\"{log.HttpStatusCode}\",\"{log.ProcessingTimeMs}\",\"{log.ErrorDetails}\",\"{log.CorrelationId}\",\"{log.TransactionId}\",\"{log.Amount}\",\"{log.PaymentMethod}\",\"{log.TseSignature}\"");
            }
            
            return csvBuilder.ToString();
        }
    }

    // DTOs for audit log responses
    public class AuditLogsResponse
    {
        public bool Success { get; set; }
        public List<AuditLog> AuditLogs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string Message { get; set; } = string.Empty;
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
    }
}

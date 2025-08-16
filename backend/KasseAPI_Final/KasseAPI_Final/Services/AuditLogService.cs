using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.Text.Json;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Comprehensive audit logging service for tracking all payment operations and system activities
    /// This service provides detailed audit trails for compliance and security monitoring
    /// </summary>
    public interface IAuditLogService
    {
        Task<AuditLog> LogPaymentOperationAsync(string action, string entityType, Guid? entityId, 
            string userId, string userRole, decimal? amount = null, string? paymentMethod = null, 
            string? tseSignature = null, string? transactionId = null, string? correlationId = null,
            object? requestData = null, object? responseData = null, string? description = null,
            string? notes = null, AuditLogStatus status = AuditLogStatus.Success, 
            string? errorDetails = null, double? processingTimeMs = null);

        Task<AuditLog> LogEntityChangeAsync(string action, string entityType, Guid entityId, 
            string userId, string userRole, object? oldValues = null, object? newValues = null,
            string? description = null, string? notes = null, AuditLogStatus status = AuditLogStatus.Success,
            string? errorDetails = null);

        Task<AuditLog> LogSystemOperationAsync(string action, string entityType, string userId, 
            string userRole, string? description = null, string? notes = null, 
            AuditLogStatus status = AuditLogStatus.Success, string? errorDetails = null,
            object? requestData = null, object? responseData = null);

        Task<AuditLog> LogUserActivityAsync(string action, string userId, string userRole, 
            string? description = null, string? notes = null, AuditLogStatus status = AuditLogStatus.Success,
            string? errorDetails = null, object? requestData = null, object? responseData = null);

        Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null, int page = 1, int pageSize = 50);

        Task<IEnumerable<AuditLog>> GetPaymentAuditLogsAsync(Guid paymentId, DateTime? startDate = null, 
            DateTime? endDate = null, int page = 1, int pageSize = 50);

        Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, DateTime? startDate = null, 
            DateTime? endDate = null, int page = 1, int pageSize = 50);

        Task<AuditLog?> GetAuditLogByIdAsync(Guid auditLogId);

        Task<int> GetAuditLogsCountAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null);

        Task<IEnumerable<AuditLog>> GetAuditLogsByCorrelationIdAsync(string correlationId);

        Task<IEnumerable<AuditLog>> GetAuditLogsByTransactionIdAsync(string transactionId);

        Task<bool> DeleteAuditLogsOlderThanAsync(DateTime cutoffDate);

        Task<Dictionary<string, int>> GetAuditLogStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditLogService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(AppDbContext context, ILogger<AuditLogService> logger, 
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Log payment operations with comprehensive details
        /// </summary>
        public async Task<AuditLog> LogPaymentOperationAsync(string action, string entityType, Guid? entityId,
            string userId, string userRole, decimal? amount = null, string? paymentMethod = null,
            string? tseSignature = null, string? transactionId = null, string? correlationId = null,
            object? requestData = null, object? responseData = null, string? description = null,
            string? notes = null, AuditLogStatus status = AuditLogStatus.Success,
            string? errorDetails = null, double? processingTimeMs = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var sessionId = Guid.NewGuid().ToString();

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    UserRole = userRole,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValues = null, // Payment operations typically don't have old values
                    NewValues = responseData != null ? JsonSerializer.Serialize(responseData) : null,
                    RequestData = requestData != null ? JsonSerializer.Serialize(requestData) : null,
                    ResponseData = responseData != null ? JsonSerializer.Serialize(responseData) : null,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = description ?? $"Payment operation: {action} on {entityType}",
                    Notes = notes,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgent(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = processingTimeMs,
                    ErrorDetails = errorDetails,
                    CorrelationId = correlationId,
                    TransactionId = transactionId,
                    Amount = amount,
                    PaymentMethod = paymentMethod,
                    TseSignature = tseSignature
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment audit log created: {Action} on {EntityType} by user {UserId}", 
                    action, entityType, userId);

                return auditLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment audit log for action {Action} by user {UserId}", 
                    action, userId);
                throw;
            }
        }

        /// <summary>
        /// Log entity changes (create, update, delete operations)
        /// </summary>
        public async Task<AuditLog> LogEntityChangeAsync(string action, string entityType, Guid entityId,
            string userId, string userRole, object? oldValues = null, object? newValues = null,
            string? description = null, string? notes = null, AuditLogStatus status = AuditLogStatus.Success,
            string? errorDetails = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var sessionId = Guid.NewGuid().ToString();

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    UserRole = userRole,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                    RequestData = null,
                    ResponseData = null,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = description ?? $"Entity change: {action} on {entityType}",
                    Notes = notes,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgent(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = errorDetails,
                    CorrelationId = null,
                    TransactionId = null,
                    Amount = null,
                    PaymentMethod = null,
                    TseSignature = null
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Entity change audit log created: {Action} on {EntityType} {EntityId} by user {UserId}", 
                    action, entityType, entityId, userId);

                return auditLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create entity change audit log for action {Action} on {EntityType} {EntityId} by user {UserId}", 
                    action, entityType, entityId, userId);
                throw;
            }
        }

        /// <summary>
        /// Log system operations (configuration changes, maintenance, etc.)
        /// </summary>
        public async Task<AuditLog> LogSystemOperationAsync(string action, string entityType, string userId,
            string userRole, string? description = null, string? notes = null,
            AuditLogStatus status = AuditLogStatus.Success, string? errorDetails = null,
            object? requestData = null, object? responseData = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var sessionId = Guid.NewGuid().ToString();

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    UserRole = userRole,
                    Action = action,
                    EntityType = entityType,
                    EntityId = null,
                    OldValues = null,
                    NewValues = null,
                    RequestData = requestData != null ? JsonSerializer.Serialize(requestData) : null,
                    ResponseData = responseData != null ? JsonSerializer.Serialize(responseData) : null,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = description ?? $"System operation: {action} on {entityType}",
                    Notes = notes,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgent(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = errorDetails,
                    CorrelationId = null,
                    TransactionId = null,
                    Amount = null,
                    PaymentMethod = null,
                    TseSignature = null
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("System operation audit log created: {Action} on {EntityType} by user {UserId}", 
                    action, entityType, userId);

                return auditLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create system operation audit log for action {Action} on {EntityType} by user {UserId}", 
                    action, entityType, userId);
                throw;
            }
        }

        /// <summary>
        /// Log user activities (login, logout, role changes, etc.)
        /// </summary>
        public async Task<AuditLog> LogUserActivityAsync(string action, string userId, string userRole,
            string? description = null, string? notes = null, AuditLogStatus status = AuditLogStatus.Success,
            string? errorDetails = null, object? requestData = null, object? responseData = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var sessionId = Guid.NewGuid().ToString();

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    UserRole = userRole,
                    Action = action,
                    EntityType = AuditLogEntityTypes.USER,
                    EntityId = null,
                    OldValues = null,
                    NewValues = null,
                    RequestData = requestData != null ? JsonSerializer.Serialize(requestData) : null,
                    ResponseData = responseData != null ? JsonSerializer.Serialize(responseData) : null,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = description ?? $"User activity: {action}",
                    Notes = notes,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgent(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = errorDetails,
                    CorrelationId = null,
                    TransactionId = null,
                    Amount = null,
                    PaymentMethod = null,
                    TseSignature = null
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User activity audit log created: {Action} by user {UserId}", 
                    action, userId);

                return auditLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user activity audit log for action {Action} by user {UserId}", 
                    action, userId);
                throw;
            }
        }

        /// <summary>
        /// Get audit logs with filtering and pagination
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                // Apply filters
                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(a => a.UserId == userId);

                if (!string.IsNullOrEmpty(userRole))
                    query = query.Where(a => a.UserRole == userRole);

                if (!string.IsNullOrEmpty(action))
                    query = query.Where(a => a.Action == action);

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(a => a.EntityType == entityType);

                if (entityId.HasValue)
                    query = query.Where(a => a.EntityId == entityId.Value);

                if (status.HasValue)
                    query = query.Where(a => a.Status == status.Value);

                // Order by timestamp descending (newest first)
                query = query.OrderByDescending(a => a.Timestamp);

                // Apply pagination
                var skip = (page - 1) * pageSize;
                var auditLogs = await query.Skip(skip).Take(pageSize).ToListAsync();

                _logger.LogInformation("Retrieved {Count} audit logs with filters", auditLogs.Count);

                return auditLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs");
                throw;
            }
        }

        /// <summary>
        /// Get audit logs for a specific payment
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetPaymentAuditLogsAsync(Guid paymentId, DateTime? startDate = null,
            DateTime? endDate = null, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs
                    .Where(a => a.EntityType == AuditLogEntityTypes.PAYMENT && a.EntityId == paymentId);

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                query = query.OrderByDescending(a => a.Timestamp);

                var skip = (page - 1) * pageSize;
                var auditLogs = await query.Skip(skip).Take(pageSize).ToListAsync();

                _logger.LogInformation("Retrieved {Count} payment audit logs for payment {PaymentId}", 
                    auditLogs.Count, paymentId);

                return auditLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve payment audit logs for payment {PaymentId}", paymentId);
                throw;
            }
        }

        /// <summary>
        /// Get audit logs for a specific user
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, DateTime? startDate = null,
            DateTime? endDate = null, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs.Where(a => a.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                query = query.OrderByDescending(a => a.Timestamp);

                var skip = (page - 1) * pageSize;
                var auditLogs = await query.Skip(skip).Take(pageSize).ToListAsync();

                _logger.LogInformation("Retrieved {Count} user audit logs for user {UserId}", 
                    auditLogs.Count, userId);

                return auditLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user audit logs for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Get audit log by ID
        /// </summary>
        public async Task<AuditLog?> GetAuditLogByIdAsync(Guid auditLogId)
        {
            try
            {
                var auditLog = await _context.AuditLogs.FindAsync(auditLogId);
                return auditLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit log {AuditLogId}", auditLogId);
                throw;
            }
        }

        /// <summary>
        /// Get count of audit logs with filters
        /// </summary>
        public async Task<int> GetAuditLogsCountAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null)
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                // Apply filters
                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(a => a.UserId == userId);

                if (!string.IsNullOrEmpty(userRole))
                    query = query.Where(a => a.UserRole == userRole);

                if (!string.IsNullOrEmpty(action))
                    query = query.Where(a => a.Action == action);

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(a => a.EntityType == entityType);

                if (entityId.HasValue)
                    query = query.Where(a => a.EntityId == entityId.Value);

                if (status.HasValue)
                    query = query.Where(a => a.Status == status.Value);

                var count = await query.CountAsync();

                _logger.LogInformation("Retrieved audit log count: {Count}", count);

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit log count");
                throw;
            }
        }

        /// <summary>
        /// Get audit logs by correlation ID
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetAuditLogsByCorrelationIdAsync(string correlationId)
        {
            try
            {
                var auditLogs = await _context.AuditLogs
                    .Where(a => a.CorrelationId == correlationId)
                    .OrderBy(a => a.Timestamp)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} audit logs for correlation ID {CorrelationId}", 
                    auditLogs.Count, correlationId);

                return auditLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs for correlation ID {CorrelationId}", correlationId);
                throw;
            }
        }

        /// <summary>
        /// Get audit logs by transaction ID
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetAuditLogsByTransactionIdAsync(string transactionId)
        {
            try
            {
                var auditLogs = await _context.AuditLogs
                    .Where(a => a.TransactionId == transactionId)
                    .OrderBy(a => a.Timestamp)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} audit logs for transaction ID {TransactionId}", 
                    auditLogs.Count, transactionId);

                return auditLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs for transaction ID {TransactionId}", transactionId);
                throw;
            }
        }

        /// <summary>
        /// Delete audit logs older than specified date
        /// </summary>
        public async Task<bool> DeleteAuditLogsOlderThanAsync(DateTime cutoffDate)
        {
            try
            {
                var oldLogs = await _context.AuditLogs
                    .Where(a => a.Timestamp < cutoffDate)
                    .ToListAsync();

                if (oldLogs.Any())
                {
                    _context.AuditLogs.RemoveRange(oldLogs);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Deleted {Count} audit logs older than {CutoffDate}", 
                        oldLogs.Count, cutoffDate);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete audit logs older than {CutoffDate}", cutoffDate);
                throw;
            }
        }

        /// <summary>
        /// Get audit log statistics
        /// </summary>
        public async Task<Dictionary<string, int>> GetAuditLogStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                var statistics = new Dictionary<string, int>();

                // Count by action
                var actionStats = await query
                    .GroupBy(a => a.Action)
                    .Select(g => new { Action = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var stat in actionStats)
                {
                    statistics[$"Action_{stat.Action}"] = stat.Count;
                }

                // Count by status
                var statusStats = await query
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var stat in statusStats)
                {
                    statistics[$"Status_{stat.Status}"] = stat.Count;
                }

                // Count by entity type
                var entityTypeStats = await query
                    .GroupBy(a => a.EntityType)
                    .Select(g => new { EntityType = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var stat in entityTypeStats)
                {
                    statistics[$"EntityType_{stat.EntityType}"] = stat.Count;
                }

                // Total count
                statistics["Total"] = await query.CountAsync();

                _logger.LogInformation("Retrieved audit log statistics: {Statistics}", 
                    string.Join(", ", statistics.Select(kvp => $"{kvp.Key}: {kvp.Value}")));

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit log statistics");
                throw;
            }
        }

        // Helper methods
        private string GetClientIpAddress(HttpContext? httpContext)
        {
            try
            {
                if (httpContext == null) return "Unknown";

                var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeader))
                {
                    return forwardedHeader.Split(',')[0].Trim();
                }

                var remoteIp = httpContext.Connection.RemoteIpAddress;
                return remoteIp?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetUserAgent(HttpContext? httpContext)
        {
            try
            {
                return httpContext?.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}

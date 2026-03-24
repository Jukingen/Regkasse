using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Time;
using System.Security.Claims;
using System.Text.Json;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Audit logging service – audit logs are an immutable event stream.
    /// Invariants: (1) Append-only – only inserts; (2) No record may be modified; (3) Every event has actor, timestamp, actionType;
    /// (4) Sensitive data must never be logged (use UserAuditDiffHelper whitelist for diff data).
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
            object? requestData = null, object? responseData = null, string? correlationIdOverride = null);

        Task<AuditLog> LogUserActivityAsync(string action, string userId, string userRole, 
            string? description = null, string? notes = null, AuditLogStatus status = AuditLogStatus.Success,
            string? errorDetails = null, object? requestData = null, object? responseData = null);

        /// <summary>
        /// User lifecycle audit (centralized). Every event has actor, target, timestamp, actionType.
        /// USER_UPDATED must include structured changes; USER_ROLE_CHANGED must include role diff. Only changed values are logged.
        /// </summary>
        Task<AuditLog> LogUserLifecycleAsync(AuditEventType actionType, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null);

        /// <summary>Legacy overload: maps action string to AuditEventType and delegates. Prefer LogUserLifecycleAsync(AuditEventType, ...).</summary>
        Task<AuditLog> LogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null);

        Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null, int page = 1, int pageSize = 50);

        Task<IEnumerable<AuditLog>> GetPaymentAuditLogsAsync(Guid paymentId, DateTime? startDate = null, 
            DateTime? endDate = null, int page = 1, int pageSize = 50);

        Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, DateTime? startDate = null, 
            DateTime? endDate = null, int page = 1, int pageSize = 50);

        /// <summary>Count of user lifecycle events where the user is the target (EntityName).</summary>
        Task<int> GetUserLifecycleAuditLogsCountAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);

        Task<AuditLog?> GetAuditLogByIdAsync(Guid auditLogId);

        Task<int> GetAuditLogsCountAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null);

        Task<IEnumerable<AuditLog>> GetAuditLogsByCorrelationIdAsync(string correlationId);

        Task<IEnumerable<AuditLog>> GetAuditLogsByTransactionIdAsync(string transactionId);

        /// <summary>Sprint 5: Returns result with DeletedCount, SkippedDueToLegalHoldCount; Success false when retention or validation fails.</summary>
        Task<AuditLogCleanupResult> DeleteAuditLogsOlderThanAsync(DateTime cutoffDate);

        Task<Dictionary<string, int>> GetAuditLogStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>Incident playbook: high-risk admin/user-lifecycle actions (deactivate, reactivate, password reset, role change, create).</summary>
        Task<IEnumerable<AuditLog>> GetSuspiciousAdminActionsAsync(DateTime? since = null, int limit = 100);
    }

    /// <summary>Sprint 5: Result of audit cleanup; includes counts and retention/hold enforcement.</summary>
    public class AuditLogCleanupResult
    {
        public bool Success { get; set; }
        public int DeletedCount { get; set; }
        public int SkippedDueToLegalHoldCount { get; set; }
        public DateTime? MinCutoffDate { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditLogService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActorDisplayNameResolver _actorDisplayNameResolver;
        private readonly AuditRetentionOptions _retentionOptions;

        public AuditLogService(AppDbContext context, ILogger<AuditLogService> logger,
            IHttpContextAccessor httpContextAccessor, IActorDisplayNameResolver actorDisplayNameResolver,
            IOptions<AuditRetentionOptions> retentionOptions)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _actorDisplayNameResolver = actorDisplayNameResolver;
            _retentionOptions = retentionOptions?.Value ?? new AuditRetentionOptions();
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
                    UserAgent = GetUserAgentMinimized(httpContext),
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
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = errorDetails,
                    CorrelationId = GetRequestCorrelationId(),
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
            object? requestData = null, object? responseData = null, string? correlationIdOverride = null)
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
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = errorDetails,
                    CorrelationId = correlationIdOverride ?? GetRequestCorrelationId(),
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
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = httpContext?.Request.Path,
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = errorDetails,
                    CorrelationId = GetRequestCorrelationId(),
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
        /// User lifecycle audit (centralized). Invariant 3: every event includes actor (actorUserId), target (targetUserId), timestamp (UtcNow), actionType.
        /// USER_UPDATED must include structured changes; USER_ROLE_CHANGED must include role diff. Unchanged values are not logged.
        /// Invariant 4: callers must pass only whitelisted diff data (UserAuditDiffHelper); sensitive data must never be logged.
        /// </summary>
        public async Task<AuditLog> LogUserLifecycleAsync(AuditEventType actionType, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null)
        {
            var action = GetActionString(actionType);
            return await LogUserLifecycleAsyncCore(actionType, action, actorUserId, actorRole, targetUserId, reason, correlationId, status, description, oldValues, newValues);
        }

        /// <summary>Legacy overload: maps action string to AuditEventType and delegates.</summary>
        public async Task<AuditLog> LogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null)
        {
            var actionType = MapActionToEventType(action);
            return await LogUserLifecycleAsyncCore(actionType, action, actorUserId, actorRole, targetUserId, reason, correlationId, status, description, oldValues, newValues);
        }

        private async Task<AuditLog> LogUserLifecycleAsyncCore(AuditEventType actionType, string action,
            string actorUserId, string actorRole, string targetUserId, string? reason, string? correlationId,
            AuditLogStatus status, string? description, object? oldValues, object? newValues)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var sessionId = Guid.NewGuid().ToString();
            var requestData = new { targetUserId, reason };

            string? actorDisplayName = null;
            try
            {
                var names = await _actorDisplayNameResolver.ResolveAsync(new List<string> { actorUserId });
                names.TryGetValue(actorUserId, out actorDisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve actor display name for {UserId}", actorUserId);
            }

            // Structured diff: only changed fields (whitelist). Do not log unchanged values.
            var changeList = UserAuditDiffHelper.BuildStructuredChanges(oldValues, newValues);
            var changesJson = changeList.Count > 0 ? JsonSerializer.Serialize(changeList) : null;

            var metadata = new Dictionary<string, object?>
            {
                ["targetUserId"] = targetUserId
            };
            if (!string.IsNullOrEmpty(reason))
                metadata["reason"] = reason;
            var metadataJson = JsonSerializer.Serialize(metadata);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = actorUserId,
                UserRole = actorRole,
                Action = action,
                EntityType = AuditLogEntityTypes.USER,
                EntityId = null,
                EntityName = targetUserId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                RequestData = JsonSerializer.Serialize(requestData),
                ResponseData = null,
                Status = status,
                Timestamp = DateTime.UtcNow,
                Description = description ?? $"User lifecycle: {action} on user {targetUserId}",
                Notes = reason,
                IpAddress = GetClientIpAddress(httpContext),
                UserAgent = GetUserAgentMinimized(httpContext),
                Endpoint = httpContext?.Request.Path,
                HttpMethod = httpContext?.Request.Method,
                HttpStatusCode = httpContext?.Response.StatusCode,
                CorrelationId = correlationId ?? GetRequestCorrelationId() ?? Guid.NewGuid().ToString(),
                ActorDisplayName = actorDisplayName,
                Changes = changesJson,
                Metadata = metadataJson,
                ActionType = actionType
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User lifecycle audit: {ActionType} on user {TargetUserId} by {ActorUserId}",
                actionType, targetUserId, actorUserId);

            return auditLog;
        }

        /// <summary>Maps AuditEventType to legacy Action string for backward compatibility (existing queries/reports).</summary>
        private static string GetActionString(AuditEventType actionType)
        {
            return actionType switch
            {
                AuditEventType.UserCreated => AuditLogActions.USER_CREATE,
                AuditEventType.UserUpdated => AuditLogActions.USER_UPDATE,
                AuditEventType.UserRoleChanged => AuditLogActions.USER_ROLE_CHANGE,
                AuditEventType.UserDeactivated => AuditLogActions.USER_DEACTIVATE,
                AuditEventType.UserReactivated => AuditLogActions.USER_REACTIVATE,
                AuditEventType.PasswordResetForced => AuditLogActions.FORCE_RESET_PASSWORD,
                AuditEventType.ChangeOwnPassword => AuditLogActions.CHANGE_OWN_PASSWORD,
                AuditEventType.UserPasswordReset => AuditLogActions.USER_PASSWORD_RESET,
                AuditEventType.RolePermissionsUpdated => AuditLogActions.ROLE_PERMISSIONS_UPDATE,
                AuditEventType.RoleDeleted => AuditLogActions.ROLE_DELETE,
                AuditEventType.LoginSuccess => AuditLogActions.USER_LOGIN,
                AuditEventType.LoginFailed => AuditLogActions.USER_LOGIN,
                AuditEventType.UserLogout => AuditLogActions.USER_LOGOUT,
                AuditEventType.UserDeleted => AuditLogActions.USER_DELETE,
                _ => AuditLogActions.USER_UPDATE
            };
        }

        /// <summary>Maps legacy action string to AuditEventType. Used when reading old logs or from legacy callers.</summary>
        private static AuditEventType MapActionToEventType(string action)
        {
            if (string.IsNullOrEmpty(action)) return AuditEventType.Other;
            return action switch
            {
                AuditLogActions.USER_CREATE => AuditEventType.UserCreated,
                AuditLogActions.USER_UPDATE => AuditEventType.UserUpdated,
                AuditLogActions.USER_ROLE_CHANGE => AuditEventType.UserRoleChanged,
                AuditLogActions.USER_DEACTIVATE => AuditEventType.UserDeactivated,
                AuditLogActions.USER_REACTIVATE => AuditEventType.UserReactivated,
                AuditLogActions.FORCE_RESET_PASSWORD => AuditEventType.PasswordResetForced,
                AuditLogActions.CHANGE_OWN_PASSWORD => AuditEventType.ChangeOwnPassword,
                AuditLogActions.USER_PASSWORD_RESET => AuditEventType.UserPasswordReset,
                AuditLogActions.ROLE_PERMISSIONS_UPDATE => AuditEventType.RolePermissionsUpdated,
                AuditLogActions.ROLE_DELETE => AuditEventType.RoleDeleted,
                AuditLogActions.USER_LOGIN => AuditEventType.LoginSuccess,
                AuditLogActions.USER_LOGOUT => AuditEventType.UserLogout,
                AuditLogActions.USER_DELETE => AuditEventType.UserDeleted,
                _ => AuditEventType.Other
            };
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

                // Austria calendar-day half-open bounds on audit instants (see PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds).
                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lo.HasValue)
                    query = query.Where(a => a.Timestamp >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(a => a.Timestamp < hi.Value);

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

                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lo.HasValue)
                    query = query.Where(a => a.Timestamp >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(a => a.Timestamp < hi.Value);

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
        /// Get user lifecycle audit logs where the given user is the target (events on this user: create, deactivate, reactivate, role change, password reset).
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, DateTime? startDate = null,
            DateTime? endDate = null, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs
                    .Where(a => a.EntityType == AuditLogEntityTypes.USER && a.EntityName == userId);

                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lo.HasValue)
                    query = query.Where(a => a.Timestamp >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(a => a.Timestamp < hi.Value);

                query = query.OrderByDescending(a => a.Timestamp);

                var skip = (page - 1) * pageSize;
                var auditLogs = await query.Skip(skip).Take(pageSize).ToListAsync();

                _logger.LogInformation("Retrieved {Count} user lifecycle audit logs for user {UserId}", 
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
        /// Count of user lifecycle events where the user is the target (EntityName).
        /// </summary>
        public async Task<int> GetUserLifecycleAuditLogsCountAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.AuditLogs
                    .Where(a => a.EntityType == AuditLogEntityTypes.USER && a.EntityName == userId);
                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lo.HasValue)
                    query = query.Where(a => a.Timestamp >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(a => a.Timestamp < hi.Value);
                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user lifecycle audit log count for user {UserId}", userId);
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

                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lo.HasValue)
                    query = query.Where(a => a.Timestamp >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(a => a.Timestamp < hi.Value);

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
        /// Sprint 5: Delete audit logs older than specified date. Enforces 7-year minimum retention and skips records under legal hold.
        /// </summary>
        public async Task<AuditLogCleanupResult> DeleteAuditLogsOlderThanAsync(DateTime cutoffDate)
        {
            try
            {
                // Retention cutoff: interpret cutoffDate in Austria wall-clock then UTC (not calendar half-open listing).
                var cutoffUtc = PostgreSqlUtcDateTime.ToUtcForNpgsql(cutoffDate);
                var retentionYears = _retentionOptions.RetentionYears > 0 ? _retentionOptions.RetentionYears : 7;
                var minCutoff = DateTime.UtcNow.Date.AddYears(-retentionYears);
                if (cutoffUtc > minCutoff)
                {
                    _logger.LogWarning("Audit cleanup rejected: cutoff {CutoffDate} is within retention window. Min cutoff is {MinCutoff} (retention {Years} years).",
                        cutoffDate, minCutoff, retentionYears);
                    return new AuditLogCleanupResult
                    {
                        Success = false,
                        DeletedCount = 0,
                        SkippedDueToLegalHoldCount = 0,
                        MinCutoffDate = minCutoff,
                        ErrorMessage = $"Cutoff date must be on or before {minCutoff:yyyy-MM-dd} to comply with {retentionYears}-year audit retention. No records were deleted."
                    };
                }

                var candidateLogs = await _context.AuditLogs
                    .Where(a => a.Timestamp < cutoffUtc)
                    .ToListAsync();

                var activeHolds = await _context.LegalHolds
                    .Where(h => h.IsActive)
                    .ToListAsync();

                var toDelete = candidateLogs
                    .Where(a => !activeHolds.Any(h => a.Timestamp.Date >= h.FromDate && a.Timestamp.Date <= h.ToDate))
                    .ToList();
                var skippedCount = candidateLogs.Count - toDelete.Count;

                if (toDelete.Any())
                {
                    _context.AuditLogs.RemoveRange(toDelete);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Audit cleanup: deleted {DeletedCount} logs older than {CutoffDate}; skipped {SkippedCount} due to legal hold.",
                        toDelete.Count, cutoffDate, skippedCount);
                }

                return new AuditLogCleanupResult
                {
                    Success = true,
                    DeletedCount = toDelete.Count,
                    SkippedDueToLegalHoldCount = skippedCount,
                    MinCutoffDate = minCutoff
                };
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

                var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
                if (lo.HasValue)
                    query = query.Where(a => a.Timestamp >= lo.Value);
                if (hi.HasValue)
                    query = query.Where(a => a.Timestamp < hi.Value);

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

        /// <summary>
        /// Incident playbook: returns high-risk admin/user-lifecycle actions for security review (deactivate, reactivate, password reset, role change, user create).
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetSuspiciousAdminActionsAsync(DateTime? since = null, int limit = 100)
        {
            try
            {
                var highRiskActions = new[]
                {
                    AuditLogActions.USER_DEACTIVATE,
                    AuditLogActions.USER_REACTIVATE,
                    AuditLogActions.USER_PASSWORD_RESET,
                    AuditLogActions.FORCE_RESET_PASSWORD,
                    AuditLogActions.CHANGE_OWN_PASSWORD,
                    AuditLogActions.USER_ROLE_CHANGE,
                    AuditLogActions.USER_CREATE
                };

                var query = _context.AuditLogs
                    .Where(a => a.EntityType == AuditLogEntityTypes.USER && highRiskActions.Contains(a.Action));

                if (since.HasValue)
                {
                    var sinceUtc = PostgreSqlUtcDateTime.ToUtcForNpgsql(since.Value);
                    query = query.Where(a => a.Timestamp >= sinceUtc);
                }

                var list = await query
                    .OrderByDescending(a => a.Timestamp)
                    .Take(Math.Min(limit, 500))
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} suspicious admin actions since {Since}", list.Count, since);
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve suspicious admin actions");
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

        /// <summary>Privacy minimization: store truncated User-Agent (max 200 chars) for audit; avoid full fingerprint.</summary>
        private string GetUserAgentMinimized(HttpContext? httpContext)
        {
            const int maxLength = 200;
            var raw = GetUserAgent(httpContext);
            if (string.IsNullOrEmpty(raw) || raw == "Unknown") return raw ?? "Unknown";
            return raw.Length <= maxLength ? raw : raw.Substring(0, maxLength);
        }

        /// <summary>CorrelationId from current request (set by CorrelationIdMiddleware).</summary>
        private string? GetRequestCorrelationId()
        {
            try
            {
                return _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
            }
            catch
            {
                return null;
            }
        }
    }
}

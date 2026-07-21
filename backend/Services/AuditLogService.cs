using KasseAPI_Final.Data;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
            object? requestData = null, object? responseData = null, string? correlationIdOverride = null,
            ImpersonationAuditContext.Snapshot? impersonationSnapshot = null,
            AuditEventType? actionType = null, Guid? entityId = null, Guid? tenantId = null);

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
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null);

        Task<AuditLog> LogUserLifecycleAsync(AuditEventType actionType, string actorUserId, string actorRole,
            string targetUserId, Guid? tenantId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null);

        /// <summary>Legacy overload: maps action string to AuditEventType and delegates. Prefer LogUserLifecycleAsync(AuditEventType, ...).</summary>
        Task<AuditLog> LogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null);

        Task<AuditLog> LogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, Guid? tenantId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null);

        Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null, int page = 1, int pageSize = 50,
            string? targetUserId = null, string? ipAddress = null, string? statusOutcome = null, bool? hasChanges = null);

        /// <summary>Keyset-paginated audit log list (preferred for admin UI).</summary>
        Task<(IReadOnlyList<AuditLog> Items, KeysetPageMetaDto Meta)> GetAuditLogsPagedAsync(
            AuditLogQueryFilters filters,
            int pageSize,
            string? afterCursor = null,
            int page = 1,
            bool includeTotalCount = false);

        Task<IEnumerable<AuditLog>> GetPaymentAuditLogsAsync(Guid paymentId, DateTime? startDate = null,
            DateTime? endDate = null, int page = 1, int pageSize = 50);

        Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, DateTime? startDate = null,
            DateTime? endDate = null, int page = 1, int pageSize = 50);

        /// <summary>Count of user lifecycle events where the user is the target (EntityName).</summary>
        Task<int> GetUserLifecycleAuditLogsCountAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);

        Task<AuditLog?> GetAuditLogByIdAsync(Guid auditLogId);

        Task<int> GetAuditLogsCountAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null,
            string? targetUserId = null, string? ipAddress = null, string? statusOutcome = null, bool? hasChanges = null);

        Task<IEnumerable<AuditLog>> GetAuditLogsByCorrelationIdAsync(string correlationId);

        Task<IEnumerable<AuditLog>> GetAuditLogsByTransactionIdAsync(string transactionId);

        /// <summary>Sprint 5: Returns result with DeletedCount, SkippedDueToLegalHoldCount; Success false when retention or validation fails.</summary>
        Task<AuditLogCleanupResult> DeleteAuditLogsOlderThanAsync(DateTime cutoffDate);

        Task<Dictionary<string, int>> GetAuditLogStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>Incident playbook: high-risk admin/user-lifecycle actions (deactivate, reactivate, password reset, role change, create).</summary>
        Task<IEnumerable<AuditLog>> GetSuspiciousAdminActionsAsync(DateTime? since = null, int limit = 100);

        /// <summary>Records Super Admin impersonation session start (admin host; before tenant JWT is used).</summary>
        Task<AuditLog> LogImpersonationSessionStartedAsync(
            string superAdminUserId,
            string superAdminRole,
            Guid impersonatedTenantId,
            string? tenantSlug = null,
            string? correlationId = null);
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
        private static readonly Guid SystemTenantId = LegacyDefaultTenantIds.Primary;

        private readonly AppDbContext _context;
        private readonly ILogger<AuditLogService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICurrentTenantAccessor _tenantAccessor;
        private readonly IActorDisplayNameResolver _actorDisplayNameResolver;
        private readonly AuditRetentionOptions _retentionOptions;

        public AuditLogService(AppDbContext context, ILogger<AuditLogService> logger,
            IHttpContextAccessor httpContextAccessor, ICurrentTenantAccessor tenantAccessor,
            IActorDisplayNameResolver actorDisplayNameResolver,
            IOptions<AuditRetentionOptions> retentionOptions)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _tenantAccessor = tenantAccessor;
            _actorDisplayNameResolver = actorDisplayNameResolver;
            _retentionOptions = retentionOptions?.Value ?? new AuditRetentionOptions();
        }

        /// <summary>Read-only audit log queries — append-only stream is never mutated on read paths.</summary>
        private IQueryable<AuditLog> AuditLogsReadOnly => _context.AuditLogs.AsNoTracking();

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

                var actionCol = AuditLogPersistenceSanitizer.TruncateForAction(action);
                var entityTypeCol = AuditLogPersistenceSanitizer.TruncateForEntityType(entityType);
                var userRoleCol = AuditLogPersistenceSanitizer.TruncateForUserRole(userRole);
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = ResolveAuditTenantId(null),
                    SessionId = sessionId,
                    UserId = AuditLogPersistenceSanitizer.TruncateUserId(userId),
                    UserRole = userRoleCol,
                    Action = actionCol,
                    EntityType = entityTypeCol,
                    EntityId = entityId,
                    OldValues = null, // Payment operations typically don't have old values
                    NewValues = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(responseData),
                    RequestData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(requestData),
                    ResponseData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(responseData),
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = AuditLogPersistenceSanitizer.Truncate(
                        description ?? $"Payment operation: {actionCol} on {entityTypeCol}",
                        AuditLogPersistenceSanitizer.DescriptionMaxLength),
                    Notes = AuditLogPersistenceSanitizer.Truncate(notes, AuditLogPersistenceSanitizer.NotesMaxLength),
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = AuditLogPersistenceSanitizer.TruncateEndpoint(httpContext),
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = processingTimeMs,
                    ErrorDetails = AuditLogPersistenceSanitizer.Truncate(errorDetails, AuditLogPersistenceSanitizer.ErrorDetailsMaxLength),
                    CorrelationId = AuditLogPersistenceSanitizer.Truncate(correlationId, AuditLogPersistenceSanitizer.CorrelationIdMaxLength),
                    TransactionId = AuditLogPersistenceSanitizer.Truncate(transactionId, AuditLogPersistenceSanitizer.TransactionIdMaxLength),
                    Amount = amount,
                    PaymentMethod = AuditLogPersistenceSanitizer.Truncate(paymentMethod, 50),
                    TseSignature = AuditLogPersistenceSanitizer.Truncate(tseSignature, 500)
                };

                ApplyImpersonationContext(auditLog);
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

                var actionCol = AuditLogPersistenceSanitizer.TruncateForAction(action);
                var entityTypeCol = AuditLogPersistenceSanitizer.TruncateForEntityType(entityType);
                var userRoleCol = AuditLogPersistenceSanitizer.TruncateForUserRole(userRole);
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = ResolveAuditTenantId(null),
                    SessionId = sessionId,
                    UserId = AuditLogPersistenceSanitizer.TruncateUserId(userId),
                    UserRole = userRoleCol,
                    Action = actionCol,
                    EntityType = entityTypeCol,
                    EntityId = entityId,
                    ActionType = MapActionToEventTypeOrNull(actionCol),
                    OldValues = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(oldValues),
                    NewValues = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(newValues),
                    RequestData = null,
                    ResponseData = null,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = AuditLogPersistenceSanitizer.Truncate(
                        description ?? $"Entity change: {actionCol} on {entityTypeCol}",
                        AuditLogPersistenceSanitizer.DescriptionMaxLength),
                    Notes = AuditLogPersistenceSanitizer.Truncate(notes, AuditLogPersistenceSanitizer.NotesMaxLength),
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = AuditLogPersistenceSanitizer.TruncateEndpoint(httpContext),
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = AuditLogPersistenceSanitizer.Truncate(errorDetails, AuditLogPersistenceSanitizer.ErrorDetailsMaxLength),
                    CorrelationId = AuditLogPersistenceSanitizer.Truncate(GetRequestCorrelationId(), AuditLogPersistenceSanitizer.CorrelationIdMaxLength),
                    TransactionId = null,
                    Amount = null,
                    PaymentMethod = null,
                    TseSignature = null
                };

                ApplyImpersonationContext(auditLog);
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
            object? requestData = null, object? responseData = null, string? correlationIdOverride = null,
            ImpersonationAuditContext.Snapshot? impersonationSnapshot = null,
            AuditEventType? actionType = null, Guid? entityId = null, Guid? tenantId = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var sessionId = Guid.NewGuid().ToString();

                var actionCol = AuditLogPersistenceSanitizer.TruncateForAction(action);
                var entityTypeCol = AuditLogPersistenceSanitizer.TruncateForEntityType(entityType);
                var userRoleCol = AuditLogPersistenceSanitizer.TruncateForUserRole(userRole);
                var resolvedActionType = actionType ?? MapActionToEventType(actionCol);
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = AuditLogPersistenceSanitizer.TruncateUserId(userId),
                    UserRole = userRoleCol,
                    Action = actionCol,
                    EntityType = entityTypeCol,
                    EntityId = entityId,
                    ActionType = resolvedActionType == AuditEventType.Other ? null : resolvedActionType,
                    TenantId = ResolveAuditTenantId(tenantId),
                    OldValues = null,
                    NewValues = null,
                    RequestData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(requestData),
                    ResponseData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(responseData),
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = AuditLogPersistenceSanitizer.Truncate(
                        description ?? $"System operation: {actionCol} on {entityTypeCol}",
                        AuditLogPersistenceSanitizer.DescriptionMaxLength),
                    Notes = AuditLogPersistenceSanitizer.Truncate(notes, AuditLogPersistenceSanitizer.NotesMaxLength),
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = AuditLogPersistenceSanitizer.TruncateEndpoint(httpContext),
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = AuditLogPersistenceSanitizer.Truncate(errorDetails, AuditLogPersistenceSanitizer.ErrorDetailsMaxLength),
                    CorrelationId = AuditLogPersistenceSanitizer.Truncate(
                        correlationIdOverride ?? GetRequestCorrelationId(),
                        AuditLogPersistenceSanitizer.CorrelationIdMaxLength),
                    TransactionId = null,
                    Amount = null,
                    PaymentMethod = null,
                    TseSignature = null
                };

                ApplyImpersonationContext(auditLog, impersonationSnapshot);
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

                var actionCol = AuditLogPersistenceSanitizer.TruncateForAction(action);
                var userRoleCol = AuditLogPersistenceSanitizer.TruncateForUserRole(userRole);
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = ResolveAuditTenantId(null),
                    SessionId = sessionId,
                    UserId = AuditLogPersistenceSanitizer.TruncateUserId(userId),
                    UserRole = userRoleCol,
                    Action = actionCol,
                    EntityType = AuditLogPersistenceSanitizer.TruncateForEntityType(AuditLogEntityTypes.USER),
                    EntityId = null,
                    ActionType = MapActionToEventTypeOrNull(actionCol),
                    OldValues = null,
                    NewValues = null,
                    RequestData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(requestData),
                    ResponseData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(responseData),
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Description = AuditLogPersistenceSanitizer.Truncate(
                        description ?? $"User activity: {actionCol}",
                        AuditLogPersistenceSanitizer.DescriptionMaxLength),
                    Notes = AuditLogPersistenceSanitizer.Truncate(notes, AuditLogPersistenceSanitizer.NotesMaxLength),
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgentMinimized(httpContext),
                    Endpoint = AuditLogPersistenceSanitizer.TruncateEndpoint(httpContext),
                    HttpMethod = httpContext?.Request.Method,
                    HttpStatusCode = httpContext?.Response.StatusCode,
                    ProcessingTimeMs = null,
                    ErrorDetails = AuditLogPersistenceSanitizer.Truncate(errorDetails, AuditLogPersistenceSanitizer.ErrorDetailsMaxLength),
                    CorrelationId = AuditLogPersistenceSanitizer.Truncate(GetRequestCorrelationId(), AuditLogPersistenceSanitizer.CorrelationIdMaxLength),
                    TransactionId = null,
                    Amount = null,
                    PaymentMethod = null,
                    TseSignature = null
                };

                ApplyImpersonationContext(auditLog);
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
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null)
        {
            var action = GetActionString(actionType);
            return await LogUserLifecycleAsyncCore(actionType, action, actorUserId, actorRole, targetUserId, null, reason, correlationId, status, description, oldValues, newValues, userCreatedDetails);
        }

        public async Task<AuditLog> LogUserLifecycleAsync(AuditEventType actionType, string actorUserId, string actorRole,
            string targetUserId, Guid? tenantId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null)
        {
            var action = GetActionString(actionType);
            return await LogUserLifecycleAsyncCore(actionType, action, actorUserId, actorRole, targetUserId, tenantId, reason, correlationId, status, description, oldValues, newValues, userCreatedDetails);
        }

        /// <summary>Legacy overload: maps action string to AuditEventType and delegates.</summary>
        public async Task<AuditLog> LogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null)
        {
            var actionType = MapActionToEventType(action);
            return await LogUserLifecycleAsyncCore(actionType, action, actorUserId, actorRole, targetUserId, null, reason, correlationId, status, description, oldValues, newValues, userCreatedDetails);
        }

        public async Task<AuditLog> LogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, Guid? tenantId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null,
            UserCreatedAuditDetails? userCreatedDetails = null)
        {
            var actionType = MapActionToEventType(action);
            return await LogUserLifecycleAsyncCore(actionType, action, actorUserId, actorRole, targetUserId, tenantId, reason, correlationId, status, description, oldValues, newValues, userCreatedDetails);
        }

        private async Task<AuditLog> LogUserLifecycleAsyncCore(AuditEventType actionType, string action,
            string actorUserId, string actorRole, string targetUserId, Guid? tenantId, string? reason, string? correlationId,
            AuditLogStatus status, string? description, object? oldValues, object? newValues,
            UserCreatedAuditDetails? userCreatedDetails)
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
            var changesJson = changeList.Count > 0
                ? AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(changeList)
                : null;

            var metadata = new Dictionary<string, object?>
            {
                ["targetUserId"] = targetUserId
            };
            if (!string.IsNullOrEmpty(reason))
                metadata["reason"] = reason;
            if (userCreatedDetails != null)
            {
                metadata["createdByUserId"] = userCreatedDetails.CreatedByUserId;
                metadata["role"] = userCreatedDetails.Role;
                metadata["passwordReturned"] = userCreatedDetails.PasswordReturned;
                if (userCreatedDetails.TenantId is Guid tid)
                    metadata["tenantId"] = tid;
            }

            var metadataJson = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(metadata);

            var actionCol = AuditLogPersistenceSanitizer.TruncateForAction(action);
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = ResolveAuditTenantId(tenantId),
                SessionId = sessionId,
                UserId = AuditLogPersistenceSanitizer.TruncateUserId(actorUserId),
                UserRole = AuditLogPersistenceSanitizer.TruncateForUserRole(actorRole),
                Action = actionCol,
                EntityType = AuditLogPersistenceSanitizer.TruncateForEntityType(AuditLogEntityTypes.USER),
                EntityId = null,
                EntityName = AuditLogPersistenceSanitizer.Truncate(targetUserId, AuditLogPersistenceSanitizer.EntityNameMaxLength),
                OldValues = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(oldValues),
                NewValues = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(newValues),
                RequestData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(requestData),
                ResponseData = null,
                Status = status,
                Timestamp = DateTime.UtcNow,
                Description = AuditLogPersistenceSanitizer.Truncate(
                    description ?? $"User lifecycle: {actionCol} on user {targetUserId}",
                    AuditLogPersistenceSanitizer.DescriptionMaxLength),
                Notes = AuditLogPersistenceSanitizer.Truncate(reason, AuditLogPersistenceSanitizer.NotesMaxLength),
                IpAddress = GetClientIpAddress(httpContext),
                UserAgent = GetUserAgentMinimized(httpContext),
                Endpoint = AuditLogPersistenceSanitizer.TruncateEndpoint(httpContext),
                HttpMethod = httpContext?.Request.Method,
                HttpStatusCode = httpContext?.Response.StatusCode,
                CorrelationId = AuditLogPersistenceSanitizer.Truncate(
                    correlationId ?? GetRequestCorrelationId() ?? Guid.NewGuid().ToString(),
                    AuditLogPersistenceSanitizer.CorrelationIdMaxLength),
                ActorDisplayName = AuditLogPersistenceSanitizer.Truncate(actorDisplayName, 200),
                Changes = changesJson,
                Metadata = metadataJson,
                ActionType = actionType
            };

            ApplyImpersonationContext(auditLog);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User lifecycle audit: {ActionType} on user {TargetUserId} by {ActorUserId}",
                actionType, targetUserId, actorUserId);

            return auditLog;
        }

        public async Task<AuditLog> LogImpersonationSessionStartedAsync(
            string superAdminUserId,
            string superAdminRole,
            Guid impersonatedTenantId,
            string? tenantSlug = null,
            string? correlationId = null)
        {
            var slugNote = string.IsNullOrWhiteSpace(tenantSlug) ? null : $"slug={tenantSlug.Trim()}";
            var description = $"Super Admin impersonation session started for tenant {impersonatedTenantId:D}";
            return await LogSystemOperationAsync(
                AuditLogActions.TENANT_IMPERSONATION_STARTED,
                AuditLogEntityTypes.SYSTEM_CONFIG,
                superAdminUserId,
                superAdminRole,
                description: description,
                notes: slugNote,
                status: AuditLogStatus.Success,
                requestData: new { impersonatedTenantId, tenantSlug },
                correlationIdOverride: correlationId,
                impersonationSnapshot: ImpersonationAuditContext.ForSessionStart(superAdminUserId, impersonatedTenantId))
                .ConfigureAwait(false);
        }

        /// <summary>Maps AuditEventType to legacy Action string for backward compatibility (existing queries/reports).</summary>
        private static string GetActionString(AuditEventType actionType)
        {
            return actionType switch
            {
                AuditEventType.UserCreated => AuditLogActions.USER_CREATED,
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
                AuditEventType.LoginFailed => AuditLogActions.USER_LOGIN_FAILED,
                AuditEventType.UserLogout => AuditLogActions.USER_LOGOUT,
                AuditEventType.UserDeleted => AuditLogActions.USER_DELETE,
                AuditEventType.UserTenantMembershipChanged => AuditLogActions.USER_TENANT_MEMBERSHIP_CHANGED,
                AuditEventType.UserNameChanged => AuditLogActions.USER_NAME_CHANGE,
                AuditEventType.RestoreRequested => AuditLogActions.RESTORE_REQUESTED,
                AuditEventType.RestoreApproved => AuditLogActions.RESTORE_APPROVED,
                AuditEventType.RestoreRejected => AuditLogActions.RESTORE_REJECTED,
                AuditEventType.RestoreCompleted => AuditLogActions.RESTORE_COMPLETED,
                AuditEventType.RestoreFailed => AuditLogActions.RESTORE_FAILED,
                AuditEventType.CategoryUpdated => AuditLogActions.CATEGORY_UPDATED,
                AuditEventType.CategoryDemoReset => AuditLogActions.CATEGORY_DEMO_RESET,
                AuditEventType.LicenseRenewed => AuditLogActions.LICENSE_RENEWED,
                AuditEventType.LicenseExtended => AuditLogActions.LICENSE_EXTENDED,
                AuditEventType.LicenseUpdated => AuditLogActions.LICENSE_UPDATED,
                AuditEventType.InvoiceResent => AuditLogActions.INVOICE_RESENT,
                AuditEventType.UserPermissionOverridesChanged => AuditLogActions.USER_PERMISSION_OVERRIDES_CHANGED,
                AuditEventType.ReportPdfDownloaded => AuditLogActions.REPORT_PDF_DOWNLOADED,
                _ => AuditLogActions.USER_UPDATE
            };
        }

        private static AuditEventType? MapActionToEventTypeOrNull(string action)
        {
            var mapped = MapActionToEventType(action);
            return mapped == AuditEventType.Other ? null : mapped;
        }

        /// <summary>Maps legacy action string to AuditEventType. Used when reading old logs or from legacy callers.</summary>
        private static AuditEventType MapActionToEventType(string action)
        {
            if (string.IsNullOrEmpty(action))
                return AuditEventType.Other;
            return action switch
            {
                AuditLogActions.USER_CREATE => AuditEventType.UserCreated,
                AuditLogActions.USER_CREATED => AuditEventType.UserCreated,
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
                AuditLogActions.USER_LOGIN_FAILED => AuditEventType.LoginFailed,
                AuditLogActions.USER_LOGOUT => AuditEventType.UserLogout,
                AuditLogActions.USER_DELETE => AuditEventType.UserDeleted,
                AuditLogActions.USER_TENANT_MEMBERSHIP_CHANGED => AuditEventType.UserTenantMembershipChanged,
                AuditLogActions.USER_NAME_CHANGE => AuditEventType.UserNameChanged,
                AuditLogActions.RESTORE_REQUESTED => AuditEventType.RestoreRequested,
                AuditLogActions.RESTORE_APPROVED => AuditEventType.RestoreApproved,
                AuditLogActions.RESTORE_REJECTED => AuditEventType.RestoreRejected,
                AuditLogActions.RESTORE_COMPLETED => AuditEventType.RestoreCompleted,
                AuditLogActions.RESTORE_FAILED => AuditEventType.RestoreFailed,
                AuditLogActions.CATEGORY_UPDATED => AuditEventType.CategoryUpdated,
                AuditLogActions.CATEGORY_DEMO_RESET => AuditEventType.CategoryDemoReset,
                AuditLogActions.LICENSE_RENEWED => AuditEventType.LicenseRenewed,
                AuditLogActions.LICENSE_EXTENDED => AuditEventType.LicenseExtended,
                AuditLogActions.LICENSE_UPDATED => AuditEventType.LicenseUpdated,
                AuditLogActions.INVOICE_RESENT => AuditEventType.InvoiceResent,
                AuditLogActions.USER_PERMISSION_OVERRIDES_CHANGED => AuditEventType.UserPermissionOverridesChanged,
                AuditLogActions.REPORT_PDF_DOWNLOADED => AuditEventType.ReportPdfDownloaded,
                AuditLogActions.MANUAL_RESTORE_REQUEST_CREATED => AuditEventType.RestoreRequested,
                AuditLogActions.MANUAL_RESTORE_REQUEST_APPROVED => AuditEventType.RestoreApproved,
                AuditLogActions.MANUAL_RESTORE_REQUEST_REJECTED => AuditEventType.RestoreRejected,
                _ => AuditEventType.Other
            };
        }

        /// <summary>
        /// Get audit logs with filtering and pagination
        /// </summary>
        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? userId = null, string? userRole = null, string? action = null, string? entityType = null,
            Guid? entityId = null, AuditLogStatus? status = null, int page = 1, int pageSize = 50,
            string? targetUserId = null, string? ipAddress = null, string? statusOutcome = null, bool? hasChanges = null)
        {
            var filters = AuditLogQueryExtensions.ToFilters(
                startDate, endDate, userId, userRole, targetUserId, action, entityType, entityId,
                ipAddress, status, statusOutcome, hasChanges);
            var (items, _) = await GetAuditLogsPagedAsync(filters, pageSize, page: page, includeTotalCount: false);
            return items;
        }

        public async Task<(IReadOnlyList<AuditLog> Items, KeysetPageMetaDto Meta)> GetAuditLogsPagedAsync(
            AuditLogQueryFilters filters,
            int pageSize,
            string? afterCursor = null,
            int page = 1,
            bool includeTotalCount = false)
        {
            try
            {
                pageSize = Math.Clamp(pageSize, 1, 100);
                page = Math.Max(1, page);

                var query = AuditLogsReadOnly.ApplyFilters(filters)
                    .OrderByDescending(a => a.Timestamp)
                    .ThenByDescending(a => a.Id);

                int? total = null;
                if (includeTotalCount)
                    total = await AuditLogsReadOnly.ApplyFilters(filters).CountAsync();

                IQueryable<AuditLog> pageQuery = query;
                if (KeysetCursor.TryDecode(afterCursor, out var cursor))
                {
                    pageQuery = query.ApplyKeysetAfterDesc(cursor, a => a.Timestamp, a => a.Id);
                }
                else if (page > 1)
                {
                    pageQuery = query.Skip((page - 1) * pageSize);
                }

                var rows = await pageQuery.Take(pageSize + 1).ToListAsync();
                var hasMore = rows.Count > pageSize;
                if (hasMore)
                    rows = rows.Take(pageSize).ToList();

                string? nextCursor = null;
                if (hasMore && rows.Count > 0)
                {
                    var last = rows[^1];
                    nextCursor = new KeysetCursor(last.Timestamp, last.Id).Encode();
                }

                _logger.LogInformation("Retrieved {Count} audit logs (keyset hasMore={HasMore})", rows.Count, hasMore);

                return (rows, new KeysetPageMetaDto
                {
                    NextCursor = nextCursor,
                    HasMore = hasMore,
                    TotalCount = total,
                    PageSize = pageSize,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs (paged)");
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
                var query = AuditLogsReadOnly
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
                var query = AuditLogsReadOnly
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
                var query = AuditLogsReadOnly
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
                return await AuditLogsReadOnly
                    .FirstOrDefaultAsync(a => a.Id == auditLogId);
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
            Guid? entityId = null, AuditLogStatus? status = null,
            string? targetUserId = null, string? ipAddress = null, string? statusOutcome = null, bool? hasChanges = null)
        {
            try
            {
                var filters = AuditLogQueryExtensions.ToFilters(
                    startDate, endDate, userId, userRole, targetUserId, action, entityType, entityId,
                    ipAddress, status, statusOutcome, hasChanges);
                var count = await AuditLogsReadOnly.ApplyFilters(filters).CountAsync();

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
                var auditLogs = await AuditLogsReadOnly
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
                var auditLogs = await AuditLogsReadOnly
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
                var query = AuditLogsReadOnly;

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

                statistics["Total"] = actionStats.Sum(s => s.Count);

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
                    AuditLogActions.USER_CREATE,
                    AuditLogActions.TENANT_QUICK_USER_CREATED
                };

                var query = AuditLogsReadOnly
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
                if (httpContext == null)
                    return "Unknown";

                string? raw = null;
                var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeader))
                    raw = forwardedHeader.Split(',')[0].Trim();
                else
                    raw = httpContext.Connection.RemoteIpAddress?.ToString();

                if (string.IsNullOrEmpty(raw))
                    return "Unknown";
                return AuditLogPersistenceSanitizer.Truncate(raw, AuditLogPersistenceSanitizer.IpAddressMaxLength) ?? raw;
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
            if (string.IsNullOrEmpty(raw) || raw == "Unknown")
                return raw ?? "Unknown";
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

        private void ApplyImpersonationContext(
            AuditLog auditLog,
            ImpersonationAuditContext.Snapshot? explicitSnapshot = null)
        {
            var snapshot = explicitSnapshot
                ?? ImpersonationAuditContext.FromHttpContext(_httpContextAccessor.HttpContext, _tenantAccessor);
            ImpersonationAuditContext.ApplyTo(auditLog, snapshot);
        }

        private Guid ResolveAuditTenantId(Guid? explicitTenantId)
        {
            if (explicitTenantId is Guid tenantId && tenantId != Guid.Empty)
                return tenantId;

            return _tenantAccessor.TenantId is Guid ambientTenantId && ambientTenantId != Guid.Empty
                ? ambientTenantId
                : SystemTenantId;
        }
    }
}

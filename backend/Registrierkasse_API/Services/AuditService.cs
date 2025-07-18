using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string action, string entityType, string? entityId = null, 
            object? oldValues = null, object? newValues = null, string? description = null);
        Task LogLoginAsync(string userId, string userName, string userRole, bool success, string? errorMessage = null);
        Task LogSecurityEventAsync(string action, string description, string? userId = null, bool isSuccess = true);
        Task<List<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null, 
            string? userId = null, string? action = null, string? entityType = null, int page = 1, int pageSize = 50);
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AppDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogActionAsync(string action, string entityType, string? entityId = null, 
            object? oldValues = null, object? newValues = null, string? description = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var userId = httpContext?.User?.FindFirst("sub")?.Value ?? "SYSTEM";
                var userName = httpContext?.User?.FindFirst("name")?.Value ?? "System";
                var userRole = httpContext?.User?.FindFirst("role")?.Value ?? "System";

                var auditLog = new AuditLog
                {
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    UserId = userId,
                    UserName = userName,
                    UserRole = userRole,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                    Description = description,
                    Status = "SUCCESS",
                    AdditionalData = JsonSerializer.Serialize(new
                    {
                        RequestPath = httpContext?.Request.Path,
                        RequestMethod = httpContext?.Request.Method,
                        Timestamp = DateTime.UtcNow
                    })
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Audit log created: {Action} on {EntityType} by {UserId}", 
                    action, entityType, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create audit log for action: {Action}", action);
            }
        }

        public async Task LogLoginAsync(string userId, string userName, string userRole, bool success, string? errorMessage = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                
                var auditLog = new AuditLog
                {
                    Action = success ? "LOGIN_SUCCESS" : "LOGIN_FAILED",
                    EntityType = "USER",
                    EntityId = userId,
                    UserId = userId,
                    UserName = userName,
                    UserRole = userRole,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                    Description = success ? "User login successful" : "User login failed",
                    Status = success ? "SUCCESS" : "FAILED",
                    ErrorMessage = errorMessage,
                    AdditionalData = JsonSerializer.Serialize(new
                    {
                        RequestPath = httpContext?.Request.Path,
                        RequestMethod = httpContext?.Request.Method,
                        LoginTime = DateTime.UtcNow
                    })
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Login audit log created: {UserId} - {Success}", userId, success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create login audit log for user: {UserId}", userId);
            }
        }

        public async Task LogSecurityEventAsync(string action, string description, string? userId = null, bool isSuccess = true)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var currentUserId = userId ?? httpContext?.User?.FindFirst("sub")?.Value ?? "SYSTEM";
                var userName = httpContext?.User?.FindFirst("name")?.Value ?? "System";
                var userRole = httpContext?.User?.FindFirst("role")?.Value ?? "System";

                var auditLog = new AuditLog
                {
                    Action = action,
                    EntityType = "SECURITY",
                    UserId = currentUserId,
                    UserName = userName,
                    UserRole = userRole,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                    Description = description,
                    Status = isSuccess ? "SUCCESS" : "FAILED",
                    AdditionalData = JsonSerializer.Serialize(new
                    {
                        RequestPath = httpContext?.Request.Path,
                        RequestMethod = httpContext?.Request.Method,
                        SecurityEventTime = DateTime.UtcNow
                    })
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogWarning("Security audit log created: {Action} - {Description}", action, description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create security audit log for action: {Action}", action);
            }
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null, 
            string? userId = null, string? action = null, string? entityType = null, int page = 1, int pageSize = 50)
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(a => a.CreatedAt >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.CreatedAt <= endDate.Value);

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(a => a.UserId == userId);

                if (!string.IsNullOrEmpty(action))
                    query = query.Where(a => a.Action == action);

                if (!string.IsNullOrEmpty(entityType))
                    query = query.Where(a => a.EntityType == entityType);

                var auditLogs = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return auditLogs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve audit logs");
                return new List<AuditLog>();
            }
        }

        private string? GetClientIpAddress(HttpContext? httpContext)
        {
            if (httpContext == null) return null;

            // X-Forwarded-For header'ı kontrol et (proxy arkasında)
            var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedHeader))
            {
                return forwardedHeader.Split(',')[0].Trim();
            }

            // X-Real-IP header'ı kontrol et
            var realIpHeader = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIpHeader))
            {
                return realIpHeader;
            }

            // Remote IP address
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
} 

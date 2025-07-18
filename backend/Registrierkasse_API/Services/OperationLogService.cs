using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Security.Claims;

namespace Registrierkasse_API.Services
{
    /// <summary>
    /// Kullanıcı operasyonlarını loglamak için servis
    /// </summary>
    public class OperationLogService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<OperationLogService> _logger;

        public OperationLogService(
            AppDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<OperationLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcı operasyonunu loglar
        /// </summary>
        public async Task LogOperationAsync(string operation, string details, string? userId = null)
        {
            try
            {
                // Kullanıcı ID'sini al
                var currentUserId = userId ?? GetCurrentUserId();

                var operationLog = new OperationLog
                {
                    UserId = currentUserId ?? string.Empty,
                    Operation = operation,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = GetClientIpAddress() ?? string.Empty,
                    UserAgent = GetUserAgent() ?? string.Empty
                };

                _context.OperationLogs.Add(operationLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Operation logged: {Operation} by user {UserId}", operation, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log operation: {Operation}", operation);
            }
        }

        /// <summary>
        /// Belirli bir kullanıcının operasyon loglarını getirir
        /// </summary>
        public async Task<List<OperationLog>> GetUserOperationLogsAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            IQueryable<OperationLog> query = _context.OperationLogs
                .Where(log => log.UserId == userId);

            if (fromDate.HasValue)
                query = query.Where(log => log.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.Timestamp <= toDate.Value);

            return await query.OrderByDescending(log => log.Timestamp).ToListAsync();
        }

        /// <summary>
        /// Tüm operasyon loglarını getirir (Admin için)
        /// </summary>
        public async Task<List<OperationLog>> GetAllOperationLogsAsync(DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50)
        {
            IQueryable<OperationLog> query = _context.OperationLogs
                .Include(log => log.User);

            if (fromDate.HasValue)
                query = query.Where(log => log.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.Timestamp <= toDate.Value);

            return await query
                .OrderByDescending(log => log.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Belirli bir operasyon türünün loglarını getirir
        /// </summary>
        public async Task<List<OperationLog>> GetOperationLogsByTypeAsync(string operation, DateTime? fromDate = null, DateTime? toDate = null)
        {
            IQueryable<OperationLog> query = _context.OperationLogs
                .Include(log => log.User)
                .Where(log => log.Operation == operation);

            if (fromDate.HasValue)
                query = query.Where(log => log.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.Timestamp <= toDate.Value);

            return await query.OrderByDescending(log => log.Timestamp).ToListAsync();
        }

        /// <summary>
        /// Eski logları temizler (7 yıldan eski)
        /// </summary>
        public async Task CleanupOldLogsAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddYears(-7);
                var oldLogs = await _context.OperationLogs
                    .Where(log => log.Timestamp < cutoffDate)
                    .ToListAsync();

                _context.OperationLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old operation logs", oldLogs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old operation logs");
            }
        }

        /// <summary>
        /// Mevcut kullanıcının ID'sini alır
        /// </summary>
        private string? GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// Client IP adresini alır
        /// </summary>
        private string? GetClientIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // X-Forwarded-For header'ını kontrol et (proxy arkasında)
            var forwardedHeader = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedHeader))
            {
                return forwardedHeader.Split(',')[0].Trim();
            }

            // Remote IP adresini al
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// User Agent bilgisini alır
        /// </summary>
        private string? GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
        }
    }
} 
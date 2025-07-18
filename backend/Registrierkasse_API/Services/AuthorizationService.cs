using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Security.Claims;
using System.Text.Json;

namespace Registrierkasse_API.Services
{
    /// <summary>
    /// Gelişmiş yetki kontrol servisi - Rol tabanlı erişim kontrolü ve loglama
    /// </summary>
    public class AuthorizationService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuthorizationService> _logger;

        public AuthorizationService(AppDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuthorizationService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcının belirli bir kaynak üzerinde belirli bir işlem yapma yetkisi var mı kontrol eder
        /// </summary>
        public async Task<bool> HasPermissionAsync(string userId, string resource, string action)
        {
            try
            {
                var userRoles = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .ToListAsync();

                foreach (var userRole in userRoles)
                {
                    if (!userRole.Role.IsActive) continue;

                    var hasPermission = userRole.Role.RolePermissions
                        .Any(rp => rp.IsActive && 
                                  rp.Permission.IsActive &&
                                  rp.Permission.Resource == resource &&
                                  rp.Permission.Action == action);

                    if (hasPermission) return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking permission for user {userId}, resource {resource}, action {action}");
                return false;
            }
        }

        /// <summary>
        /// Kullanıcının rolünü döndürür
        /// </summary>
        public async Task<string> GetUserRoleAsync(string userId)
        {
            var userRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .OrderByDescending(ur => ur.AssignedAt)
                .FirstOrDefaultAsync();

            return userRole?.Role?.Name ?? "Cashier"; // Varsayılan rol
        }

        /// <summary>
        /// İşlem logunu kaydeder - Before/After durumları ile
        /// </summary>
        public async Task LogOperationAsync(string operation, string entityType, string entityId, 
            object beforeState = null, object afterState = null, string summary = "")
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = httpContext?.User;
                var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

                var details = $"Entity: {entityType} (ID: {entityId})";
                if (!string.IsNullOrEmpty(summary))
                {
                    details += $" - {summary}";
                }

                var operationLog = new OperationLog
                {
                    Operation = operation,
                    Details = details,
                    UserId = userId,
                    IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown",
                    UserAgent = httpContext?.Request?.Headers["User-Agent"].FirstOrDefault() ?? "unknown",
                    Timestamp = DateTime.UtcNow
                };

                _context.OperationLogs.Add(operationLog);
                await _context.SaveChangesAsync();

                // İngilizce teknik log
                _logger.LogInformation($"Operation logged: {operation} on {entityType} {entityId} by user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging operation: {operation} on {entityType} {entityId}");
            }
        }

        /// <summary>
        /// Yetkisiz erişim denemesini loglar ve Almanca hata mesajı döndürür
        /// </summary>
        public async Task<string> HandleUnauthorizedAccessAsync(string resource, string action)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var username = user?.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            // İngilizce teknik log
            _logger.LogWarning($"Unauthorized access attempt: User {username} ({userId}) tried to {action} on {resource}");

            // Almanca kullanıcı mesajı
            return $"Sie haben keine Berechtigung für diese Aktion: {action} auf {resource}";
        }

        /// <summary>
        /// Kullanıcının tüm yetkilerini döndürür
        /// </summary>
        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var permissions = await _context.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Where(ur => ur.UserId == userId && ur.IsActive && ur.Role.IsActive)
                .SelectMany(ur => ur.Role.RolePermissions
                    .Where(rp => rp.IsActive && rp.Permission.IsActive)
                    .Select(rp => $"{rp.Permission.Resource}.{rp.Permission.Action}"))
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        /// <summary>
        /// Yeni kullanıcıya varsayılan rol atar
        /// </summary>
        public async Task AssignDefaultRoleAsync(string userId, string roleName = "Cashier")
        {
            try
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null)
                {
                    _logger.LogError($"Default role {roleName} not found");
                    return;
                }

                var userRole = new UserRole
                {
                    UserId = userId,
                    RoleId = role.Id,
                    AssignedBy = "system",
                    IsActive = true
                };

                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Default role {roleName} assigned to user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning default role to user {userId}");
            }
        }
    }
} 
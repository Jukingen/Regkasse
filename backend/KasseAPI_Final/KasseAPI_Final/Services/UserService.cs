using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Data.Repositories;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Kullanıcı işlemleri için service implementation
    /// </summary>
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(
            AppDbContext context,
            ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcıyı ID'ye göre getir
        /// </summary>
        public async Task<ApplicationUser?> GetUserByIdAsync(string? userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("GetUserByIdAsync called with null or empty userId");
                    return null;
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogDebug("User not found with ID: {UserId}", userId);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Kullanıcıyı email'e göre getir
        /// </summary>
        public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("GetUserByEmailAsync called with null or empty email");
                    return null;
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user == null)
                {
                    _logger.LogDebug("User not found with email: {Email}", email);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                return null;
            }
        }

        /// <summary>
        /// Kullanıcı rolünü kontrol et
        /// </summary>
        public async Task<bool> HasRoleAsync(string userId, string role)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var hasRole = user.Role == role;
                _logger.LogDebug("User {UserId} role check: {Role} = {HasRole}", userId, role, hasRole);

                return hasRole;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking role {Role} for user {UserId}", role, userId);
                return false;
            }
        }

        /// <summary>
        /// Kullanıcı yetkilerini kontrol et
        /// </summary>
        public async Task<bool> HasPermissionAsync(string userId, string permission)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                // Rol bazlı yetki kontrolü
                var hasPermission = user.Role switch
                {
                    "Admin" => true, // Admin tüm yetkilere sahip
                    "Cashier" => permission switch
                    {
                        "payment.create" => true,
                        "payment.cancel" => true,
                        "payment.refund" => true,
                        "payment.view" => true,
                        "customer.view" => true,
                        "product.view" => true,
                        _ => false
                    },
                    "Demo" => permission switch
                    {
                        "payment.view" => true,
                        "customer.view" => true,
                        "product.view" => true,
                        _ => false
                    },
                    _ => false
                };

                _logger.LogDebug("User {UserId} permission check: {Permission} = {HasPermission}", 
                    userId, permission, hasPermission);

                return hasPermission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
                return false;
            }
        }
    }
}

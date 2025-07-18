using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using System.Security.Claims;

namespace Registrierkasse_API.Services
{
    public class RoleService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RoleService> _logger;

        public RoleService(AppDbContext context, ILogger<RoleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Kullanıcının rollerini getir
        public async Task<List<string>> GetUserRolesAsync(string userId)
        {
            try
            {
                var roles = await _context.UserRoles
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Include(ur => ur.Role)
                    .Where(ur => ur.Role.IsActive)
                    .Select(ur => ur.Role.Name)
                    .ToListAsync();

                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting roles for user {userId}");
                return new List<string>();
            }
        }

        // Kullanıcının yetkilerini getir
        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            try
            {
                var permissions = await _context.UserRoles
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .Where(ur => ur.Role.IsActive)
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Where(rp => rp.IsActive && rp.Permission.IsActive)
                    .Select(rp => $"{rp.Permission.Resource}.{rp.Permission.Action}")
                    .Distinct()
                    .ToListAsync();

                return permissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting permissions for user {userId}");
                return new List<string>();
            }
        }

        // Kullanıcının belirli bir yetkisi var mı kontrol et
        public async Task<bool> HasPermissionAsync(string userId, string resource, string action)
        {
            try
            {
                var hasPermission = await _context.UserRoles
                    .Where(ur => ur.UserId == userId && ur.IsActive)
                    .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .Where(ur => ur.Role.IsActive)
                    .SelectMany(ur => ur.Role.RolePermissions)
                    .Where(rp => rp.IsActive && rp.Permission.IsActive)
                    .AnyAsync(rp => rp.Permission.Resource == resource && rp.Permission.Action == action);

                return hasPermission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking permission for user {userId}");
                return false;
            }
        }

        // Kullanıcıya rol ata
        public async Task<bool> AssignRoleToUserAsync(string userId, int roleId, string assignedBy)
        {
            try
            {
                // Kullanıcının bu rolü zaten var mı kontrol et
                var existingRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (existingRole != null)
                {
                    if (!existingRole.IsActive)
                    {
                        existingRole.IsActive = true;
                        existingRole.AssignedAt = DateTime.UtcNow;
                        existingRole.AssignedBy = assignedBy;
                    }
                }
                else
                {
                    var userRole = new UserRole
                    {
                        UserId = userId,
                        RoleId = roleId,
                        AssignedBy = assignedBy,
                        IsActive = true
                    };

                    _context.UserRoles.Add(userRole);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Role {roleId} assigned to user {userId} by {assignedBy}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning role {roleId} to user {userId}");
                return false;
            }
        }

        // Kullanıcıdan rol kaldır
        public async Task<bool> RemoveRoleFromUserAsync(string userId, int roleId)
        {
            try
            {
                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (userRole != null)
                {
                    userRole.IsActive = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Role {roleId} removed from user {userId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing role {roleId} from user {userId}");
                return false;
            }
        }

        // Rol oluştur
        public async Task<Role?> CreateRoleAsync(string name, string description)
        {
            try
            {
                var role = new Role
                {
                    Name = name,
                    Description = description,
                    IsActive = true
                };

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Role {name} created successfully");
                return role;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating role {name}");
                return null;
            }
        }

        // Yetki oluştur
        public async Task<Permission?> CreatePermissionAsync(string name, string description, string resource, string action)
        {
            try
            {
                var permission = new Permission
                {
                    Name = name,
                    Description = description,
                    Resource = resource,
                    Action = action,
                    IsActive = true
                };

                _context.Permissions.Add(permission);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Permission {name} created successfully");
                return permission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating permission {name}");
                return null;
            }
        }

        // Role yetki ata
        public async Task<bool> AssignPermissionToRoleAsync(int roleId, int permissionId, string grantedBy)
        {
            try
            {
                var existingPermission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

                if (existingPermission != null)
                {
                    if (!existingPermission.IsActive)
                    {
                        existingPermission.IsActive = true;
                        existingPermission.GrantedAt = DateTime.UtcNow;
                        existingPermission.GrantedBy = grantedBy;
                    }
                }
                else
                {
                    var rolePermission = new RolePermission
                    {
                        RoleId = roleId,
                        PermissionId = permissionId,
                        GrantedBy = grantedBy,
                        IsActive = true
                    };

                    _context.RolePermissions.Add(rolePermission);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Permission {permissionId} assigned to role {roleId} by {grantedBy}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning permission {permissionId} to role {roleId}");
                return false;
            }
        }

        // Tüm rolleri getir
        public async Task<List<Role>> GetAllRolesAsync()
        {
            return await _context.Roles
                .Where(r => r.IsActive)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync();
        }

        // Tüm yetkileri getir
        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _context.Permissions
                .Where(p => p.IsActive)
                .ToListAsync();
        }

        // Kullanıcının demo hesabı olup olmadığını kontrol et
        public async Task<bool> IsDemoUserAsync(string userId)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsDemo, u.AccountType })
                    .FirstOrDefaultAsync();

                return user?.IsDemo == true || user?.AccountType == "demo";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if user {userId} is demo user");
                return false;
            }
        }

        // Kullanıcı giriş istatistiklerini güncelle
        public async Task UpdateLoginStatsAsync(string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    user.LoginCount++;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating login stats for user {userId}");
            }
        }
    }
} 
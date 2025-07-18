using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Models;
using Registrierkasse_API.Services;
using System.Security.Claims;

namespace Registrierkasse_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UserManagementController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleService _roleService;
        private readonly JwtService _jwtService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            UserManager<ApplicationUser> userManager,
            RoleService roleService,
            JwtService jwtService,
            ILogger<UserManagementController> logger)
        {
            _userManager = userManager;
            _roleService = roleService;
            _jwtService = jwtService;
            _logger = logger;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var currentUser = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
                
                // Kullanıcı oluştur
                var user = new ApplicationUser
                {
                    UserName = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    EmployeeNumber = request.EmployeeNumber,
                    AccountType = request.AccountType ?? "real",
                    IsDemo = request.AccountType == "demo",
                    IsActive = true,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
                }

                // Rolleri ata (eğer belirtilmişse)
                if (request.RoleIds != null && request.RoleIds.Any())
                {
                    foreach (var roleId in request.RoleIds)
                    {
                        await _roleService.AssignRoleToUserAsync(user.Id, roleId, currentUser);
                    }
                }

                _logger.LogInformation($"User {user.UserName} created successfully by {currentUser}");

                return Ok(new
                {
                    message = "Kullanıcı başarıyla oluşturuldu",
                    userId = user.Id,
                    username = user.UserName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { error = "Kullanıcı oluşturulurken hata oluştu" });
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? accountType = null)
        {
            try
            {
                var query = _userManager.Users.AsQueryable();

                // Hesap tipine göre filtrele
                if (!string.IsNullOrEmpty(accountType))
                {
                    query = query.Where(u => u.AccountType == accountType);
                }

                var users = await query
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.EmployeeNumber,
                        u.AccountType,
                        u.IsDemo,
                        u.IsActive,
                        u.LastLoginAt,
                        u.LoginCount
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { error = "Kullanıcılar alınırken hata oluştu" });
            }
        }

        [HttpGet("users/{userId}/roles")]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            try
            {
                var roles = await _roleService.GetUserRolesAsync(userId);
                var permissions = await _roleService.GetUserPermissionsAsync(userId);

                return Ok(new
                {
                    userId,
                    roles,
                    permissions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting roles for user {userId}");
                return StatusCode(500, new { error = "Kullanıcı rolleri alınırken hata oluştu" });
            }
        }

        [HttpPost("users/{userId}/roles")]
        public async Task<IActionResult> AssignRoleToUser(string userId, [FromBody] AssignRoleRequest request)
        {
            try
            {
                var currentUser = User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
                var success = await _roleService.AssignRoleToUserAsync(userId, request.RoleId, currentUser);

                if (success)
                {
                    _logger.LogInformation($"Role {request.RoleId} assigned to user {userId} by {currentUser}");
                    return Ok(new { message = "Rol başarıyla atandı" });
                }
                else
                {
                    return BadRequest(new { error = "Rol atanamadı" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning role to user {userId}");
                return StatusCode(500, new { error = "Rol atanırken hata oluştu" });
            }
        }

        [HttpDelete("users/{userId}/roles/{roleId}")]
        public async Task<IActionResult> RemoveRoleFromUser(string userId, int roleId)
        {
            try
            {
                var success = await _roleService.RemoveRoleFromUserAsync(userId, roleId);

                if (success)
                {
                    _logger.LogInformation($"Role {roleId} removed from user {userId}");
                    return Ok(new { message = "Rol başarıyla kaldırıldı" });
                }
                else
                {
                    return BadRequest(new { error = "Rol kaldırılamadı" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing role from user {userId}");
                return StatusCode(500, new { error = "Rol kaldırılırken hata oluştu" });
            }
        }

        [HttpPut("users/{userId}/status")]
        public async Task<IActionResult> UpdateUserStatus(string userId, [FromBody] UpdateUserStatusRequest request)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { error = "Kullanıcı bulunamadı" });
                }

                user.IsActive = request.IsActive;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"User {userId} status updated to {request.IsActive}");
                    return Ok(new { message = "Kullanıcı durumu güncellendi" });
                }
                else
                {
                    return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user status for {userId}");
                return StatusCode(500, new { error = "Kullanıcı durumu güncellenirken hata oluştu" });
            }
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _roleService.GetAllRolesAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles");
                return StatusCode(500, new { error = "Roller alınırken hata oluştu" });
            }
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissions()
        {
            try
            {
                var permissions = await _roleService.GetAllPermissionsAsync();
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permissions");
                return StatusCode(500, new { error = "Yetkiler alınırken hata oluştu" });
            }
        }

        [HttpPost("check-permission")]
        public async Task<IActionResult> CheckPermission([FromBody] CheckPermissionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { error = "Kullanıcı kimliği bulunamadı" });
                }

                var hasPermission = await _roleService.HasPermissionAsync(userId, request.Resource, request.Action);
                return Ok(new { hasPermission });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission");
                return StatusCode(500, new { error = "Yetki kontrolü yapılırken hata oluştu" });
            }
        }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string? AccountType { get; set; } // "real" veya "demo"
        public List<int>? RoleIds { get; set; }
    }

    public class AssignRoleRequest
    {
        public int RoleId { get; set; }
    }

    public class UpdateUserStatusRequest
    {
        public bool IsActive { get; set; }
    }

    public class CheckPermissionRequest
    {
        public string Resource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
} 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Middleware;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserManagementController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditLogService _auditLogService;
        private readonly IUserSessionInvalidation _sessionInvalidation;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLogService,
            IUserSessionInvalidation sessionInvalidation,
            ILogger<UserManagementController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditLogService = auditLogService;
            _sessionInvalidation = sessionInvalidation;
            _logger = logger;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        private string GetCurrentUserRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

        // PUT: api/usermanagement/me/password — change own password (any authenticated user)
        [HttpPut("me/password")]
        [Authorize]
        public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var user = await _userManager.FindByIdAsync(currentUserId);
                if (user == null || !user.IsActive)
                {
                    return NotFound(new { message = "User not found" });
                }

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to change password", errors = result.Errors });
                }

                var actorRole = GetCurrentUserRole();
                await _auditLogService.LogUserActivityAsync(
                    AuditLogActions.CHANGE_OWN_PASSWORD, currentUserId, actorRole,
                    description: "User changed own password", status: AuditLogStatus.Success);

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing own password for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/usermanagement — birleşik liste: query + role + isActive + page + pageSize
        [HttpGet]
        [Authorize(Policy = "UsersView")]
        public async Task<ActionResult<UsersListResponse>> GetUsers(
            [FromQuery] string? query = null,
            [FromQuery] string? role = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var q = _userManager.Users.AsQueryable();

                if (isActive.HasValue)
                    q = q.Where(u => u.IsActive == isActive.Value);

                if (!string.IsNullOrWhiteSpace(role))
                    q = q.Where(u => u.Role == role);

                var search = (query ?? "").Trim();
                if (!string.IsNullOrEmpty(search))
                    q = q.Where(u =>
                        (u.UserName != null && u.UserName.Contains(search)) ||
                        (u.FirstName != null && u.FirstName.Contains(search)) ||
                        (u.LastName != null && u.LastName.Contains(search)) ||
                        (u.Email != null && u.Email.Contains(search)) ||
                        (u.EmployeeNumber != null && u.EmployeeNumber.Contains(search)));

                var ordered = q
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName);

                var totalCount = await ordered.CountAsync();
                var items = await ordered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserInfo
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        EmployeeNumber = u.EmployeeNumber,
                        Role = u.Role,
                        TaxNumber = u.TaxNumber,
                        IsActive = u.IsActive,
                        CreatedAt = u.CreatedAt,
                        LastLoginAt = u.LastLoginAt
                    })
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                return Ok(new UsersListResponse
                {
                    Items = items,
                    Pagination = new UsersListPagination
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount,
                        TotalPages = totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/usermanagement/{id}
        [HttpGet("{id}")]
        [Authorize(Policy = "UsersView")]
        public async Task<ActionResult<UserInfo>> GetUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var userInfo = new UserInfo
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    EmployeeNumber = user.EmployeeNumber,
                    Role = user.Role,
                    TaxNumber = user.TaxNumber,
                    Notes = user.Notes,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt
                };

                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/usermanagement
        [HttpPost]
        [Authorize(Policy = "UsersManage")]
        public async Task<ActionResult<UserInfo>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Kullanıcı adı kontrol et
                var existingUser = await _userManager.FindByNameAsync(request.UserName);
                if (existingUser != null)
                {
                    return BadRequest(new { message = "Username already exists" });
                }

                // Email kontrol et
                if (!string.IsNullOrEmpty(request.Email))
                {
                    var existingEmail = await _userManager.FindByEmailAsync(request.Email);
                    if (existingEmail != null)
                    {
                        return BadRequest(new { message = "Email already exists" });
                    }
                }

                // Employee number kontrol et
                if (!string.IsNullOrEmpty(request.EmployeeNumber))
                {
                    var existingEmployee = await _userManager.Users
                        .FirstOrDefaultAsync(u => u.EmployeeNumber == request.EmployeeNumber && u.IsActive);
                    if (existingEmployee != null)
                    {
                        return BadRequest(new { message = "Employee number already exists" });
                    }
                }

                var user = new ApplicationUser
                {
                    UserName = request.UserName,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    EmployeeNumber = request.EmployeeNumber,
                    Role = request.Role,
                    TaxNumber = request.TaxNumber,
                    Notes = request.Notes,
                    IsActive = true,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to create user", errors = result.Errors });
                }

                // Role ekle
                if (!string.IsNullOrEmpty(request.Role))
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogWarning("Failed to add role {Role} to user {UserName}", request.Role, request.UserName);
                    }
                }

                var createdUserInfo = new UserInfo
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    EmployeeNumber = user.EmployeeNumber,
                    Role = user.Role,
                    TaxNumber = user.TaxNumber,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                };

                var actorId = GetCurrentUserId();
                var actorRole = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(actorId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.USER_CREATE, actorId, actorRole, user.Id, null, null,
                        AuditLogStatus.Success, $"User created: {user.UserName}");
                }

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, createdUserInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/usermanagement/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var previousRole = user.Role;

                // Email kontrol et (kendisi hariç)
                if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
                {
                    var existingEmail = await _userManager.FindByEmailAsync(request.Email);
                    if (existingEmail != null)
                    {
                        return BadRequest(new { message = "Email already exists" });
                    }
                }

                // Employee number kontrol et (kendisi hariç)
                if (!string.IsNullOrEmpty(request.EmployeeNumber) && request.EmployeeNumber != user.EmployeeNumber)
                {
                    var existingEmployee = await _userManager.Users
                        .FirstOrDefaultAsync(u => u.EmployeeNumber == request.EmployeeNumber && u.IsActive && u.Id != id);
                    if (existingEmployee != null)
                    {
                        return BadRequest(new { message = "Employee number already exists" });
                    }
                }

                // Kullanıcı bilgilerini güncelle
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.EmployeeNumber = request.EmployeeNumber;
                user.TaxNumber = request.TaxNumber;
                user.Notes = request.Notes;
                user.UpdatedAt = DateTime.UtcNow;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to update user", errors = result.Errors });
                }

                // Role güncelle
                if (!string.IsNullOrEmpty(request.Role) && request.Role != user.Role)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    if (currentRoles.Any())
                    {
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    }
                    await _userManager.AddToRoleAsync(user, request.Role);
                    user.Role = request.Role;
                }

                await _context.SaveChangesAsync();

                var actorId = GetCurrentUserId();
                var actorRole = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(actorId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.USER_UPDATE, actorId, actorRole, id, null, null,
                        AuditLogStatus.Success, $"User updated: {user.UserName}");
                    if (!string.IsNullOrEmpty(request.Role) && request.Role != previousRole)
                    {
                        await _auditLogService.LogUserLifecycleAsync(
                            AuditLogActions.USER_ROLE_CHANGE, actorId, actorRole, id,
                            $"Role changed from {previousRole} to {request.Role}", null,
                            AuditLogStatus.Success, $"Role change: {previousRole} -> {request.Role}");
                        await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
                    }
                }

                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/usermanagement/{id}/password — admin changes another user's password with current password (rare). Prefer reset-password for force reset.
        [HttpPut("{id}/password")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> ChangePassword(string id, [FromBody] ChangePasswordRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return BadRequest(new { message = "Use PUT /api/UserManagement/me/password to change your own password" });
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null || !user.IsActive)
                {
                    return NotFound(new { message = "User not found" });
                }

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to change password", errors = result.Errors });
                }

                var actorRole = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.USER_UPDATE, currentUserId!, actorRole, id, null, null,
                        AuditLogStatus.Success, "Admin changed user password (with current password)");
                }

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/usermanagement/{id}/reset-password — force reset (admin only; no current password). Block self; only SuperAdmin can reset SuperAdmin.
        [HttpPut("{id}/reset-password")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return BadRequest(new { message = "Cannot force-reset your own password. Use PUT /api/UserManagement/me/password to change your password." });
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null || !user.IsActive)
                {
                    return NotFound(new { message = "User not found" });
                }

                var actorRole = GetCurrentUserRole();
                var targetCanonicalRole = RoleCanonicalization.GetCanonicalRole(user.Role);
                var actorCanonicalRole = RoleCanonicalization.GetCanonicalRole(actorRole);
                if (string.Equals(targetCanonicalRole, RoleCanonicalization.Canonical.SuperAdmin, StringComparison.Ordinal) &&
                    !string.Equals(actorCanonicalRole, RoleCanonicalization.Canonical.SuperAdmin, StringComparison.Ordinal))
                {
                    var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
                    return StatusCode(403, new
                    {
                        code = ApiError.ForbiddenPayload.Code,
                        reason = ApiError.ForbiddenPayload.Reason,
                        requiredPolicy = "UsersManage",
                        missingRequirement = "Role",
                        correlationId,
                    });
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to reset password", errors = result.Errors });
                }

                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.FORCE_RESET_PASSWORD, currentUserId, actorRole, id, null, null,
                        AuditLogStatus.Success, "Force password reset by administrator");
                }

                await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
                return Ok(new { message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/usermanagement/{id}/deactivate
        [HttpPut("{id}/deactivate")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> DeactivateUser(string id, [FromBody] DeactivateUserRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                {
                    return BadRequest(new { message = "Deactivation reason is required for audit compliance" });
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (!user.IsActive)
                {
                    return BadRequest(new { message = "User is already deactivated" });
                }

                var currentUserId = GetCurrentUserId();
                if (id == currentUserId)
                {
                    return BadRequest(new { message = "Cannot deactivate your own account" });
                }

                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                user.DeactivatedAt = DateTime.UtcNow;
                user.DeactivatedBy = currentUserId;
                user.DeactivationReason = request.Reason.Trim();

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to deactivate user", errors = result.Errors });
                }

                var actorRole = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.USER_DEACTIVATE, currentUserId, actorRole, id,
                        request.Reason.Trim(), null, AuditLogStatus.Success,
                        $"User deactivated: {user.UserName}. Reason: {request.Reason}");
                }

                await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
                return Ok(new { message = "User deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/usermanagement/{id}/reactivate
        [HttpPut("{id}/reactivate")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> ReactivateUser(string id, [FromBody] ReactivateUserRequest? request = null)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (user.IsActive)
                {
                    return BadRequest(new { message = "User is already active" });
                }

                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
                user.DeactivatedAt = null;
                user.DeactivatedBy = null;
                user.DeactivationReason = null;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to reactivate user", errors = result.Errors });
                }

                var currentUserId = GetCurrentUserId();
                var actorRole = GetCurrentUserRole();
                var reason = request?.Reason?.Trim();
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.USER_REACTIVATE, currentUserId, actorRole, id,
                        reason, null, AuditLogStatus.Success,
                        $"User reactivated: {user.UserName}" + (string.IsNullOrEmpty(reason) ? "" : $". Note: {reason}"));
                }

                return Ok(new { message = "User reactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reactivating user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/usermanagement/{id} (soft-delete: deactivate without reason – prefer PUT deactivate for compliance)
        [HttpDelete("{id}")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Kendini silmeye çalışıyorsa engelle
                var currentUserId = GetCurrentUserId();
                if (id == currentUserId)
                {
                    return BadRequest(new { message = "Cannot delete your own account" });
                }

                // Soft delete (no reason stored – for backward compatibility; prefer PUT deactivate with reason)
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to delete user", errors = result.Errors });
                }

                var actorRole = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await _auditLogService.LogUserLifecycleAsync(
                        AuditLogActions.USER_DEACTIVATE, currentUserId, actorRole, id,
                        "Legacy DELETE (no reason)", null, AuditLogStatus.Success,
                        $"User deactivated via DELETE: {user.UserName}");
                }

                await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/usermanagement/roles
        [HttpGet("roles")]
        [Authorize(Policy = "UsersView")]
        public async Task<ActionResult<IEnumerable<string>>> GetRoles()
        {
            try
            {
                var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/usermanagement/roles
        [HttpPost("roles")]
        [Authorize(Policy = "UsersManage")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingRole = await _roleManager.FindByNameAsync(request.Name);
                if (existingRole != null)
                {
                    return BadRequest(new { message = "Role already exists" });
                }

                var role = new IdentityRole(request.Name);
                var result = await _roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to create role", errors = result.Errors });
                }

                return Ok(new { message = "Role created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

    }

    /// <summary>Sayfalanmış kullanıcı listesi yanıtı (GET /api/UserManagement).</summary>
    public class UsersListResponse
    {
        public List<UserInfo> Items { get; set; } = new();
        public UsersListPagination Pagination { get; set; } = new();
    }

    /// <summary>Sayfa bilgisi (server-side pagination).</summary>
    public class UsersListPagination
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    // DTOs
    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string? Role { get; set; }
        public string? TaxNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class CreateUserRequest
    {
        [Required]
        [MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? EmployeeNumber { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? TaxNumber { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateUserRequest
    {
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? EmployeeNumber { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? TaxNumber { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CreateRoleRequest
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>RKSV/DSGVO: Deaktivasyon nedeni zorunlu (audit trail).</summary>
    public class DeactivateUserRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class ReactivateUserRequest
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}

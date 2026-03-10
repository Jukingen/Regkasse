using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Middleware;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
        private readonly IUserUniquenessValidationService _uniquenessValidation;
        private readonly IRoleManagementService _roleManagementService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLogService,
            IUserSessionInvalidation sessionInvalidation,
            IUserUniquenessValidationService uniquenessValidation,
            IRoleManagementService roleManagementService,
            ILogger<UserManagementController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditLogService = auditLogService;
            _sessionInvalidation = sessionInvalidation;
            _uniquenessValidation = uniquenessValidation;
            _roleManagementService = roleManagementService;
            _logger = logger;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        private string GetCurrentUserRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

        /// <summary>Returns 403 if current user is not SuperAdmin. Used for role permission update and role delete.</summary>
        private bool IsCurrentUserSuperAdmin()
        {
            var role = GetCurrentUserRole();
            return string.Equals(RoleCanonicalization.GetCanonicalRole(role), Roles.SuperAdmin, StringComparison.Ordinal);
        }

        /// <summary>
        /// Runs user lifecycle audit without failing the primary operation. If audit write fails (e.g. schema/DB),
        /// logs the error and continues so that user update/create/deactivate etc. still return success.
        /// </summary>
        private async Task TryLogUserLifecycleAsync(string action, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null)
        {
            try
            {
                await _auditLogService.LogUserLifecycleAsync(action, actorUserId, actorRole, targetUserId, reason, correlationId, status, description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "User lifecycle audit failed (primary operation succeeded). Action: {Action}, TargetUserId: {TargetUserId}, ActorUserId: {ActorUserId}",
                    action, targetUserId, actorUserId);
            }
        }

        // PUT: api/usermanagement/me/password — change own password (any authenticated user; self-service, no resource permission)
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
        [HasPermission(AppPermissions.UserView)]
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

        /// <summary>
        /// Single user detail for view/edit. Returns JSON with camelCase (e.g. firstName, lastName, role).
        /// Example response: { "id": "...", "userName": "...", "firstName": "...", "lastName": "...", "email": "...", "employeeNumber": "...", "role": "Admin", "taxNumber": "...", "notes": "...", "isActive": true, "createdAt": "...", "lastLoginAt": "..." }.
        /// Route: GET /api/UserManagement/{id}
        /// </summary>
        [HttpGet("{id}")]
        [HasPermission(AppPermissions.UserView)]
        public async Task<ActionResult<UserInfo>> GetUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var userInfo = new UserInfo
                {
                    Id = user.Id ?? string.Empty,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    EmployeeNumber = user.EmployeeNumber ?? string.Empty,
                    Role = user.Role ?? string.Empty,
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<ActionResult<UserInfo>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR" });
                }
                if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
                {
                    return BadRequest(new { message = "Employee number is required.", code = "VALIDATION_ERROR", errors = new { EmployeeNumber = new[] { "Employee number is required." } } });
                }
                if (string.IsNullOrWhiteSpace(request.Role))
                {
                    return BadRequest(new { message = "Role is required. Users must have a valid role.", code = "ROLE_REQUIRED", errors = new { Role = new[] { "Role is required." } } });
                }
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Validation failed.", code = "VALIDATION_ERROR", errors = ModelState });
                }

                var roleExists = await _roleManager.FindByNameAsync(request.Role);
                if (roleExists == null)
                {
                    return BadRequest(new { message = "The specified role does not exist.", code = "ROLE_NOT_FOUND", errors = new { Role = new[] { "Role does not exist." } } });
                }

                // Kullanıcı adı kontrol et
                var existingUser = await _userManager.FindByNameAsync(request.UserName);
                if (existingUser != null)
                {
                    return BadRequest(new { message = "Username already exists" });
                }

                // Unique fields: create has no user to exclude (excludeUserId = null).
                if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(request.Email, excludeUserId: null))
                    return BadRequest(new { message = "Email already exists" });
                if (await _uniquenessValidation.IsEmployeeNumberTakenByOtherUserAsync(request.EmployeeNumber, excludeUserId: null))
                    return BadRequest(new { message = "Employee number already exists" });
                if (await _uniquenessValidation.IsTaxNumberTakenByOtherUserAsync(request.TaxNumber, excludeUserId: null))
                    return BadRequest(new { message = "Tax number already exists" });

                var user = new ApplicationUser
                {
                    UserName = request.UserName,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    EmployeeNumber = request.EmployeeNumber.Trim(),
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

                // Role is required and already validated; add user to role
                var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
                if (!roleResult.Succeeded)
                {
                    _logger.LogWarning("Failed to add role {Role} to user {UserName}", request.Role, request.UserName);
                    return BadRequest(new { message = "Failed to assign role to user.", code = "ROLE_ASSIGN_FAILED", errors = roleResult.Errors });
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
                    await TryLogUserLifecycleAsync(
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
            try
            {
                if (request == null)
                {
                    return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR" });
                }
                if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
                {
                    return BadRequest(new { message = "Employee number is required.", code = "VALIDATION_ERROR", errors = new { EmployeeNumber = new[] { "Employee number is required." } } });
                }
                if (string.IsNullOrWhiteSpace(request.Role))
                {
                    return BadRequest(new { message = "Role is required. Users must have a valid role.", code = "ROLE_REQUIRED", errors = new { Role = new[] { "Role is required." } } });
                }
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Validation failed.", code = "VALIDATION_ERROR", errors = ModelState });
                }

                var roleExists = await _roleManager.FindByNameAsync(request.Role);
                if (roleExists == null)
                {
                    return BadRequest(new { message = "The specified role does not exist.", code = "ROLE_NOT_FOUND", errors = new { Role = new[] { "Role does not exist." } } });
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                var previousRole = user.Role;

                // Uniqueness: exclude current user by loaded entity Id (user.Id), never by route id, so self-update with same employeeNumber/email does not false-conflict.
                var (hasConflict, conflictMessage) = await _uniquenessValidation.ValidateUniquenessForUpdateAsync(
                    currentUserId: user.Id,
                    user.Email,
                    user.EmployeeNumber,
                    user.TaxNumber,
                    request.Email,
                    request.EmployeeNumber,
                    request.TaxNumber);
                if (hasConflict)
                    return BadRequest(new { message = conflictMessage });

                // Email güncelle (duplicate zaten kontrol edildi; Identity lookup için NormalizedEmail gerekli)
                if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
                {
                    user.Email = request.Email;
                    user.NormalizedEmail = _userManager.NormalizeEmail(request.Email);
                }

                // Kullanıcı bilgilerini güncelle
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.EmployeeNumber = request.EmployeeNumber.Trim();
                user.TaxNumber = request.TaxNumber;
                user.Notes = request.Notes;
                user.UpdatedAt = DateTime.UtcNow;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new { message = "Failed to update user", errors = result.Errors });
                }

                // Role is required and already validated; update if changed
                if (request.Role != user.Role)
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
                    await TryLogUserLifecycleAsync(
                        AuditLogActions.USER_UPDATE, actorId, actorRole, id, null, null,
                        AuditLogStatus.Success, $"User updated: {user.UserName}");
                    if (!string.IsNullOrEmpty(request.Role) && request.Role != previousRole)
                    {
                        await TryLogUserLifecycleAsync(
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> ChangePassword(string id, [FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
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
                    await TryLogUserLifecycleAsync(
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
            var currentUserId = GetCurrentUserId();
            if (id == currentUserId)
            {
                return BadRequest(new { message = "Cannot force-reset your own password. Use PUT /api/UserManagement/me/password to change your password.", code = "VALIDATION_ERROR", errors = new { NewPassword = new[] { "Cannot reset own password from this screen." } } });
            }

            if (request == null)
            {
                return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR", errors = new { NewPassword = new[] { "Request body is required." } } });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "New password is required.", code = "VALIDATION_ERROR", errors = new { NewPassword = new[] { "New password is required." } } });
            }

            if (request.NewPassword.Length < 8)
            {
                return BadRequest(new { message = "Password must be at least 8 characters.", code = "VALIDATION_ERROR", errors = new { NewPassword = new[] { "Password must be at least 8 characters." } } });
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    var newPasswordErrors = ModelState.TryGetValue("NewPassword", out var entry)
                        ? entry!.Errors.Select(e => e.ErrorMessage).ToArray()
                        : new[] { "Validation failed." };
                    return BadRequest(new { message = newPasswordErrors.Length > 0 ? newPasswordErrors[0] : "Validation failed.", code = "VALIDATION_ERROR", errors = new { NewPassword = newPasswordErrors } });
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null || !user.IsActive)
                {
                    return NotFound(new { message = "User not found" });
                }

                var actorRole = GetCurrentUserRole();
                var targetCanonicalRole = RoleCanonicalization.GetCanonicalRole(user.Role);
                var actorCanonicalRole = RoleCanonicalization.GetCanonicalRole(actorRole);
                if (string.Equals(targetCanonicalRole, Roles.SuperAdmin, StringComparison.Ordinal) &&
                    !string.Equals(actorCanonicalRole, Roles.SuperAdmin, StringComparison.Ordinal))
                {
                    var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
                    return StatusCode(403, new
                    {
                        code = ApiError.ForbiddenPayload.Code,
                        reason = ApiError.ForbiddenPayload.Reason,
                        requiredPolicy = "user.manage",
                        missingRequirement = "Role",
                        correlationId,
                    });
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
                if (!result.Succeeded)
                {
                    var descriptions = result.Errors.Select(e => e.Description).ToArray();
                    var firstMessage = descriptions.Length > 0 ? descriptions[0] : "Password does not meet requirements.";
                    return BadRequest(new { message = firstMessage, code = "PASSWORD_RESET_FAILED", errors = new { NewPassword = descriptions } });
                }

                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await TryLogUserLifecycleAsync(
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> DeactivateUser(string id, [FromBody] DeactivateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
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
                    await TryLogUserLifecycleAsync(
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> ReactivateUser(string id, [FromBody] ReactivateUserRequest? request = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
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
                    await TryLogUserLifecycleAsync(
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            }
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
                    await TryLogUserLifecycleAsync(
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

        // GET: api/usermanagement/roles/permissions-catalog
        [HttpGet("roles/permissions-catalog")]
        [HasPermission(AppPermissions.UserView)]
        public ActionResult<IEnumerable<PermissionCatalogItemDto>> GetPermissionsCatalog()
        {
            var items = _roleManagementService.GetPermissionsCatalog();
            return Ok(items.Select(x => new PermissionCatalogItemDto
            {
                Key = x.Key,
                Group = x.Group,
                Resource = x.Resource,
                Action = x.Action,
                Description = x.Description,
            }));
        }

        // GET: api/usermanagement/roles/with-permissions
        [HttpGet("roles/with-permissions")]
        [HasPermission(AppPermissions.UserView)]
        public async Task<ActionResult<IEnumerable<RoleWithPermissionsDto>>> GetRolesWithPermissions(CancellationToken cancellationToken)
        {
            var list = await _roleManagementService.GetRolesWithPermissionsAsync(cancellationToken);
            return Ok(list);
        }

        // PUT: api/usermanagement/roles/{roleName}/permissions — SuperAdmin only; custom roles only.
        [HttpPut("roles/{roleName}/permissions")]
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> SetRolePermissions(string roleName, [FromBody] UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
        {
            if (!IsCurrentUserSuperAdmin())
            {
                var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
                return StatusCode(403, new
                {
                    code = ApiError.ForbiddenPayload.Code,
                    reason = ApiError.ForbiddenPayload.Reason,
                    requiredPolicy = "SuperAdmin",
                    missingRequirement = "Role",
                    correlationId,
                });
            }

            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest(new { message = "Role name is required.", code = "VALIDATION_ERROR" });

            request ??= new UpdateRolePermissionsRequest();
            IReadOnlyList<string> keys = request.Permissions != null ? request.Permissions : Array.Empty<string>();

            var result = await _roleManagementService.SetRolePermissionsAsync(roleName, keys, cancellationToken);
            switch (result)
            {
                case SetRolePermissionsResult.RoleNotFound:
                    return NotFound(new { message = "Role not found", code = "ROLE_NOT_FOUND" });
                case SetRolePermissionsResult.SystemRoleNotEditable:
                    return BadRequest(new { message = "System roles cannot be edited. Permission set is defined in code.", code = "SYSTEM_ROLE_NOT_EDITABLE" });
                case SetRolePermissionsResult.InvalidPermissionKeys:
                    return BadRequest(new { message = "One or more permission keys are invalid. Use GET /roles/permissions-catalog for valid keys.", code = "VALIDATION_ERROR", errors = new { Permissions = new[] { "Invalid permission key(s)." } } });
                case SetRolePermissionsResult.Success:
                    break;
            }

            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();
            if (!string.IsNullOrEmpty(actorId))
            {
                try
                {
                    await _auditLogService.LogSystemOperationAsync(
                        AuditLogActions.ROLE_PERMISSIONS_UPDATE,
                        AuditLogEntityTypes.ROLE,
                        actorId,
                        actorRole,
                        description: $"Role permissions updated: {roleName}",
                        requestData: new { roleName, permissionCount = keys.Count });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Role lifecycle audit failed. Role: {RoleName}", roleName);
                }
            }

            return Ok(new { message = "Role permissions updated successfully" });
        }

        // DELETE: api/usermanagement/roles/{roleName} — SuperAdmin only; custom roles only; blocks when users assigned.
        [HttpDelete("roles/{roleName}")]
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> DeleteRole(string roleName, CancellationToken cancellationToken)
        {
            if (!IsCurrentUserSuperAdmin())
            {
                var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
                return StatusCode(403, new
                {
                    code = ApiError.ForbiddenPayload.Code,
                    reason = ApiError.ForbiddenPayload.Reason,
                    requiredPolicy = "SuperAdmin",
                    missingRequirement = "Role",
                    correlationId,
                });
            }

            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest(new { message = "Role name is required.", code = "VALIDATION_ERROR" });

            var result = await _roleManagementService.DeleteRoleAsync(roleName, cancellationToken);
            switch (result)
            {
                case DeleteRoleResult.RoleNotFound:
                    return NotFound(new { message = "Role not found", code = "ROLE_NOT_FOUND" });
                case DeleteRoleResult.SystemRoleNotDeletable:
                    return BadRequest(new { message = "System roles cannot be deleted.", code = "SYSTEM_ROLE_NOT_DELETABLE" });
                case DeleteRoleResult.RoleHasAssignedUsers:
                    var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
                    var userCount = usersInRole?.Count ?? 0;
                    return StatusCode(409, new
                    {
                        message = "Cannot delete role: one or more users are assigned to this role. Reassign them to another role before deleting.",
                        code = "ROLE_HAS_ASSIGNED_USERS",
                        userCount,
                    });
                case DeleteRoleResult.Success:
                    break;
            }

            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();
            if (!string.IsNullOrEmpty(actorId))
            {
                try
                {
                    await _auditLogService.LogSystemOperationAsync(
                        AuditLogActions.ROLE_DELETE,
                        AuditLogEntityTypes.ROLE,
                        actorId,
                        actorRole,
                        description: $"Role deleted: {roleName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Role lifecycle audit failed. Role: {RoleName}", roleName);
                }
            }

            return Ok(new { message = "Role deleted successfully" });
        }

        // GET: api/usermanagement/roles
        [HttpGet("roles")]
        [HasPermission(AppPermissions.UserView)]
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
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (Roles.Canonical.Contains(request.Name.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "Role name is reserved for system roles. Choose a different name for a custom role.", code = "ROLE_NAME_RESERVED", errors = new { Name = new[] { "This role name is reserved." } } });
                }

                var existingRole = await _roleManager.FindByNameAsync(request.Name);
                if (existingRole != null)
                {
                    return BadRequest(new { message = "Role already exists", code = "ROLE_ALREADY_EXISTS" });
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

    /// <summary>User DTO for list and detail. Detail (GET {id}) includes all fields required by the edit form.</summary>
    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        /// <summary>Role name for display and form select (e.g. Admin, Manager).</summary>
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
        [MinLength(8)]
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

        [Required(AllowEmptyStrings = false, ErrorMessage = "Employee number is required for audit and DB constraint compliance.")]
        [MaxLength(20)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Role is required.")]
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

        [Required(AllowEmptyStrings = false, ErrorMessage = "Employee number is required for audit and DB constraint compliance.")]
        [MaxLength(20)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Role is required. Users must have a valid role.")]
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
        [MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>Contract: JSON property must be "newPassword" (camelCase) to match OpenAPI and frontend. Min length must match Identity Password.RequiredLength (8).</summary>
    public class ResetPasswordRequest
    {
        [JsonPropertyName("newPassword")]
        [Required(ErrorMessage = "New password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
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

    /// <summary>Response item for GET /api/UserManagement/roles/permissions-catalog.</summary>
    public class PermissionCatalogItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>Request body for PUT /api/UserManagement/roles/{roleName}/permissions. Empty list allowed.</summary>
    public class UpdateRolePermissionsRequest
    {
        public List<string>? Permissions { get; set; }
    }
}

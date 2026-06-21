using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Validators;
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
        private readonly IUserRoleChangeService _userRoleChangeService;
        private readonly IUserUniquenessValidationService _uniquenessValidation;
        private readonly IRoleManagementService _roleManagementService;
        private readonly IUserPermissionOverrideService _permissionOverrideService;
        private readonly ILogger<UserManagementController> _logger;
        private readonly IUserTenantMembershipProvisioner _tenantMembershipProvisioner;
        private readonly ICurrentTenantAccessor _tenantAccessor;
        private readonly IApiMessageLocalizer _messages;
        private readonly II18nErrorService _i18nErrorService;
        private readonly PasswordErrorTranslator _passwordErrors;
        private readonly IUserUsernameHistoryService _usernameHistory;
        private readonly IUsernameChangeEmailService _usernameChangeEmail;

        public UserManagementController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLogService,
            IUserSessionInvalidation sessionInvalidation,
            IUserRoleChangeService userRoleChangeService,
            IUserUniquenessValidationService uniquenessValidation,
            IRoleManagementService roleManagementService,
            IUserPermissionOverrideService permissionOverrideService,
            ILogger<UserManagementController> logger,
            IUserTenantMembershipProvisioner tenantMembershipProvisioner,
            ICurrentTenantAccessor tenantAccessor,
            IApiMessageLocalizer messages,
            II18nErrorService i18nErrorService,
            PasswordErrorTranslator passwordErrors,
            IUserUsernameHistoryService usernameHistory,
            IUsernameChangeEmailService usernameChangeEmail)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditLogService = auditLogService;
            _sessionInvalidation = sessionInvalidation;
            _userRoleChangeService = userRoleChangeService;
            _uniquenessValidation = uniquenessValidation;
            _roleManagementService = roleManagementService;
            _permissionOverrideService = permissionOverrideService;
            _logger = logger;
            _tenantMembershipProvisioner = tenantMembershipProvisioner;
            _tenantAccessor = tenantAccessor;
            _messages = messages;
            _i18nErrorService = i18nErrorService;
            _passwordErrors = passwordErrors;
            _usernameHistory = usernameHistory;
            _usernameChangeEmail = usernameChangeEmail;
        }

        private string? GetCurrentUserId() => User.GetActorUserId();
        private string GetCurrentUserRole() => User.GetActorRole() ?? "Unknown";

        /// <summary>Returns 403 if current user is not SuperAdmin. Used for role permission update and role delete.</summary>
        private bool IsCurrentUserSuperAdmin()
        {
            var role = GetCurrentUserRole();
            return string.Equals(RoleCanonicalization.GetCanonicalRole(role), Roles.SuperAdmin, StringComparison.Ordinal);
        }

        /// <summary>
        /// Runs user lifecycle audit without failing the primary operation. Uses standardized AuditEventType.
        /// oldValues/newValues: safe field-level diff only; USER_UPDATED and USER_ROLE_CHANGED must include structured changes.
        /// </summary>
        private async Task TryLogUserLifecycleAsync(AuditEventType actionType, string actorUserId, string actorRole,
            string targetUserId, string? reason = null, string? correlationId = null,
            AuditLogStatus status = AuditLogStatus.Success, string? description = null,
            object? oldValues = null, object? newValues = null)
        {
            try
            {
                // Keep backward compatibility with legacy audit action strings.
                // Several integration tests mock the string-based overload.
                var action = actionType switch
                {
                    AuditEventType.UserCreated => AuditLogActions.USER_CREATE,
                    AuditEventType.UserUpdated => AuditLogActions.USER_UPDATE,
                    AuditEventType.UserRoleChanged => AuditLogActions.USER_ROLE_CHANGE,
                    AuditEventType.UserDeactivated => AuditLogActions.USER_DEACTIVATE,
                    AuditEventType.UserReactivated => AuditLogActions.USER_REACTIVATE,
                    AuditEventType.ChangeOwnPassword => AuditLogActions.CHANGE_OWN_PASSWORD,
                    AuditEventType.UserPasswordReset => AuditLogActions.USER_PASSWORD_RESET,
                    _ => actionType.ToString()
                };

                await _auditLogService.LogUserLifecycleAsync(
                    action,
                    actorUserId,
                    actorRole,
                    targetUserId,
                    reason,
                    correlationId,
                    status,
                    description,
                    oldValues,
                    newValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "User lifecycle audit failed (primary operation succeeded). ActionType: {ActionType}, TargetUserId: {TargetUserId}, ActorUserId: {ActorUserId}",
                    actionType, targetUserId, actorUserId);
            }
        }

        // PUT: api/usermanagement/me/password — change own password (any authenticated user; self-service, no resource permission)
        [HttpPut("me/password")]
        [Authorize]
        public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request)
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
                    return NotFound();
                }

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
                if (!isPasswordValid)
                {
                    return BadRequest(new PasswordChangeResponse
                    {
                        Success = false,
                        Message = _i18nErrorService.GetMessage("CurrentPasswordIncorrect"),
                    });
                }

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
                if (!result.Succeeded)
                {
                    var validationResponse = _passwordErrors.GetValidationResponse(result);
                    return BadRequest(new PasswordChangeResponse
                    {
                        Success = false,
                        Message = validationResponse.Message,
                        ErrorCodes = validationResponse.ErrorCodes,
                        Requirements = GetPasswordRequirements(),
                    });
                }

                user.MustChangePasswordOnNextLogin = false;
                user.UpdatedAt = DateTime.UtcNow;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Password changed but MustChangePasswordOnNextLogin clear failed for user {UserId}: {Errors}",
                        currentUserId,
                        string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                }

                var stampResult = await _userManager.UpdateSecurityStampAsync(user);
                if (!stampResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Security stamp update failed after own password change for user {UserId}: {Errors}",
                        currentUserId,
                        string.Join("; ", stampResult.Errors.Select(e => e.Description)));
                }

                await _sessionInvalidation.InvalidateSessionsForUserAsync(currentUserId);

                var actorRole = GetCurrentUserRole();
                await TryLogUserLifecycleAsync(
                    AuditEventType.ChangeOwnPassword, currentUserId, actorRole, currentUserId,
                    null, null, AuditLogStatus.Success, $"User {user.Email} changed password");

                return Ok(new PasswordChangeResponse
                {
                    Success = true,
                    Message = _i18nErrorService.GetMessage("PasswordChangedSuccess"),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing own password for user {UserId}", GetCurrentUserId());
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private static PasswordRequirements GetPasswordRequirements() => new()
        {
            MinLength = 8,
            RequireDigit = true,
            RequireLowercase = true,
            RequireUppercase = true,
            RequireNonAlphanumeric = true,
        };

        /// <summary>GET api/UserManagement/me/username-change-policy — cooldown status for self-service username change.</summary>
        [HttpGet("me/username-change-policy")]
        public async Task<ActionResult<UsernameChangePolicyDto>> GetMyUsernameChangePolicy(
            CancellationToken cancellationToken = default)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var user = await _userManager.FindByIdAsync(currentUserId).ConfigureAwait(false);
            if (user == null || !user.IsActive)
                return NotFound(new { message = "User not found" });

            var bypassRestrictions = UsernameChangeRestrictions.IsBypassedForActor(GetCurrentUserRole());
            var status = await UsernameChangeRateLimit
                .GetStatusAsync(
                    _userManager,
                    user,
                    bypassCooldown: bypassRestrictions,
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(new UsernameChangePolicyDto
            {
                CooldownDays = status.CooldownDays,
                CanChange = status.CanChange,
                RestrictionsApply = !bypassRestrictions,
                LastChangedAtUtc = status.LastChangedAtUtc,
                NextChangeAllowedAtUtc = status.NextChangeAllowedAtUtc,
            });
        }

        /// <summary>PATCH api/UserManagement/me/username — self-service login username change (audited; invalidates sessions).</summary>
        [HttpPatch("me/username")]
        public async Task<IActionResult> ChangeMyUsername(
            [FromBody] UpdateUsernameRequest? request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR" });

            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var user = await _userManager.FindByIdAsync(currentUserId).ConfigureAwait(false);
            if (user == null || !user.IsActive)
                return NotFound(new { message = "User not found" });

            var newUsername = request.NewUsername.Trim();
            var bypassRestrictions = UsernameChangeRestrictions.IsBypassedForActor(GetCurrentUserRole());

            var validationErrors = UsernameValidation.ValidateNewUsername(
                newUsername,
                bypassReservedUsername: bypassRestrictions);
            if (validationErrors != null)
                return BadRequest(new { message = "Validation failed.", code = "VALIDATION_ERROR", errors = validationErrors });

            var oldUsername = user.UserName;
            if (string.Equals(oldUsername, newUsername, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    oldUsername,
                    newUsername = user.UserName ?? newUsername,
                    message = "Username unchanged.",
                });
            }

            var rateLimitError = await UsernameChangeRateLimit
                .GetRateLimitErrorAsync(_userManager, user, bypassCooldown: bypassRestrictions, cancellationToken)
                .ConfigureAwait(false);
            if (rateLimitError != null)
                return BadRequest(new { message = rateLimitError, code = "BUSINESS_RULE" });

            var newAccountError = UsernameChangePolicy.GetNewAccountRestrictionError(user, bypassRestrictions);
            if (newAccountError != null)
                return BadRequest(new { message = newAccountError, code = "BUSINESS_RULE" });

            if (await _uniquenessValidation.IsUserNameTakenByOtherUserAsync(newUsername, user.Id).ConfigureAwait(false))
                return Conflict(new { message = UsernameConflictMessages.Detail(newUsername), code = "USERNAME_CONFLICT" });

            var setNameResult = await _userManager.SetUserNameAsync(user, newUsername).ConfigureAwait(false);
            if (!setNameResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Username update failed",
                    code = "VALIDATION_ERROR",
                    errors = setNameResult.Errors.Select(e => e.Description),
                });
            }

            user.UpdatedAt = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!updateResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Username update failed",
                    code = "VALIDATION_ERROR",
                    errors = updateResult.Errors.Select(e => e.Description),
                });
            }

            var actorRole = GetCurrentUserRole();
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            var tenantId = _tenantAccessor.TenantId;

            await _auditLogService.LogUserLifecycleAsync(
                AuditEventType.UserNameChanged,
                currentUserId,
                actorRole,
                currentUserId,
                tenantId,
                reason,
                null,
                AuditLogStatus.Success,
                description: $"Username changed from '{oldUsername}' to '{newUsername}'",
                oldValues: new { UserName = oldUsername },
                newValues: new { UserName = newUsername }).ConfigureAwait(false);

            var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
            if (!stampResult.Succeeded)
            {
                _logger.LogWarning(
                    "Security stamp update failed after self-service username change for user {UserId}",
                    currentUserId);
            }

            await _sessionInvalidation.InvalidateSessionsForUserAsync(currentUserId, cancellationToken).ConfigureAwait(false);

            await _usernameHistory.RecordChangeAsync(
                currentUserId,
                oldUsername,
                newUsername,
                currentUserId,
                reason,
                cancellationToken).ConfigureAwait(false);

            await UsernameChangeRateLimit.RecordChangeAsync(_userManager, user, cancellationToken).ConfigureAwait(false);
            await TryNotifyOwnUsernameChangedAsync(user, oldUsername, newUsername).ConfigureAwait(false);

            return Ok(new
            {
                oldUsername,
                newUsername = user.UserName ?? newUsername,
                message = "Username updated successfully. Please sign in again.",
            });
        }

        private async Task TryNotifyOwnUsernameChangedAsync(
            ApplicationUser user,
            string? oldUsername,
            string newUsername)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
                return;

            await _usernameChangeEmail.TrySendUsernameChangedAsync(
                new UsernameChangedEmailRequest(
                    user.Email.Trim(),
                    oldUsername ?? string.Empty,
                    newUsername,
                    user.Email.Trim(),
                    DateTime.UtcNow)).ConfigureAwait(false);
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

                var q = _context.Users.AsNoTracking();

                var actorCanonicalRole = RoleCanonicalization.GetCanonicalRole(GetCurrentUserRole());
                var isSuperAdmin = string.Equals(actorCanonicalRole, Roles.SuperAdmin, StringComparison.Ordinal);
                if (!isSuperAdmin && _tenantAccessor.TenantId is Guid scopedTenantId)
                {
                    q = q.Where(u =>
                        _context.UserTenantMemberships.Any(m =>
                            m.UserId == u.Id && m.TenantId == scopedTenantId && m.IsActive)
                        && u.Role != Roles.SuperAdmin);
                }

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
        /// Example response: { "id": "...", "userName": "...", "firstName": "...", "lastName": "...", "email": "...", "employeeNumber": "...", "role": "SuperAdmin", "taxNumber": "...", "notes": "...", "isActive": true, "createdAt": "...", "lastLoginAt": "..." }.
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
        [HasPermission(AppPermissions.UserCreate)]
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
                    TaxNumber = string.IsNullOrWhiteSpace(request.TaxNumber) ? null : request.TaxNumber.Trim(),
                    Notes = request.Notes ?? string.Empty,
                    IsActive = true,
                    EmailConfirmed = true
                };

                var createCt = HttpContext?.RequestAborted ?? default;
                await using var tx = await _context.Database.BeginTransactionAsync(createCt);
                try
                {
                    var result = await _userManager.CreateAsync(user, request.Password);
                    if (!result.Succeeded)
                    {
                        await tx.RollbackAsync(createCt);
                        return BadRequest(new { message = "Failed to create user", errors = result.Errors });
                    }

                    var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
                    if (!roleResult.Succeeded)
                    {
                        await tx.RollbackAsync(createCt);
                        _logger.LogWarning("Failed to add role {Role} to user {UserName}", request.Role, request.UserName);
                        return BadRequest(new { message = "Failed to assign role to user.", code = "ROLE_ASSIGN_FAILED", errors = roleResult.Errors });
                    }

                    if (!string.Equals(request.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                    {
                        await _tenantMembershipProvisioner.ProvisionActiveMembershipAsync(
                            user.Id, LegacyDefaultTenantIds.Primary, cancellationToken: createCt);
                    }

                    await tx.CommitAsync(createCt);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(createCt);
                    _logger.LogError(ex, "User create failed (rolled back): Identity or tenant membership for attempted user {UserName}", request.UserName);
                    return StatusCode(500, new { message = "User creation failed.", code = "USER_CREATE_TRANSACTION_FAILED" });
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
                        AuditEventType.UserCreated, actorId, actorRole, user.Id, null, null,
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
        [HasPermission(AppPermissions.UserEdit)]
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

                // Audit diff: only whitelisted safe fields (UserAuditDiffHelper). No credentials, no Notes/TaxNumber/EmployeeNumber.
                object? oldSnapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);

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
                user.TaxNumber = string.IsNullOrWhiteSpace(request.TaxNumber) ? null : request.TaxNumber.Trim();
                user.Notes = request.Notes;
                if (request.IsDemo.HasValue)
                    user.IsDemo = request.IsDemo.Value;

                var roleWillChange = !string.Equals(
                    request.Role.Trim(),
                    user.Role?.Trim() ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);

                if (!request.IsDemo.HasValue
                    && user.IsDemo
                    && !DemoUserHelper.IsRoleAllowedForDemo(request.Role))
                {
                    user.IsDemo = false;
                    _logger.LogInformation("IsDemo auto-cleared for user {UserId}: role {Role} is not allowed for demo", id, request.Role);
                }

                user.UpdatedAt = DateTime.UtcNow;

                if (roleWillChange)
                {
                    var actorId = GetCurrentUserId() ?? "unknown";
                    var actorRole = GetCurrentUserRole();
                    var (_, roleError) = await _userRoleChangeService.ChangeUserRoleAsync(
                        user,
                        request.Role,
                        actorId,
                        actorRole,
                        ResolveActorTenantScope(),
                        CancellationToken.None).ConfigureAwait(false);

                    if (roleError != null)
                    {
                        return BadRequest(new { message = roleError, code = "ROLE_ASSIGN_FAILED" });
                    }
                }
                else
                {
                    var result = await _userManager.UpdateAsync(user);
                    if (!result.Succeeded)
                    {
                        return BadRequest(new { message = "Failed to update user", errors = result.Errors });
                    }
                }

                await _context.SaveChangesAsync();

                var actorIdForAudit = GetCurrentUserId();
                var actorRoleForAudit = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(actorIdForAudit))
                {
                    object newSnapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);
                    await TryLogUserLifecycleAsync(
                        AuditEventType.UserUpdated, actorIdForAudit, actorRoleForAudit, id, null, null,
                        AuditLogStatus.Success, $"User updated: {user.UserName}", oldSnapshot, newSnapshot);
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
        [HasPermission(AppPermissions.UserResetPassword)]
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
                    return BadRequest(_passwordErrors.BuildPasswordValidationBadRequest(result.Errors));
                }

                var actorRole = GetCurrentUserRole();
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await TryLogUserLifecycleAsync(
                        AuditEventType.ChangeOwnPassword, currentUserId!, actorRole, id, null, null,
                        AuditLogStatus.Success, "Password changed (with current password)");
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
        [HasPermission(AppPermissions.UserResetPassword)]
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
                    var validation = _passwordErrors.GetValidationResponse(result);
                    return BadRequest(new
                    {
                        message = validation.Message,
                        code = "PASSWORD_RESET_FAILED",
                        errorCodes = validation.ErrorCodes,
                        errors = new { NewPassword = result.Errors.Select(e => _passwordErrors.TranslateError(e)).ToArray() },
                    });
                }

                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await TryLogUserLifecycleAsync(
                        AuditEventType.PasswordResetForced, currentUserId, actorRole, id, null, null,
                        AuditLogStatus.Success, "Force password reset by administrator");
                }

                await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
                return Ok(new { message = _messages.Get(ApiMessageKeys.PasswordResetSuccess) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {UserId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/usermanagement/{id}/deactivate
        [HttpPut("{id}/deactivate")]
        [HasPermission(AppPermissions.UserEdit)]
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
                        AuditEventType.UserDeactivated, currentUserId, actorRole, id,
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
        [HasPermission(AppPermissions.UserEdit)]
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
                user.LockoutEnd = null;

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
                        AuditEventType.UserReactivated, currentUserId, actorRole, id,
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
        [HasPermission(AppPermissions.UserDelete)]
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
                        AuditEventType.UserDeactivated, currentUserId, actorRole, id,
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

        // GET: api/usermanagement/{id}/permissions/overrides
        [HttpGet("{id}/permissions/overrides")]
        [HasPermission(AppPermissions.UserManage)]
        public async Task<ActionResult<IReadOnlyList<UserPermissionOverrideDto>>> GetPermissionOverrides(
            string id,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var tenantScope = ResolveActorTenantScope();
            var overrides = await _permissionOverrideService.ListOverridesAsync(id, tenantScope, cancellationToken);
            return Ok(overrides);
        }

        // GET: api/usermanagement/{id}/permissions/effective
        [HttpGet("{id}/permissions/effective")]
        [HasPermission(AppPermissions.UserManage)]
        public async Task<ActionResult<UserEffectivePermissionsDto>> GetEffectivePermissions(
            string id,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            var tenantScope = ResolveActorTenantScope();
            var detail = await _permissionOverrideService.GetEffectivePermissionsDetailAsync(
                id, roles, tenantScope, cancellationToken);
            return Ok(detail);
        }

        // PUT: api/usermanagement/{id}/permissions/overrides
        [HttpPut("{id}/permissions/overrides")]
        [HasPermission(AppPermissions.UserManage)]
        public async Task<ActionResult<UserPermissionOverrideDto>> UpsertPermissionOverride(
            string id,
            [FromBody] UpsertUserPermissionOverrideRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });
            if (request == null)
                return BadRequest(new { message = "Request body is required.", code = "VALIDATION_ERROR" });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var actorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(actorId))
                return Unauthorized(new { message = "User not authenticated", code = "UNAUTHORIZED" });

            var tenantScope = ResolveActorTenantScope();
            var result = await _permissionOverrideService.UpsertOverrideAsync(
                id, request, actorId, tenantScope, cancellationToken);
            if (result == null)
                return BadRequest(new { message = "Invalid permission override request.", code = "INVALID_PERMISSION" });

            await TryLogUserLifecycleAsync(
                AuditEventType.UserPermissionOverridesChanged,
                actorId,
                GetCurrentUserRole(),
                id,
                request.Reason,
                HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string,
                AuditLogStatus.Success,
                $"Permission override {(request.IsGranted ? "granted" : "denied")}: {request.Permission}",
                oldValues: null,
                newValues: new
                {
                    request.Permission,
                    request.IsGranted,
                    request.TenantId,
                    request.ExpiresAt,
                });

            await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
            return Ok(result);
        }

        // DELETE: api/usermanagement/{id}/permissions/overrides/{overrideId}
        [HttpDelete("{id}/permissions/overrides/{overrideId:guid}")]
        [HasPermission(AppPermissions.UserManage)]
        public async Task<IActionResult> DeletePermissionOverride(
            string id,
            Guid overrideId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { message = "User id is required.", code = "VALIDATION_ERROR" });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var actorId = GetCurrentUserId();
            var tenantScope = ResolveActorTenantScope();
            var deleted = await _permissionOverrideService.DeleteOverrideAsync(id, overrideId, tenantScope, cancellationToken);
            if (!deleted)
                return NotFound(new { message = "Permission override not found" });

            if (!string.IsNullOrEmpty(actorId))
            {
                await TryLogUserLifecycleAsync(
                    AuditEventType.UserPermissionOverridesChanged,
                    actorId,
                    GetCurrentUserRole(),
                    id,
                    null,
                    HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string,
                    AuditLogStatus.Success,
                    $"Permission override removed: {overrideId}");
            }

            await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
            return Ok(new { message = "Permission override removed" });
        }

        private Guid? ResolveActorTenantScope()
        {
            var actorCanonicalRole = RoleCanonicalization.GetCanonicalRole(GetCurrentUserRole());
            if (string.Equals(actorCanonicalRole, Roles.SuperAdmin, StringComparison.Ordinal))
                return null;
            return _tenantAccessor.TenantId;
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

        // PUT: api/usermanagement/roles/{roleName}/permissions — SuperAdmin only; custom roles only (system roles immutable).
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

                var result = await _roleManagementService
                    .CreateRoleAsync(request.Name, request.InheritFromRole, CancellationToken.None)
                    .ConfigureAwait(false);

                return result switch
                {
                    CreateRoleResult.Success => Ok(new { message = "Role created successfully" }),
                    CreateRoleResult.ReservedName => BadRequest(new
                    {
                        message = "Role name is reserved for system roles. Choose a different name for a custom role.",
                        code = "ROLE_NAME_RESERVED",
                        errors = new { Name = new[] { "This role name is reserved." } },
                    }),
                    CreateRoleResult.RoleAlreadyExists => BadRequest(new { message = "Role already exists", code = "ROLE_ALREADY_EXISTS" }),
                    CreateRoleResult.SourceRoleNotFound => BadRequest(new { message = "Source role for inheritance was not found.", code = "INHERIT_ROLE_NOT_FOUND" }),
                    CreateRoleResult.CannotInheritFromSuperAdmin => BadRequest(new { message = "SuperAdmin permissions cannot be inherited onto custom roles.", code = "INHERIT_SUPERADMIN_FORBIDDEN" }),
                    _ => BadRequest(new { message = "Failed to create role", code = "ROLE_CREATE_FAILED" }),
                };
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
        /// <summary>Role name for display and form select (e.g. SuperAdmin, Manager).</summary>
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

        /// <summary>Optional. When set, updates ApplicationUser.IsDemo. Omit to leave unchanged (avoids unintended preserve on role-only edits).</summary>
        public bool? IsDemo { get; set; }
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

        /// <summary>Optional source role; permissions are copied to the new custom role.</summary>
        [MaxLength(64)]
        public string? InheritFromRole { get; set; }
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

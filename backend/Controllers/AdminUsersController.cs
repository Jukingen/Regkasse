using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin user management API – permission-first (user.manage), audit on every mutation, no hard delete.
/// Base route: /api/admin/users
/// </summary>
[Authorize]
[HasPermission(AppPermissions.UserManage)]
[ApiController]
[Route("api/admin/users")]
[Produces("application/json")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly ILogger<AdminUsersController> _logger;
    private readonly IUserTenantMembershipProvisioner _tenantMembershipProvisioner;
    private readonly ITenantUserService _tenantUserService;

    public AdminUsersController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUniquenessValidationService uniquenessValidation,
        ILogger<AdminUsersController> logger,
        IUserTenantMembershipProvisioner tenantMembershipProvisioner,
        ITenantUserService tenantUserService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
        _sessionInvalidation = sessionInvalidation;
        _uniquenessValidation = uniquenessValidation;
        _logger = logger;
        _tenantMembershipProvisioner = tenantMembershipProvisioner;
        _tenantUserService = tenantUserService;
    }

    private string? ActorId => User.GetActorUserId();
    private string ActorRole => User.GetActorRole() ?? "Unknown";

    private async Task<List<Guid>> GetBusinessTenantIdsAsync(CancellationToken cancellationToken) =>
        await _context.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive
                && t.Slug != "admin"
                && t.Slug != LegacyDefaultTenantIds.PrimarySlug)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<List<AdminUserDto>> ListPlatformUsersAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);

        var userIdsWithBusinessMembership = await _context.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.IsActive && businessTenantIds.Contains(m.TenantId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var query = _userManager.Users.AsQueryable();
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        query = query.Where(u =>
            u.Role == Roles.SuperAdmin || !userIdsWithBusinessMembership.Contains(u.Id));

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.Select(ToDto).ToList();
    }

    private async Task<List<AdminTenantUserRowDto>> ListTenantUsersAsync(
        string? role,
        bool? isActive,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);

        var membershipQuery = _context.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.IsActive && businessTenantIds.Contains(m.TenantId));

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            membershipQuery = membershipQuery.Where(m => m.TenantId == tenantId.Value);

        var rows = await (
            from m in membershipQuery
            join u in _context.Users.AsNoTracking() on m.UserId equals u.Id
            join t in _context.Tenants.AsNoTracking() on m.TenantId equals t.Id
            where u.Role != Roles.SuperAdmin
            select new { Membership = m, User = u, Tenant = t }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        var filtered = rows.AsEnumerable();
        if (isActive.HasValue)
            filtered = filtered.Where(x => x.User.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(role))
            filtered = filtered.Where(x => x.User.Role == role);

        return filtered
            .OrderBy(x => x.Tenant.Slug)
            .ThenByDescending(x => x.Membership.IsOwner)
            .ThenBy(x => x.User.LastName)
            .ThenBy(x => x.User.FirstName)
            .Select(x => new AdminTenantUserRowDto
            {
                UserId = x.User.Id,
                Email = x.User.Email ?? x.User.UserName ?? string.Empty,
                Name = FormatTenantUserName(x.User),
                Role = x.User.Role ?? Roles.FallbackUnknown,
                IsOwner = x.Membership.IsOwner,
                IsActive = x.User.IsActive,
                TenantId = x.Tenant.Id,
                TenantSlug = x.Tenant.Slug,
                TenantName = x.Tenant.Name,
                JoinedAtUtc = x.Membership.CreatedAtUtc,
            })
            .ToList();
    }

    private static string FormatTenantUserName(ApplicationUser u)
    {
        var name = $"{u.FirstName} {u.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? u.UserName ?? u.Id : name;
    }

    private static AdminUserDto ToDto(ApplicationUser u) => new()
    {
        Id = u.Id,
        UserName = u.UserName,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName,
        EmployeeNumber = u.EmployeeNumber,
        Role = u.Role,
        TaxNumber = u.TaxNumber,
        Notes = u.Notes,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        LastLoginAt = u.LastLoginAt,
        UpdatedAt = u.UpdatedAt,
        DeactivatedAt = u.DeactivatedAt,
        Etag = u.ConcurrencyStamp,
    };

    /// <summary>
    /// List users. <paramref name="type"/> = <c>platform</c> | <c>tenant</c> (alias: <c>scope</c>).
    /// Tenant rows include membership metadata; optional <paramref name="tenantId"/> filters one mandant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminUserDto>), 200)]
    [ProducesResponseType(typeof(IEnumerable<AdminTenantUserRowDto>), 200)]
    [ProducesResponseType(typeof(ApiError), 403)]
    public async Task<IActionResult> List(
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? type = null,
        [FromQuery] string? scope = null,
        [FromQuery] Guid? tenantId = null)
    {
        var listType = (type ?? scope)?.Trim();
        if (string.Equals(listType, "tenant", StringComparison.OrdinalIgnoreCase))
            return Ok(await ListTenantUsersAsync(role, isActive, tenantId, HttpContext?.RequestAborted ?? default).ConfigureAwait(false));

        if (string.Equals(listType, "platform", StringComparison.OrdinalIgnoreCase))
            return Ok(await ListPlatformUsersAsync(isActive, HttpContext?.RequestAborted ?? default).ConfigureAwait(false));

        var query = _userManager.Users.AsQueryable();
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(HttpContext?.RequestAborted ?? default)
            .ConfigureAwait(false);
        return Ok(users.Select(ToDto));
    }

    /// <summary>Create or assign a tenant user by email (generates password for new accounts; no invitation email).</summary>
    [HttpPost("invite")]
    [ProducesResponseType(typeof(TenantUserInviteResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantUserInviteResultDto>> Invite(
        [FromBody] AdminInviteUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        if (request.TenantId == Guid.Empty)
            return BadRequest(ApiError.Validation("Validation failed", new Dictionary<string, string[]> { ["tenantId"] = new[] { "Tenant id is required." } }));

        var (result, error) = await _tenantUserService
            .InviteAsync(request.TenantId, new InviteTenantUserRequest
            {
                Email = request.Email,
                Role = request.Role,
                IsOwner = request.IsOwner,
            }, ActorId ?? "unknown", cancellationToken)
            .ConfigureAwait(false);

        if (error == "Tenant not found.")
            return NotFound(ApiError.NotFound("Tenant not found", error));
        if (error != null)
            return BadRequest(ApiError.Validation("Invite failed", new Dictionary<string, string[]> { ["invite"] = new[] { error } }));

        return CreatedAtAction(nameof(List), new { type = "tenant", tenantId = request.TenantId }, result);
    }

    /// <summary>Get user by id. Returns 404 if not found.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminUserDto), 200)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<AdminUserDto>> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));
        return Ok(ToDto(user));
    }

    /// <summary>Create a new user. Writes audit event.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserDto), 201)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] AdminCreateUserRequest request)
    {
        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UserName))
            errors["userName"] = new[] { "Username is required." };
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            errors["password"] = new[] { "Password must be at least 8 characters." };
        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors["firstName"] = new[] { "First name is required." };
        if (string.IsNullOrWhiteSpace(request.LastName))
            errors["lastName"] = new[] { "Last name is required." };
        if (string.IsNullOrWhiteSpace(request.Role))
            errors["role"] = new[] { "Role is required." };
        if (errors.Count > 0)
            return BadRequest(ApiError.Validation("Validation failed", errors));

        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing != null)
            return BadRequest(ApiError.Conflict("Username already exists", $"Username '{request.UserName}' is already in use."));

        if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(request.Email, excludeUserId: null))
            return BadRequest(ApiError.Conflict("Email already exists", $"Email '{request.Email}' is already in use."));
        if (await _uniquenessValidation.IsEmployeeNumberTakenByOtherUserAsync(request.EmployeeNumber, excludeUserId: null))
            return BadRequest(ApiError.Conflict("Employee number already exists", "Employee number is already in use."));
        if (await _uniquenessValidation.IsTaxNumberTakenByOtherUserAsync(request.TaxNumber, excludeUserId: null))
            return BadRequest(ApiError.Conflict("Tax number already exists", "Tax number is already in use."));

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmployeeNumber = request.EmployeeNumber ?? "",
            Role = request.Role,
            TaxNumber = request.TaxNumber ?? "",
            Notes = request.Notes ?? "",
            IsActive = true,
            EmailConfirmed = true,
        };

        var createCt = HttpContext?.RequestAborted ?? default;
        await using var tx = await _context.Database.BeginTransactionAsync(createCt);
        try
        {
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync(createCt);
                errors = result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(ApiError.Validation("User creation failed", errors));
            }

            if (!string.IsNullOrEmpty(request.Role))
            {
                var roleAdd = await _userManager.AddToRoleAsync(user, request.Role);
                if (!roleAdd.Succeeded)
                {
                    await tx.RollbackAsync(createCt);
                    _logger.LogWarning("Failed to add role {Role} to user {UserName}", request.Role, request.UserName);
                    return BadRequest(ApiError.Validation("Role assignment failed",
                        roleAdd.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
                }
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
            _logger.LogError(ex, "Admin user create failed (rolled back): Identity or tenant membership for attempted user {UserName}", request.UserName);
            return StatusCode(500, ApiError.ServerError("User creation failed", "User creation failed."));
        }

        if (ActorId != null)
            await _auditLogService.LogUserLifecycleAsync(AuditEventType.UserCreated, ActorId, ActorRole, user.Id, null, null, AuditLogStatus.Success, $"User created: {user.UserName}");

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
    }

    /// <summary>Partial update. Use If-Match: "{etag}" for optimistic concurrency (ConcurrencyStamp).</summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(AdminUserDto), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 412)]
    public async Task<ActionResult<AdminUserDto>> Patch(string id, [FromBody] AdminPatchUserRequest request, [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        if (!string.IsNullOrWhiteSpace(ifMatch) && user.ConcurrencyStamp != ifMatch)
            return StatusCode(412, ApiError.ConcurrencyConflict("Resource version does not match. Refresh and try again."));

        // Unique fields: validate before applying; exclude current user (user.Id) so own record is not a conflict.
        if (request.Email != null && request.Email != user.Email && await _uniquenessValidation.IsEmailTakenByOtherUserAsync(request.Email, user.Id))
            return BadRequest(ApiError.Conflict("Email already exists", $"Email '{request.Email}' is already in use."));
        if (request.EmployeeNumber != null && request.EmployeeNumber.Trim() != (user.EmployeeNumber?.Trim() ?? "") && await _uniquenessValidation.IsEmployeeNumberTakenByOtherUserAsync(request.EmployeeNumber, user.Id))
            return BadRequest(ApiError.Conflict("Employee number already exists", "Employee number is already in use."));
        if (request.TaxNumber != null && request.TaxNumber.Trim() != (user.TaxNumber?.Trim() ?? "") && await _uniquenessValidation.IsTaxNumberTakenByOtherUserAsync(request.TaxNumber, user.Id))
            return BadRequest(ApiError.Conflict("Tax number already exists", "Tax number is already in use."));

        var roleChanged = false;
        var previousRole = user.Role;
        var oldSnapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.Email != null) user.Email = request.Email;
        if (request.EmployeeNumber != null) user.EmployeeNumber = request.EmployeeNumber;
        if (request.TaxNumber != null) user.TaxNumber = request.TaxNumber;
        if (request.Notes != null) user.Notes = request.Notes;
        if (request.IsDemo.HasValue) user.IsDemo = request.IsDemo.Value;
        if (request.Role != null && request.Role != user.Role)
        {
            user.Role = request.Role;
            roleChanged = true;
        }

        // Auto-clear IsDemo when role is not allowed for demo and caller did not explicitly keep it true.
        if (!request.IsDemo.HasValue
            && user.IsDemo
            && !DemoUserHelper.IsRoleAllowedForDemo(user.Role))
        {
            user.IsDemo = false;
            _logger.LogInformation("IsDemo auto-cleared for user {UserId}: role {Role} is not allowed for demo", id, user.Role);
        }

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return BadRequest(ApiError.Validation("Update failed", errors));
        }

        // Keep AspNetUserRoles in sync with ApplicationUser.Role (same rule as UserManagementController).
        if (roleChanged && !string.IsNullOrWhiteSpace(request.Role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            var addResult = await _userManager.AddToRoleAsync(user, request.Role);
            if (!addResult.Succeeded)
                _logger.LogWarning("AdminUsersController Patch: AddToRoleAsync failed for user {UserId} role {Role}", id, request.Role);
        }

        if (ActorId != null)
        {
            var newSnapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);
            await _auditLogService.LogUserLifecycleAsync(AuditEventType.UserUpdated, ActorId, ActorRole, id, null, null, AuditLogStatus.Success, $"User updated: {user.UserName}", oldSnapshot, newSnapshot);
            if (roleChanged)
            {
                await _auditLogService.LogUserLifecycleAsync(AuditEventType.UserRoleChanged, ActorId, ActorRole, id, $"Role changed from {previousRole} to {request.Role}", null, AuditLogStatus.Success, $"Role change: {previousRole} -> {request.Role}", oldValues: new { Role = previousRole }, newValues: new { Role = request.Role });
                await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
            }
        }

        return Ok(ToDto(user));
    }

    /// <summary>Deactivate user. Reason required for audit. Invalidates sessions.</summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(typeof(AdminUserDto), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<AdminUserDto>> Deactivate(string id, [FromBody] AdminDeactivateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiError.Validation("Reason required", new Dictionary<string, string[]> { ["reason"] = new[] { "Deactivation reason is required for audit compliance." } }));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));
        if (!user.IsActive)
            return BadRequest(ApiError.BusinessRule("User is already deactivated"));

        if (id == ActorId)
            return BadRequest(ApiError.BusinessRule("You cannot deactivate your own account"));

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        user.DeactivatedAt = DateTime.UtcNow;
        user.DeactivatedBy = ActorId;
        user.DeactivationReason = request.Reason.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(ApiError.Validation("Deactivate failed", result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));

        if (ActorId != null)
        {
            var desc = "User deactivated: " + user.UserName + ". Reason: " + request.Reason;
            await _auditLogService.LogUserLifecycleAsync(AuditEventType.UserDeactivated, ActorId, ActorRole, id, request.Reason.Trim(), null, AuditLogStatus.Success, desc);
        }

        await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
        return Ok(ToDto(user));
    }

    /// <summary>Reactivate user. Optional reason. Writes audit event.</summary>
    [HttpPost("{id}/reactivate")]
    [ProducesResponseType(typeof(AdminUserDto), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<AdminUserDto>> Reactivate(string id, [FromBody] AdminReactivateRequest? request = null)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));
        if (user.IsActive)
            return BadRequest(ApiError.BusinessRule("User is already active"));

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.DeactivatedAt = null;
        user.DeactivatedBy = null;
        user.DeactivationReason = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(ApiError.Validation("Reactivate failed", result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));

        var reason = request?.Reason?.Trim();
        if (ActorId != null)
            await _auditLogService.LogUserLifecycleAsync(AuditEventType.UserReactivated, ActorId, ActorRole, id, reason, null, AuditLogStatus.Success, $"User reactivated: {user.UserName}" + (string.IsNullOrEmpty(reason) ? "" : $". Note: {reason}"));

        return Ok(ToDto(user));
    }

    /// <summary>Force password reset (admin). User must change password at next login can be set. Invalidates sessions.</summary>
    [HttpPost("{id}/force-password-reset")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<IActionResult> ForcePasswordReset(string id, [FromBody] AdminForcePasswordResetRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(ApiError.Validation("Invalid password", new Dictionary<string, string[]> { ["newPassword"] = new[] { "New password must be at least 8 characters." } }));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(ApiError.Validation("Password reset failed", result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));

        if (ActorId != null)
            await _auditLogService.LogUserLifecycleAsync(AuditEventType.PasswordResetForced, ActorId, ActorRole, id, null, null, AuditLogStatus.Success, "Admin force password reset");

        await _sessionInvalidation.InvalidateSessionsForUserAsync(id);
        return NoContent();
    }

    /// <summary>Get audit activity for the user (paginated).</summary>
    [HttpGet("{id}/activity")]
    [ProducesResponseType(typeof(AdminUserActivityResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<AdminUserActivityResponse>> GetActivity(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var logs = await _auditLogService.GetUserAuditLogsAsync(id, null, null, page, pageSize);
        var total = await _auditLogService.GetUserLifecycleAuditLogsCountAsync(id, null, null);

        return Ok(new AdminUserActivityResponse
        {
            UserId = id,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            Items = logs.Select(l => new AdminUserActivityItem
            {
                Id = l.Id,
                Action = l.Action,
                EntityType = l.EntityType,
                Description = l.Description,
                Status = l.Status.ToString(),
                Timestamp = l.Timestamp,
                CorrelationId = l.CorrelationId,
            }).ToList(),
        });
    }

    // --- DTOs (safe; no secrets) ---

    public class AdminTenantUserRowDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public bool IsActive { get; set; }
        public Guid TenantId { get; set; }
        public string TenantSlug { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime JoinedAtUtc { get; set; }
    }

    public class AdminInviteUserRequest
    {
        [Required]
        public Guid TenantId { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Role { get; set; } = string.Empty;

        public bool IsOwner { get; set; }
    }

    public class AdminUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? Role { get; set; }
        public string? TaxNumber { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeactivatedAt { get; set; }
        /// <summary>Concurrency stamp for If-Match (optimistic concurrency).</summary>
        public string? Etag { get; set; }
    }

    public class AdminCreateUserRequest
    {
        [Required, MaxLength(50)]
        public string UserName { get; set; } = string.Empty;
        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;
        [EmailAddress, MaxLength(256)]
        public string? Email { get; set; }
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        [MaxLength(20)]
        public string? EmployeeNumber { get; set; }
        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty;
        [MaxLength(20)]
        public string? TaxNumber { get; set; }
        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class AdminPatchUserRequest
    {
        [MaxLength(50)]
        public string? FirstName { get; set; }
        [MaxLength(50)]
        public string? LastName { get; set; }
        [EmailAddress, MaxLength(256)]
        public string? Email { get; set; }
        [MaxLength(20)]
        public string? EmployeeNumber { get; set; }
        [MaxLength(20)]
        public string? Role { get; set; }
        [MaxLength(20)]
        public string? TaxNumber { get; set; }
        [MaxLength(500)]
        public string? Notes { get; set; }
        /// <summary>Optional. When set, updates ApplicationUser.IsDemo (demo is not a role; flag must be cleared to allow real payments).</summary>
        public bool? IsDemo { get; set; }
    }

    public class AdminDeactivateRequest
    {
        [Required, MinLength(1), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class AdminReactivateRequest
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class AdminForcePasswordResetRequest
    {
        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AdminUserActivityResponse
    {
        public string UserId { get; set; } = string.Empty;
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<AdminUserActivityItem> Items { get; set; } = new();
    }

    public class AdminUserActivityItem
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? CorrelationId { get; set; }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Validators;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Email;
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
public partial class AdminUsersController : ControllerBase
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
    private readonly IUserCreationService _userCreation;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IUsernameChangeEmailService _usernameChangeEmail;
    private readonly IUserUsernameHistoryService _usernameHistory;
    private readonly ActivityEventRecorder _activityEvents;

    public AdminUsersController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IUserSessionInvalidation sessionInvalidation,
        IUserUniquenessValidationService uniquenessValidation,
        IUserCreationService userCreation,
        ILogger<AdminUsersController> logger,
        IUserTenantMembershipProvisioner tenantMembershipProvisioner,
        ITenantUserService tenantUserService,
        ICurrentTenantAccessor tenantAccessor,
        IUsernameChangeEmailService usernameChangeEmail,
        IUserUsernameHistoryService usernameHistory,
        ActivityEventRecorder activityEvents)
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
        _userCreation = userCreation;
        _tenantAccessor = tenantAccessor;
        _usernameChangeEmail = usernameChangeEmail;
        _usernameHistory = usernameHistory;
        _activityEvents = activityEvents;
    }

    private bool IsActorSuperAdmin() =>
        string.Equals(
            RoleCanonicalization.GetCanonicalRole(ActorRole),
            Roles.SuperAdmin,
            StringComparison.Ordinal);

    private string? ActorId => User.GetActorUserId();
    private string ActorRole => User.GetActorRole() ?? "Unknown";

    private async Task<bool> ValidateUserInTenantAsync(
        string userId,
        Guid? expectedTenantId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = expectedTenantId ?? _tenantAccessor.TenantId;
        if (tenantId == null)
            return false;

        var membership = await _context.UserTenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId
                    && m.TenantId == tenantId
                    && m.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        return membership != null;
    }

    private async Task<bool> IsUserAccessibleInAmbientTenantAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid ambientTenantId)
            return true;

        return await ValidateUserInTenantAsync(userId, ambientTenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<Guid>> GetBusinessTenantIdsAsync(CancellationToken cancellationToken) =>
        await _context.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive
                && t.Status != TenantStatuses.Deleted
                && t.Slug != "admin"
                && t.Slug != LegacyDefaultTenantIds.PrimarySlug)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static IEnumerable<ApplicationUser> ApplyOrphanUserListFilter(
        IEnumerable<ApplicationUser> users,
        bool? isActive)
    {
        if (isActive == true)
        {
            return users.Where(u =>
                u.IsActive && !OperationalTenantMembershipPolicy.IsOrphanedTenantUser(u));
        }

        if (isActive == false)
        {
            return users.Where(u =>
                !u.IsActive || OperationalTenantMembershipPolicy.IsOrphanedTenantUser(u));
        }

        return users.Where(u => !OperationalTenantMembershipPolicy.IsOrphanedTenantUser(u));
    }

    /// <summary>Case-insensitive match on login name, email, display name, and employee number.</summary>
    private static IQueryable<ApplicationUser> ApplyUserSearchFilter(IQueryable<ApplicationUser> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;

        var term = search.Trim().ToLowerInvariant();
        return query.Where(u =>
            (u.UserName != null && u.UserName.ToLower().Contains(term))
            || (u.Email != null && u.Email.ToLower().Contains(term))
            || (u.FirstName + " " + u.LastName).ToLower().Contains(term)
            || (u.EmployeeNumber != null && u.EmployeeNumber.ToLower().Contains(term)));
    }

    private static bool MatchesTenantUserSearch(
        string? search,
        string name,
        string userName,
        string email,
        string tenantName,
        string tenantSlug)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        var term = search.Trim().ToLowerInvariant();
        return name.ToLowerInvariant().Contains(term)
            || userName.ToLowerInvariant().Contains(term)
            || email.ToLowerInvariant().Contains(term)
            || tenantName.ToLowerInvariant().Contains(term)
            || tenantSlug.ToLowerInvariant().Contains(term);
    }

    private async Task<List<AdminUserDto>> ListPlatformUsersAsync(
        bool? isActive,
        string? search,
        CancellationToken cancellationToken)
    {
        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);
        var businessTenantIdSet = businessTenantIds.ToHashSet();

        var operationalTenantIds = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive && t.Status != TenantStatuses.Deleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIdsWithOperationalMembership = await MembershipsQuery()
            .Where(m => m.IsActive && operationalTenantIds.Contains(m.TenantId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var operationalUserIdSet = userIdsWithOperationalMembership.ToHashSet();

        var userIdsWithBusinessMembership = await MembershipsQuery()
            .Where(m => m.IsActive && businessTenantIds.Contains(m.TenantId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var businessUserIdSet = userIdsWithBusinessMembership.ToHashSet();

        var query = UsersWithMembershipsQuery();
        if (isActive == true)
            query = query.Where(u => u.IsActive);

        query = query.Where(u =>
            u.Role == Roles.SuperAdmin
            || (operationalUserIdSet.Contains(u.Id) && !businessUserIdSet.Contains(u.Id)));
        query = ApplyUserSearchFilter(query, search);

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ApplyOrphanUserListFilter(users, isActive)
            .Select(u => ToDto(u, businessTenantIdSet))
            .ToList();
    }

    private async Task<List<AdminTenantUserRowDto>> ListTenantUsersAsync(
        string? role,
        bool? isActive,
        Guid? tenantId,
        string? search,
        CancellationToken cancellationToken)
    {
        if (!IsActorSuperAdmin())
        {
            if (_tenantAccessor.TenantId is Guid ambientTenantId)
                tenantId = ambientTenantId;
            else
                return new List<AdminTenantUserRowDto>();
        }

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var tenantUsers = await _tenantUserService
                .ListAsync(tenantId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (tenantUsers == null)
                return new List<AdminTenantUserRowDto>();

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (tenant == null)
                return new List<AdminTenantUserRowDto>();

            var userIds = tenantUsers.Select(u => u.UserId).ToList();
            var usersById = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken)
                .ConfigureAwait(false);

            return tenantUsers
                .Where(dto =>
                {
                    if (!usersById.TryGetValue(dto.UserId, out var user))
                        return false;
                    if (isActive.HasValue && user.IsActive != isActive.Value)
                        return false;
                    if (!string.IsNullOrWhiteSpace(role) && user.Role != role)
                        return false;
                    return MatchesTenantUserSearch(search, dto.Name, dto.UserName, dto.Email, tenant.Name, tenant.Slug);
                })
                .Select(dto =>
                {
                    usersById.TryGetValue(dto.UserId, out var user);
                    return new AdminTenantUserRowDto
                    {
                        UserId = dto.UserId,
                        UserName = !string.IsNullOrEmpty(dto.UserName) ? dto.UserName : user?.UserName ?? string.Empty,
                        Email = dto.Email,
                        Name = dto.Name,
                        Role = dto.Role,
                        IsOwner = dto.IsOwner,
                        IsActive = user?.IsActive ?? true,
                        TenantId = tenant.Id,
                        TenantSlug = tenant.Slug,
                        TenantName = tenant.Name,
                        JoinedAtUtc = dto.JoinedAtUtc,
                        LastLoginAt = user?.LastLoginAt,
                    };
                })
                .ToList();
        }

        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);

        var membershipQuery = MembershipsQuery()
            .Where(m => m.IsActive && businessTenantIds.Contains(m.TenantId));

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
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                MatchesTenantUserSearch(
                    search,
                    FormatTenantUserName(x.User),
                    x.User.UserName ?? string.Empty,
                    x.User.Email ?? x.User.UserName ?? string.Empty,
                    x.Tenant.Name,
                    x.Tenant.Slug));
        }

        return filtered
            .OrderBy(x => x.Tenant.Slug)
            .ThenByDescending(x => x.Membership.IsOwner)
            .ThenBy(x => x.User.LastName)
            .ThenBy(x => x.User.FirstName)
            .Select(x => new AdminTenantUserRowDto
            {
                UserId = x.User.Id,
                UserName = x.User.UserName ?? string.Empty,
                Email = x.User.Email ?? x.User.UserName ?? string.Empty,
                Name = FormatTenantUserName(x.User),
                Role = x.User.Role ?? Roles.FallbackUnknown,
                IsOwner = x.Membership.IsOwner,
                IsActive = x.User.IsActive,
                TenantId = x.Tenant.Id,
                TenantSlug = x.Tenant.Slug,
                TenantName = x.Tenant.Name,
                JoinedAtUtc = x.Membership.CreatedAtUtc,
                LastLoginAt = x.User.LastLoginAt,
            })
            .ToList();
    }

    private static string FormatTenantUserName(ApplicationUser u)
    {
        var name = $"{u.FirstName} {u.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? u.UserName ?? u.Id : name;
    }

    private IQueryable<UserTenantMembership> MembershipsQuery() =>
        _tenantAccessor.TenantId is Guid
            ? _context.UserTenantMemberships.AsNoTracking()
            : _context.UserTenantMemberships.AsNoTracking().IgnoreQueryFilters();

    private IQueryable<ApplicationUser> UsersWithMembershipsQuery()
    {
        var query = _context.Users.AsNoTracking();
        if (_tenantAccessor.TenantId is not Guid)
            query = query.IgnoreQueryFilters();

        return query
            .Include(u => u.UserTenantMemberships)
            .ThenInclude(m => m.Tenant);
    }

    private static UserTenantMembership? PickPrimaryMembership(
        ApplicationUser user,
        IReadOnlySet<Guid>? businessTenantIds)
    {
        var active = user.UserTenantMemberships
            .Where(m => m.IsActive
                && m.Tenant != null
                && m.Tenant.IsActive
                && m.Tenant.Status != TenantStatuses.Deleted)
            .ToList();

        if (active.Count == 0)
            return null;

        var business = businessTenantIds is { Count: > 0 }
            ? active.Where(m => businessTenantIds.Contains(m.TenantId)).ToList()
            : active;

        var pool = business.Count > 0 ? business : active;

        return pool
            .OrderByDescending(m => m.IsOwner)
            .ThenBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .FirstOrDefault();
    }

    private static AdminUserDto ToDto(ApplicationUser u, IReadOnlySet<Guid>? businessTenantIds = null)
    {
        var dto = new AdminUserDto
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
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

        if (string.Equals(u.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            dto.UserType = "Platform";
            return dto;
        }

        var primary = PickPrimaryMembership(u, businessTenantIds);
        if (primary?.Tenant == null)
        {
            dto.UserType = "Platform";
            return dto;
        }

        dto.UserType = "Tenant";
        dto.TenantId = primary.TenantId.ToString();
        dto.TenantName = primary.Tenant.Name;
        dto.TenantSlug = primary.Tenant.Slug;
        return dto;
    }

    private async Task<Guid?> ResolvePrimaryTenantIdForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId
                && m.IsActive
                && m.Tenant != null
                && m.Tenant.IsActive
                && m.Tenant.Status != TenantStatuses.Deleted)
            .OrderByDescending(m => m.IsOwner)
            .ThenBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.Id)
            .Select(m => (Guid?)m.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? search = null)
    {
        var listType = (type ?? scope)?.Trim();
        if (string.Equals(listType, "tenant", StringComparison.OrdinalIgnoreCase))
            return Ok(await ListTenantUsersAsync(role, isActive, tenantId, search, HttpContext?.RequestAborted ?? default).ConfigureAwait(false));

        if (string.Equals(listType, "platform", StringComparison.OrdinalIgnoreCase))
            return Ok(await ListPlatformUsersAsync(isActive, search, HttpContext?.RequestAborted ?? default).ConfigureAwait(false));

        var cancellationToken = HttpContext?.RequestAborted ?? default;
        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);
        var businessTenantIdSet = businessTenantIds.ToHashSet();

        var query = UsersWithMembershipsQuery();
        if (isActive == true)
            query = query.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);
        query = ApplyUserSearchFilter(query, search);

        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApplyOrphanUserListFilter(users, isActive).Select(u => ToDto(u, businessTenantIdSet)));
    }

    /// <summary>Preview next auto-generated username for Quick Create (role-based prefix + number).</summary>
    [HttpGet("username-suggestions")]
    [ProducesResponseType(typeof(UsernameSuggestionResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<ActionResult<UsernameSuggestionResponse>> GetUsernameSuggestions(
        [FromQuery] string? role,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return BadRequest(ApiError.Validation(
                "Validation failed",
                new Dictionary<string, string[]> { ["role"] = new[] { "Role is required." } }));
        }

        var normalizedRole = role.Trim();
        if (await _roleManager.FindByNameAsync(normalizedRole).ConfigureAwait(false) == null)
        {
            return BadRequest(ApiError.Validation(
                "Role not found",
                new Dictionary<string, string[]> { ["role"] = new[] { "The specified role does not exist." } }));
        }

        var (suggestedUsername, availableNumbers) = await UniqueUsernameGenerator
            .GetSuggestionAsync(_context, normalizedRole, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new UsernameSuggestionResponse
        {
            SuggestedUsername = suggestedUsername,
            AvailableNumbers = availableNumbers,
        });
    }

    /// <summary>Get user by id. Returns 404 if not found.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminUserDto), 200)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<AdminUserDto>> GetById(string id, CancellationToken cancellationToken = default)
    {
        var businessTenantIdSet = (await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false)).ToHashSet();
        var user = await UsersWithMembershipsQuery()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        if (_tenantAccessor.TenantId is Guid ambientTenantId)
        {
            var hasActiveMembershipInAmbientTenant = user.UserTenantMemberships.Any(m =>
                m.IsActive
                && m.TenantId == ambientTenantId
                && m.Tenant != null
                && m.Tenant.IsActive
                && m.Tenant.Status != TenantStatuses.Deleted);

            if (!hasActiveMembershipInAmbientTenant)
                return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));
        }

        return Ok(ToDto(user, businessTenantIdSet));
    }

    /// <summary>Active tenant memberships for a user (super-admin user management).</summary>
    [HttpGet("{id}/tenants")]
    [ProducesResponseType(typeof(IEnumerable<AdminUserTenantMembershipDto>), 200)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<IEnumerable<AdminUserTenantMembershipDto>>> GetUserTenants(
        string id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);
        var rows = await _context.UserTenantMemberships
            .AsNoTracking()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == id && m.IsActive && businessTenantIds.Contains(m.TenantId))
            .OrderBy(m => m.Tenant!.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows.Select(m => new AdminUserTenantMembershipDto
        {
            TenantId = m.TenantId,
            TenantName = m.Tenant?.Name ?? string.Empty,
            TenantSlug = m.Tenant?.Slug ?? string.Empty,
            IsOwner = m.IsOwner,
            Role = user.Role ?? Roles.FallbackUnknown,
        }));
    }

    /// <summary>Replace active business-tenant memberships for a user. Super-admin only; not for platform operators.</summary>
    [HttpPut("{id}/tenants")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<IActionResult> PutUserTenants(
        string id,
        [FromBody] UpdateUserTenantsRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!IsActorSuperAdmin())
            return StatusCode(403, ApiError.Forbidden("Forbidden", "Only Super Admin can change user tenant memberships."));

        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        if (string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiError.BusinessRule("Platform users cannot be assigned to tenants."));

        var requestedIds = (request.TenantIds ?? new List<Guid>())
            .Where(t => t != Guid.Empty)
            .Distinct()
            .ToList();

        var businessTenantIds = await GetBusinessTenantIdsAsync(cancellationToken).ConfigureAwait(false);
        var businessSet = businessTenantIds.ToHashSet();
        var invalid = requestedIds.Where(t => !businessSet.Contains(t)).ToList();
        if (invalid.Count > 0)
            return BadRequest(ApiError.Validation("Invalid tenant ids", new Dictionary<string, string[]>
            {
                ["tenantIds"] = new[] { "One or more tenant ids are not valid business tenants." },
            }));

        var existing = await _context.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var previousActive = existing
            .Where(m => m.IsActive && businessSet.Contains(m.TenantId))
            .Select(m => m.TenantId)
            .OrderBy(x => x)
            .ToList();

        var requestedSet = requestedIds.ToHashSet();
        var now = DateTime.UtcNow;

        foreach (var membership in existing)
        {
            if (!businessSet.Contains(membership.TenantId))
                continue;

            var shouldBeActive = requestedSet.Contains(membership.TenantId);
            if (membership.IsActive == shouldBeActive)
                continue;

            membership.IsActive = shouldBeActive;
            membership.UpdatedAtUtc = now;
        }

        foreach (var tenantId in requestedIds)
        {
            if (existing.Any(m => m.TenantId == tenantId))
                continue;

            _context.UserTenantMemberships.Add(new UserTenantMembership
            {
                UserId = id,
                TenantId = tenantId,
                IsActive = true,
                CreatedAtUtc = now,
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var newActive = requestedIds.OrderBy(x => x).ToList();
        if (ActorId != null)
        {
            await _auditLogService.LogUserLifecycleAsync(
                AuditEventType.UserTenantMembershipChanged,
                ActorId,
                ActorRole,
                id,
                null,
                null,
                AuditLogStatus.Success,
                $"User tenant memberships updated: {user.UserName}",
                oldValues: new { TenantIds = previousActive },
                newValues: new { TenantIds = newActive });
        }

        return NoContent();
    }

    /// <summary>
    /// Create a user without invitation email. Password is generated when omitted and returned once in the response.
    /// When <see cref="AdminCreateUserRequest.TenantId"/> is set, creates a tenant-scoped user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminCreateUserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateTenantUserResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] AdminCreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        var roleName = request.Role?.Trim();
        var resolvedTenantId = ResolveCreateTenantId(request);

        _logger.LogInformation(
            "Admin user create: role={Role}, requestTenantId={RequestTenantId}, ambientTenantId={AmbientTenantId}, resolvedTenantId={ResolvedTenantId}",
            roleName ?? "(empty)",
            request.TenantId,
            _tenantAccessor.TenantId,
            resolvedTenantId);

        if (resolvedTenantId is Guid tenantId)
            return await CreateTenantUserAsync(tenantId, request, cancellationToken).ConfigureAwait(false);

        if (!IsPlatformOnlyRole(roleName))
        {
            _logger.LogWarning(
                "Admin user create rejected: mandant role {Role} without tenant (requestTenantId={RequestTenantId}, ambientTenantId={AmbientTenantId})",
                roleName,
                request.TenantId,
                _tenantAccessor.TenantId);
            return BadRequest(ApiError.Validation(
                "Validation failed",
                new Dictionary<string, string[]>
                {
                    ["tenantId"] = new[]
                    {
                        "Tenant ID is required for mandant users. Set tenantId in the request body or use POST /api/admin/tenants/{tenantId}/users.",
                    },
                }));
        }

        return await CreatePlatformUserAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Explicit <see cref="AdminCreateUserRequest.TenantId"/> wins; else HTTP tenant context.</summary>
    private Guid? ResolveCreateTenantId(AdminCreateUserRequest request)
    {
        if (request.TenantId is Guid explicitTenantId && explicitTenantId != Guid.Empty)
            return explicitTenantId;
        if (_tenantAccessor.TenantId is Guid ambientTenantId)
            return ambientTenantId;
        return null;
    }

    private static bool IsPlatformOnlyRole(string? role) =>
        string.Equals(role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    private async Task<IActionResult> CreatePlatformUserAsync(
        AdminCreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            errors["email"] = new[] { "Email is required." };
        if (string.IsNullOrWhiteSpace(request.Role))
            errors["role"] = new[] { "Role is required." };
        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password!.Length < 8)
            errors["password"] = new[] { "Password must be at least 8 characters when provided." };
        if (errors.Count > 0)
            return BadRequest(ApiError.Validation("Validation failed", errors));

        var roleName = request.Role!.Trim();
        if (await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false) == null)
            return BadRequest(ApiError.Validation("Role not found", new Dictionary<string, string[]> { ["role"] = new[] { "The specified role does not exist." } }));

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var requestedUsername = request.UserName.Trim();
            if (await _uniquenessValidation.IsUserNameTakenByOtherUserAsync(requestedUsername, excludeUserId: null)
                    .ConfigureAwait(false))
            {
                return BadRequest(ApiError.Conflict(
                    UsernameConflictMessages.Title,
                    UsernameConflictMessages.Detail(requestedUsername)));
            }
        }

        var (userName, userNameError) = await _userCreation
            .ResolveUsernameAsync(request.UserName, roleName, cancellationToken)
            .ConfigureAwait(false);
        if (userNameError != null)
        {
            if (!string.IsNullOrWhiteSpace(request.UserName) && ReservedUsernames.IsReserved(request.UserName))
            {
                return BadRequest(ApiError.Validation(
                    "Validation failed",
                    new Dictionary<string, string[]> { ["userName"] = new[] { userNameError } }));
            }

            return BadRequest(ApiError.Conflict(UsernameConflictMessages.Title, userNameError));
        }

        if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(email, excludeUserId: null).ConfigureAwait(false))
            return BadRequest(ApiError.Conflict("Email already exists", $"Email '{email}' is already in use."));
        if (await _uniquenessValidation.IsEmployeeNumberTakenByOtherUserAsync(request.EmployeeNumber, excludeUserId: null).ConfigureAwait(false))
            return BadRequest(ApiError.Conflict("Employee number already exists", "Employee number is already in use."));
        if (await _uniquenessValidation.IsTaxNumberTakenByOtherUserAsync(request.TaxNumber, excludeUserId: null).ConfigureAwait(false))
            return BadRequest(ApiError.Conflict("Tax number already exists", "Tax number is already in use."));

        var generatedPassword = string.IsNullOrWhiteSpace(request.Password)
            ? PasswordGenerator.GenerateSecurePassword(12)
            : request.Password!;
        var employeeNumber = string.IsNullOrWhiteSpace(request.EmployeeNumber)
            ? $"ADM{Guid.NewGuid():N}"[..20]
            : request.EmployeeNumber.Trim();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FirstName = request.FirstName?.Trim() ?? string.Empty,
            LastName = request.LastName?.Trim() ?? string.Empty,
            EmployeeNumber = employeeNumber,
            Role = roleName,
            TaxNumber = string.IsNullOrWhiteSpace(request.TaxNumber) ? null : request.TaxNumber.Trim(),
            Notes = request.Notes ?? string.Empty,
            IsActive = true,
            EmailConfirmed = true,
            MustChangePasswordOnNextLogin = string.IsNullOrWhiteSpace(request.Password),
        };

        var createCt = HttpContext?.RequestAborted ?? cancellationToken;
        await using var tx = await _context.Database.BeginTransactionAsync(createCt);
        try
        {
            var result = await _userManager.CreateAsync(user, generatedPassword).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync(createCt).ConfigureAwait(false);
                errors = result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(ApiError.Validation("User creation failed", errors));
            }

            var roleAdd = await _userManager.AddToRoleAsync(user, roleName).ConfigureAwait(false);
            if (!roleAdd.Succeeded)
            {
                await tx.RollbackAsync(createCt).ConfigureAwait(false);
                _logger.LogWarning("Failed to add role {Role} to user {UserName}", roleName, userName);
                return BadRequest(ApiError.Validation("Role assignment failed",
                    roleAdd.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
            }

            if (!string.Equals(roleName, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                await _tenantMembershipProvisioner.ProvisionActiveMembershipAsync(
                    user.Id, LegacyDefaultTenantIds.Primary, cancellationToken: createCt).ConfigureAwait(false);
            }

            await tx.CommitAsync(createCt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(createCt).ConfigureAwait(false);
            _logger.LogError(ex, "Admin user create failed (rolled back): Identity or tenant membership for attempted user {UserName}", userName);
            return StatusCode(500, ApiError.ServerError("User creation failed", "User creation failed."));
        }

        if (ActorId != null)
        {
            await _auditLogService.LogUserLifecycleAsync(
                AuditEventType.UserCreated,
                ActorId,
                ActorRole,
                user.Id,
                status: AuditLogStatus.Success,
                description: $"User created: {user.UserName}",
                userCreatedDetails: new UserCreatedAuditDetails(
                    ActorId,
                    roleName,
                    TenantId: null,
                    PasswordReturned: true)).ConfigureAwait(false);
        }

        await _activityEvents.TryPublishAsync(
            LegacyDefaultTenantIds.Primary,
            ActivityEventType.UserCreated,
            new
            {
                UserId = user.Id,
                UserEmail = user.Email,
                Role = roleName,
                ActorId,
            },
            ActorId,
            cancellationToken: createCt).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToCreateResponse(ToDto(user), generatedPassword));
    }

    private async Task<IActionResult> CreateTenantUserAsync(
        Guid tenantId,
        AdminCreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = new[] { "Email is required." };
        if (string.IsNullOrWhiteSpace(request.Role))
            errors["role"] = new[] { "Role is required." };
        if (errors.Count > 0)
            return BadRequest(ApiError.Validation("Validation failed", errors));

        var actorId = ActorId ?? "unknown";
        var (result, error) = await _tenantUserService
            .CreateAsync(
                tenantId,
                new CreateTenantUserRequest
                {
                    Email = request.Email.Trim(),
                    UserName = request.UserName,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Role = request.Role.Trim(),
                    IsOwner = request.IsOwner,
                },
                actorId,
                ActorRole,
                cancellationToken)
            .ConfigureAwait(false);

        if (error == "Tenant not found.")
            return NotFound(ApiError.NotFound("Tenant not found", error));
        if (error != null)
            return BadRequest(ApiError.Validation("User creation failed", new Dictionary<string, string[]> { ["create"] = new[] { error } }));

        return CreatedAtAction(nameof(List), new { type = "tenant", tenantId }, result);
    }

    private static AdminCreateUserResponseDto ToCreateResponse(AdminUserDto dto, string generatedPassword) =>
        new()
        {
            Id = dto.Id,
            UserName = dto.UserName,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            EmployeeNumber = dto.EmployeeNumber,
            Role = dto.Role,
            TaxNumber = dto.TaxNumber,
            Notes = dto.Notes,
            IsActive = dto.IsActive,
            CreatedAt = dto.CreatedAt,
            LastLoginAt = dto.LastLoginAt,
            UpdatedAt = dto.UpdatedAt,
            DeactivatedAt = dto.DeactivatedAt,
            Etag = dto.Etag,
            TenantId = dto.TenantId,
            TenantName = dto.TenantName,
            TenantSlug = dto.TenantSlug,
            UserType = dto.UserType,
            GeneratedPassword = generatedPassword,
        };

    /// <summary>Partial update. Use If-Match: "{etag}" for optimistic concurrency (ConcurrencyStamp).</summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(AdminUserDto), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 412)]
    public async Task<ActionResult<AdminUserDto>> Patch(
        string id,
        [FromBody] AdminPatchUserRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

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
        if (request.TaxNumber != null)
            user.TaxNumber = string.IsNullOrWhiteSpace(request.TaxNumber) ? null : request.TaxNumber.Trim();
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
    public async Task<ActionResult<AdminUserDto>> Deactivate(
        string id,
        [FromBody] AdminDeactivateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiError.Validation("Reason required", new Dictionary<string, string[]> { ["reason"] = new[] { "Deactivation reason is required for audit compliance." } }));

        if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

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
    public async Task<ActionResult<AdminUserDto>> Reactivate(
        string id,
        [FromBody] AdminReactivateRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

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
        user.LockoutEnd = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(ApiError.Validation("Reactivate failed", result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));

        var reason = request?.Reason?.Trim();
        if (ActorId != null)
            await _auditLogService.LogUserLifecycleAsync(AuditEventType.UserReactivated, ActorId, ActorRole, id, reason, null, AuditLogStatus.Success, $"User reactivated: {user.UserName}" + (string.IsNullOrEmpty(reason) ? "" : $". Note: {reason}"));

        return Ok(ToDto(user));
    }

    /// <summary>Username change history for compliance and support (newest first).</summary>
    [HttpGet("{id}/username-history")]
    [ProducesResponseType(typeof(IReadOnlyList<UserUsernameHistoryDto>), 200)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<IReadOnlyList<UserUsernameHistoryDto>>> GetUsernameHistory(
        string id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id).ConfigureAwait(false);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var rows = await _usernameHistory.ListForUserAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    /// <summary>Change login username. Audited; invalidates active sessions.</summary>
    [HttpPatch("{id}/username")]
    [ProducesResponseType(typeof(AdminUpdateUsernameResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    [ProducesResponseType(typeof(ApiError), 409)]
    public async Task<ActionResult<AdminUpdateUsernameResponse>> UpdateUsername(
        string id,
        [FromBody] UpdateUsernameRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(ApiError.Validation("Invalid body", new Dictionary<string, string[]> { ["body"] = new[] { "Request body is required." } }));

        if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var newUsername = request.NewUsername.Trim();
        var validationErrors = UsernameValidation.ValidateNewUsername(newUsername);
        if (validationErrors != null)
            return BadRequest(ApiError.Validation("Validation failed", validationErrors));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var oldUsername = user.UserName;
        if (string.Equals(oldUsername, newUsername, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new AdminUpdateUsernameResponse
            {
                OldUsername = oldUsername,
                NewUsername = user.UserName ?? newUsername,
            });
        }

        var rateLimitError = await UsernameChangeRateLimit
            .GetRateLimitErrorAsync(_userManager, user, cancellationToken)
            .ConfigureAwait(false);
        if (rateLimitError != null)
            return BadRequest(ApiError.BusinessRule("Username change rate limit", rateLimitError));

        var newAccountError = UsernameChangePolicy.GetNewAccountRestrictionError(user);
        if (newAccountError != null)
            return BadRequest(ApiError.BusinessRule("Username change not allowed", newAccountError));

        if (await _uniquenessValidation.IsUserNameTakenByOtherUserAsync(newUsername, user.Id).ConfigureAwait(false))
            return Conflict(ApiError.Conflict(UsernameConflictMessages.Title, UsernameConflictMessages.Detail(newUsername)));

        var setNameResult = await _userManager.SetUserNameAsync(user, newUsername).ConfigureAwait(false);
        if (!setNameResult.Succeeded)
        {
            return BadRequest(ApiError.Validation(
                "Username update failed",
                setNameResult.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
        }

        user.UpdatedAt = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!updateResult.Succeeded)
        {
            return BadRequest(ApiError.Validation(
                "Username update failed",
                updateResult.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
        }

        var targetTenantId = await ResolvePrimaryTenantIdForUserAsync(id, cancellationToken).ConfigureAwait(false);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (ActorId != null)
        {
            await _auditLogService.LogUserLifecycleAsync(
                AuditEventType.UserNameChanged,
                ActorId,
                ActorRole,
                id,
                targetTenantId,
                reason,
                null,
                AuditLogStatus.Success,
                description: $"Username changed from '{oldUsername}' to '{newUsername}'",
                oldValues: new { UserName = oldUsername },
                newValues: new { UserName = newUsername }).ConfigureAwait(false);
        }

        await InvalidateUserSessionsAfterUsernameChangeAsync(id, user, cancellationToken).ConfigureAwait(false);

        await _usernameHistory.RecordChangeAsync(
            id,
            oldUsername,
            newUsername,
            ActorId,
            reason,
            cancellationToken).ConfigureAwait(false);

        await UsernameChangeRateLimit.RecordChangeAsync(_userManager, user, cancellationToken).ConfigureAwait(false);

        await TryNotifyUsernameChangedAsync(user, oldUsername, newUsername).ConfigureAwait(false);

        return Ok(new AdminUpdateUsernameResponse
        {
            OldUsername = oldUsername,
            NewUsername = user.UserName ?? newUsername,
        });
    }

    private async Task InvalidateUserSessionsAfterUsernameChangeAsync(
        string userId,
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var activeSessionCount = await _context.AuthSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        if (!stampResult.Succeeded)
        {
            _logger.LogWarning(
                "Security stamp update failed after username change for user {UserId}: {Errors}",
                userId,
                string.Join("; ", stampResult.Errors.Select(e => e.Description)));
        }

        await _sessionInvalidation.InvalidateSessionsForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        if (activeSessionCount > 0)
        {
            _logger.LogInformation(
                "Username change for user {UserId}: invalidated {SessionCount} active session(s).",
                userId,
                activeSessionCount);
        }
    }

    private async Task TryNotifyUsernameChangedAsync(
        ApplicationUser user,
        string? oldUsername,
        string newUsername)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return;

        var actorEmail = "unknown";
        if (!string.IsNullOrEmpty(ActorId))
        {
            var actor = await _userManager.FindByIdAsync(ActorId).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(actor?.Email))
                actorEmail = actor.Email.Trim();
        }

        var sent = await _usernameChangeEmail.TrySendUsernameChangedAsync(
            new UsernameChangedEmailRequest(
                user.Email.Trim(),
                oldUsername ?? string.Empty,
                newUsername,
                actorEmail,
                DateTime.UtcNow)).ConfigureAwait(false);

        if (!sent)
        {
            _logger.LogInformation(
                "Username change email skipped or failed for user {UserId} (SMTP not configured or send error).",
                user.Id);
        }
    }

    /// <summary>Force password reset (admin). User must change password at next login can be set. Invalidates sessions.</summary>
    [HttpPost("{id}/force-password-reset")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<IActionResult> ForcePasswordReset(
        string id,
        [FromBody] AdminForcePasswordResetRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(ApiError.Validation("Invalid password", new Dictionary<string, string[]> { ["newPassword"] = new[] { "New password must be at least 8 characters." } }));

        if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

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

    /// <summary>
    /// Generates a new temporary password, stores only the hash, and returns the password once for Super Admin handoff.
    /// Intended for controlled operator recovery; existing password hashes are never exposed.
    /// </summary>
    [HttpPost("{id}/generate-temporary-password")]
    [ProducesResponseType(typeof(AdminTemporaryPasswordResponse), 200)]
    [ProducesResponseType(typeof(ApiError), 400)]
    [ProducesResponseType(typeof(ApiError), 403)]
    [ProducesResponseType(typeof(ApiError), 404)]
    public async Task<ActionResult<AdminTemporaryPasswordResponse>> GenerateTemporaryPassword(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!IsActorSuperAdmin())
            return StatusCode(403, ApiError.Forbidden("Forbidden", "Only Super Admin can generate temporary passwords."));

        if (id == ActorId)
            return BadRequest(ApiError.BusinessRule("You cannot generate a temporary password for your own account."));

        if (!await IsUserAccessibleInAmbientTenantAsync(id, cancellationToken).ConfigureAwait(false))
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found."));

        var targetTenantId = await ResolvePrimaryTenantIdForUserAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
        var generatedPassword = PasswordGenerator.GenerateSecurePassword(12);
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        user.MustChangePasswordOnNextLogin = true;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.ResetPasswordAsync(user, token, generatedPassword);
        if (!result.Succeeded)
        {
            return BadRequest(ApiError.Validation(
                "Temporary password generation failed",
                result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())));
        }

        if (ActorId != null)
        {
            await _auditLogService.LogUserLifecycleAsync(
                AuditEventType.PasswordResetForced,
                ActorId,
                ActorRole,
                id,
                targetTenantId,
                "Temporary password generated by Super Admin.",
                null,
                AuditLogStatus.Success,
                "Temporary password generated and shown once.",
                oldValues: new { MustChangePasswordOnNextLogin = false },
                newValues: new { MustChangePasswordOnNextLogin = true, PasswordReturned = true });

            await _auditLogService.LogUserLifecycleAsync(
                AuditLogActions.SUPER_ADMIN_VIEWED_PASSWORD,
                ActorId,
                ActorRole,
                id,
                targetTenantId,
                "Support / Password reset",
                null,
                AuditLogStatus.Success,
                "Super Admin viewed a newly generated temporary password.",
                oldValues: null,
                newValues: new
                {
                    UserEmail = user.Email,
                    ViewedAt = DateTime.UtcNow,
                    PasswordReturned = true
                });
        }

        await _sessionInvalidation.InvalidateSessionsForUserAsync(id);

        return Ok(new AdminTemporaryPasswordResponse
        {
            GeneratedPassword = generatedPassword,
            ForcePasswordChangeOnNextLogin = true,
        });
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
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public bool IsActive { get; set; }
        public Guid TenantId { get; set; }
        public string TenantSlug { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime JoinedAtUtc { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class AdminUserTenantMembershipDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantSlug { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
    }

    public class UpdateUserTenantsRequest
    {
        public List<Guid> TenantIds { get; set; } = new();
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

    public class AdminTemporaryPasswordResponse
    {
        public string GeneratedPassword { get; set; } = string.Empty;
        public bool ForcePasswordChangeOnNextLogin { get; set; }
    }

    public class AdminUpdateUsernameResponse
    {
        public string? OldUsername { get; set; }
        public string NewUsername { get; set; } = string.Empty;
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

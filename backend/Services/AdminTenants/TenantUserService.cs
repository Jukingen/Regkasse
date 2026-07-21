using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantUserService : ITenantUserService
{
    private const string ActionTenantUserCreated = "TENANT_USER_CREATED";
    private const string ActionTenantQuickUserCreated = AuditLogActions.TENANT_QUICK_USER_CREATED;

    private static readonly HashSet<string> AssignableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Manager,
        Roles.Cashier,
        Roles.Waiter,
        Roles.Kitchen,
        Roles.ReportViewer,
        Roles.Accountant,
    };

    private static readonly HashSet<string> TenantCreateRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Manager,
        Roles.Cashier,
        Roles.Accountant,
    };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantMembershipProvisioner _membershipProvisioner;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly IQuickUserGeneratorService _quickUserGenerator;
    private readonly IUserCreationService _userCreation;
    private readonly IAuditLogService _auditLog;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ActivityEventRecorder _activityEvents;
    private readonly IUserRoleChangeService _userRoleChangeService;
    private readonly ILogger<TenantUserService> _logger;

    public TenantUserService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        IUserUniquenessValidationService uniquenessValidation,
        IUserSessionInvalidation sessionInvalidation,
        IQuickUserGeneratorService quickUserGenerator,
        IUserCreationService userCreation,
        IAuditLogService auditLog,
        IHttpContextAccessor httpContextAccessor,
        ICurrentTenantAccessor tenantAccessor,
        ActivityEventRecorder activityEvents,
        IUserRoleChangeService userRoleChangeService,
        ILogger<TenantUserService> logger)
    {
        _db = db;
        _userManager = userManager;
        _membershipProvisioner = membershipProvisioner;
        _uniquenessValidation = uniquenessValidation;
        _sessionInvalidation = sessionInvalidation;
        _quickUserGenerator = quickUserGenerator;
        _userCreation = userCreation;
        _auditLog = auditLog;
        _httpContextAccessor = httpContextAccessor;
        _tenantAccessor = tenantAccessor;
        _activityEvents = activityEvents;
        _userRoleChangeService = userRoleChangeService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantUserDto>?> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return null;

        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return null;

        var rows = await MembershipsUnfiltered()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { Membership = m, User = u })
            .OrderByDescending(x => x.Membership.IsOwner)
            .ThenBy(x => x.User.LastName)
            .ThenBy(x => x.User.FirstName)
            .Select(x => new TenantUserDto(
                x.User.Id,
                x.User.UserName ?? string.Empty,
                x.User.Email ?? x.User.UserName ?? string.Empty,
                (x.User.FirstName + " " + x.User.LastName).Trim() == string.Empty
                    ? x.User.UserName ?? x.User.Id
                    : (x.User.FirstName + " " + x.User.LastName).Trim(),
                x.User.Role ?? Roles.FallbackUnknown,
                x.Membership.IsOwner,
                x.Membership.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }

    public Task<(TenantUserDto? Result, string? Error)> AssignExistingAsync(
        Guid tenantId,
        AddAdminTenantUserRequest request,
        CancellationToken cancellationToken = default) =>
        AssignExistingInternalAsync(tenantId, request, cancellationToken);

    private async Task<(TenantUserDto? Result, string? Error)> AssignExistingInternalAsync(
        Guid tenantId,
        AddAdminTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return (null, "Tenant not found.");

        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant not found.");

        if (string.IsNullOrWhiteSpace(request.UserId))
            return (null, "User id is required.");

        var (roleValid, roleError) = await ValidateAssignableRoleAsync(request.Role, cancellationToken).ConfigureAwait(false);
        if (!roleValid)
            return (null, roleError);

        var user = await _userManager.FindByIdAsync(request.UserId).ConfigureAwait(false);
        if (user == null)
            return (null, "User not found.");

        if (string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return (null, "SuperAdmin users cannot be assigned to a tenant via membership.");

        var assignError = await AssignUserToTenantAsync(user, tenantId, request.Role, request.IsOwner, cancellationToken)
            .ConfigureAwait(false);
        if (assignError != null)
            return (null, assignError);

        var membership = await FindActiveMembershipAsync(user.Id, tenantId, cancellationToken).ConfigureAwait(false);
        if (membership == null)
            return (null, "Membership was not created for this tenant.");

        return (ToDto(user, membership), null);
    }

    public Task<(CreateTenantUserResultDto? Result, string? Error)> CreateAsync(
        Guid tenantId,
        CreateTenantUserRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default) =>
        CreateUserCoreAsync(
            tenantId,
            request.Email.Trim(),
            request.Role,
            request.IsOwner,
            actorUserId,
            actorRole,
            ActionTenantUserCreated,
            firstName: ResolveFirstName(request.FirstName),
            lastName: request.LastName,
            notesSuffix: "manual create",
            preGeneratedPassword: null,
            passwordLength: 12,
            explicitUserName: request.UserName,
            cancellationToken: cancellationToken);

    public async Task<(CreateTenantUserResultDto? Result, string? Error)> CreateQuickAsync(
        Guid tenantId,
        CreateQuickTenantUserRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var (plan, error) = await _quickUserGenerator
            .PrepareAsync(tenantId, request.Role, request.UserName, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return (null, error);

        return await CreateUserCoreAsync(
            tenantId,
            plan!.Email,
            plan.Role,
            isOwner: false,
            actorUserId,
            ResolveActorRole(),
            ActionTenantQuickUserCreated,
            firstName: plan.Role,
            lastName: null,
            notesSuffix: "quick user",
            preGeneratedPassword: plan.Password,
            passwordLength: 12,
            explicitUserName: plan.UserName,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<(CreateTenantUserResultDto? Result, string? Error)> CreateUserCoreAsync(
        Guid tenantId,
        string email,
        string role,
        bool isOwner,
        string actorUserId,
        string actorRole,
        string auditAction,
        string firstName,
        string? lastName,
        string notesSuffix,
        string? preGeneratedPassword,
        int passwordLength,
        string? explicitUserName = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return (null, "Tenant not found.");

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        if (string.IsNullOrEmpty(email))
            return (null, "Email is required.");

        if (!TryValidateTenantCreateRole(role, out var roleError))
            return (null, roleError);

        var existing = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing != null)
            return (null, "An account with this email already exists. Assign the existing user instead.");

        if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(email, excludeUserId: null)
                .ConfigureAwait(false))
            return (null, $"Email '{email}' is already in use.");

        var generatedPassword = !string.IsNullOrEmpty(preGeneratedPassword)
            ? preGeneratedPassword
            : PasswordGenerator.GenerateSecurePassword(passwordLength);
        var now = DateTime.UtcNow;
        var normalizedRole = role.Trim();
        var (userName, userNameError) = await _userCreation
            .ResolveUsernameAsync(explicitUserName, normalizedRole, cancellationToken)
            .ConfigureAwait(false);
        if (userNameError != null)
            return (null, userNameError);
        var resolvedLastName = string.IsNullOrWhiteSpace(lastName)
            ? tenant.Name
            : lastName.Trim();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FirstName = firstName.Length > 50 ? firstName[..50] : firstName,
            LastName = resolvedLastName.Length > 50 ? resolvedLastName[..50] : resolvedLastName,
            EmployeeNumber = $"INV{Guid.NewGuid():N}"[..20],
            Role = normalizedRole,
            Notes = $"{notesSuffix} for tenant {tenant.Slug}",
            IsActive = true,
            EmailConfirmed = true,
            AccountType = "Admin",
            IsDemo = false,
            MustChangePasswordOnNextLogin = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var create = await _userManager.CreateAsync(user, generatedPassword).ConfigureAwait(false);
        if (!create.Succeeded)
            return (null, string.Join("; ", create.Errors.Select(e => e.Description)));

        var roleAdd = await _userManager.AddToRoleAsync(user, normalizedRole).ConfigureAwait(false);
        if (!roleAdd.Succeeded)
            return (null, string.Join("; ", roleAdd.Errors.Select(e => e.Description)));

        var assignError = await AssignUserToTenantAsync(user, tenantId, normalizedRole, isOwner, cancellationToken)
            .ConfigureAwait(false);
        if (assignError != null)
            return (null, assignError);

        var portalUrl = BuildTenantPortalUrl(tenant.Slug);
        var actorId = string.IsNullOrWhiteSpace(actorUserId) ? "unknown" : actorUserId.Trim();
        var isQuick = string.Equals(auditAction, ActionTenantQuickUserCreated, StringComparison.Ordinal);
        if (isQuick)
        {
            await LogQuickUserCreatedAuditAsync(
                    actorId,
                    tenantId,
                    user.Id,
                    email,
                    userName,
                    normalizedRole,
                    tenant.Slug,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await LogUserCreatedAuditAsync(
                    actorId,
                    actorRole,
                    tenantId,
                    user.Id,
                    email,
                    tenant.Slug,
                    normalizedRole,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Created tenant user {Email} for tenant {TenantId} by {Actor} (auditAction={AuditAction})",
            email,
            tenantId,
            actorId,
            auditAction);

        await _activityEvents.TryPublishAsync(
            new ActivityEventPublishRequest(
                tenantId,
                ActivityEventType.UserCreated,
                "User created",
                Description: $"User {userName} ({email}) was created.",
                ActorUserId: actorId,
                EntityType: "user",
                EntityId: user.Id),
            cancellationToken).ConfigureAwait(false);

        return (new CreateTenantUserResultDto(
            user.Id,
            email,
            userName,
            generatedPassword,
            ForcePasswordChangeOnNextLogin: true,
            Success: true,
            portalUrl,
            isQuick ? normalizedRole : null,
            tenant.Id,
            tenant.Slug), null);
    }

    private async Task LogUserCreatedAuditAsync(
        string actorUserId,
        string actorRole,
        Guid tenantId,
        string targetUserId,
        string email,
        string tenantSlug,
        string role,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            await _auditLog.LogUserLifecycleAsync(
                AuditEventType.UserCreated,
                actorUserId,
                actorRole,
                targetUserId,
                status: AuditLogStatus.Success,
                description: $"Tenant user created for {email} on tenant {tenantSlug}",
                userCreatedDetails: new UserCreatedAuditDetails(
                    actorUserId,
                    role,
                    tenantId,
                    PasswordReturned: true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for USER_CREATED tenant {TenantId}", tenantId);
        }
    }

    private string ResolveActorRole()
    {
        var role = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return string.IsNullOrWhiteSpace(role) ? Roles.FallbackUnknown : role.Trim();
    }

    private bool IsActorSuperAdmin() =>
        string.Equals(
            RoleCanonicalization.GetCanonicalRole(ResolveActorRole()),
            Roles.SuperAdmin,
            StringComparison.Ordinal);

    private bool CanAccessTenant(Guid tenantId) =>
        IsActorSuperAdmin()
        || (_tenantAccessor.TenantId is Guid ambient && ambient == tenantId);

    private async Task LogQuickUserCreatedAuditAsync(
        string actorUserId,
        Guid tenantId,
        string targetUserId,
        string email,
        string userName,
        string role,
        string tenantSlug,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = new
            {
                email,
                userName,
                role,
                generatedPassword = "***HIDDEN***",
                method = "quick_generate",
                forcePasswordChangeOnNextLogin = true,
                tenantId,
            };

            var description =
                $"Super Admin erstellte Schnell-Benutzer '{userName}' ({role}) für Mandant '{tenantSlug}'";

            Guid? entityId = Guid.TryParse(targetUserId, out var parsedUserId) ? parsedUserId : null;
            var now = DateTime.UtcNow;
            var entry = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = Guid.NewGuid().ToString(),
                UserId = actorUserId.Length > 450 ? actorUserId[..450] : actorUserId,
                UserRole = Roles.SuperAdmin,
                Action = AuditLogActions.TENANT_QUICK_USER_CREATED,
                EntityType = AuditLogEntityTypes.USER,
                EntityId = entityId,
                EntityName = targetUserId.Length > 100 ? targetUserId[..100] : targetUserId,
                RequestData = AuditLogPersistenceSanitizer.SerializeObjectToJsonColumn(details),
                Status = AuditLogStatus.Success,
                Timestamp = now,
                CreatedAt = now,
                UpdatedAt = now,
                Description = description.Length > 500 ? description[..500] : description,
                Notes = $"tenantId={tenantId}",
                IsActive = true,
            };

            ImpersonationAuditContext.ApplyTo(
                entry,
                ImpersonationAuditContext.FromHttpContext(_httpContextAccessor.HttpContext, _tenantAccessor));

            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audit log failed for {Action} tenant {TenantId} user {UserId}",
                AuditLogActions.TENANT_QUICK_USER_CREATED,
                tenantId,
                targetUserId);
        }
    }

    public async Task<(TenantUserDto? Result, string? Error)> UpdateAsync(
        Guid tenantId,
        string userId,
        UpdateAdminTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return (null, "Tenant not found.");

        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant not found.");

        var membership = await MembershipsUnfiltered()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (membership == null)
            return (null, "User is not assigned to this tenant.");

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return (null, "User not found.");

        if (request.Role != null)
        {
            var (roleValid, roleError) = await ValidateAssignableRoleAsync(request.Role, cancellationToken).ConfigureAwait(false);
            if (!roleValid)
                return (null, roleError);

            var roleUpdateError = await ChangeUserRoleAsync(
                user,
                request.Role,
                tenantId,
                cancellationToken).ConfigureAwait(false);
            if (roleUpdateError != null)
                return (null, roleUpdateError);
        }

        if (request.IsOwner == true)
        {
            await ClearTenantOwnersExceptAsync(tenantId, membership.Id, cancellationToken).ConfigureAwait(false);
            membership.IsOwner = true;
            membership.UpdatedAtUtc = DateTime.UtcNow;
        }
        else if (request.IsOwner == false)
        {
            membership.IsOwner = false;
            membership.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (ToDto(user, membership), null);
    }

    public async Task<(TenantUserPasswordResetResultDto? Result, string? Error)> ResetPasswordAsync(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return (null, "Tenant not found.");

        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant not found.");

        var hasMembership = await MembershipsUnfiltered()
            .AsNoTracking()
            .AnyAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (!hasMembership)
            return (null, "User is not assigned to this tenant.");

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null || !user.IsActive)
            return (null, "User not found.");

        if (string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return (null, "Cannot reset password for SuperAdmin via tenant user management.");

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        var generatedPassword = PasswordGenerator.GenerateSecurePassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var reset = await _userManager.ResetPasswordAsync(user, token, generatedPassword).ConfigureAwait(false);
        if (!reset.Succeeded)
            return (null, string.Join("; ", reset.Errors.Select(e => e.Description)));

        user.MustChangePasswordOnNextLogin = true;
        user.UpdatedAt = DateTime.UtcNow;
        var profileUpdate = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!profileUpdate.Succeeded)
            return (null, string.Join("; ", profileUpdate.Errors.Select(e => e.Description)));

        await _sessionInvalidation.InvalidateSessionsForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        const string deliveryNote =
            "Share the password manually. User must change password on next login.";

        try
        {
            await _auditLog.LogUserLifecycleAsync(
                AuditEventType.PasswordResetForced,
                ResolveActorUserId(),
                ResolveActorRole(),
                userId,
                tenantId,
                reason: "Tenant user password reset (generated one-time password)",
                description: "Admin force password reset for tenant user",
                status: AuditLogStatus.Success).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for password reset user {UserId} tenant {TenantId}", userId, tenantId);
        }

        _logger.LogInformation(
            "Super-admin reset password for user {UserId} on tenant {TenantId}",
            userId,
            tenantId);

        return (new TenantUserPasswordResetResultDto(
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            generatedPassword,
            deliveryNote,
            ForcePasswordChangeOnNextLogin: true), null);
    }

    public async Task<(TenantUserDto? Result, string? Error)> UpdateRoleAsync(
        Guid tenantId,
        string userId,
        UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return (null, "Tenant not found.");

        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant not found.");

        var membership = await MembershipsUnfiltered()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (membership == null)
            return (null, "User is not assigned to this tenant.");

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return (null, "User not found.");

        var (roleValid, roleError) = await ValidateAssignableRoleAsync(request.Role, cancellationToken).ConfigureAwait(false);
        if (!roleValid)
            return (null, roleError);

        var roleUpdateError = await ChangeUserRoleAsync(
                user,
                request.Role,
                tenantId,
                cancellationToken).ConfigureAwait(false);
        if (roleUpdateError != null)
            return (null, roleUpdateError);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (ToDto(user, membership), null);
    }

    public async Task<(bool Success, string? Error)> RemoveAsync(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!CanAccessTenant(tenantId))
            return (false, "Tenant not found.");

        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (false, "Tenant not found.");

        var membership = await MembershipsUnfiltered()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (membership == null)
            return (false, "User is not assigned to this tenant.");

        membership.IsActive = false;
        membership.IsOwner = false;
        membership.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, null);
    }

    private async Task<string?> AssignUserToTenantAsync(
        ApplicationUser user,
        Guid tenantId,
        string role,
        bool isOwner,
        CancellationToken cancellationToken)
    {
        var roleUpdateError = await ChangeUserRoleAsync(
            user,
            role,
            tenantId,
            cancellationToken).ConfigureAwait(false);
        if (roleUpdateError != null)
            return roleUpdateError;

        await _membershipProvisioner
            .ProvisionActiveMembershipAsync(user.Id, tenantId, isOwner, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return null;
    }

    private static string BuildTenantPortalUrl(string slug) => $"https://{slug.Trim()}.regkasse.at";

    /// <summary>
    /// Super-admin APIs pass explicit <paramref name="tenantId"/>; bypass <see cref="ITenantEntity"/> ambient filter.
    /// </summary>
    private IQueryable<UserTenantMembership> MembershipsUnfiltered() =>
        _db.UserTenantMemberships.IgnoreQueryFilters();

    private Task<UserTenantMembership?> FindActiveMembershipAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        MembershipsUnfiltered()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.TenantId == tenantId && m.IsActive,
                cancellationToken);

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);

    private async Task ClearTenantOwnersExceptAsync(Guid tenantId, Guid exceptMembershipId, CancellationToken cancellationToken)
    {
        var others = await MembershipsUnfiltered()
            .Where(m => m.TenantId == tenantId && m.IsOwner && m.Id != exceptMembershipId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        foreach (var m in others)
        {
            m.IsOwner = false;
            m.UpdatedAtUtc = now;
        }
    }

    private string ResolveActorUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(userId) ? "unknown" : userId.Trim();
    }

    private async Task<string?> ChangeUserRoleAsync(
        ApplicationUser user,
        string role,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var (_, error) = await _userRoleChangeService.ChangeUserRoleAsync(
            user,
            role,
            ResolveActorUserId(),
            ResolveActorRole(),
            tenantId,
            cancellationToken).ConfigureAwait(false);
        return error;
    }

    private async Task<(bool Valid, string? Error)> ValidateAssignableRoleAsync(
        string? role,
        CancellationToken cancellationToken)
    {
        var trimmed = role?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return (false, "Role is required.");

        if (string.Equals(trimmed, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || Roles.ReservedRoleNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return (false, $"Role '{trimmed}' cannot be assigned to a tenant user.");
        }

        if (AssignableRoles.Contains(trimmed))
            return (true, null);

        var normalized = trimmed.ToUpperInvariant();
        var roleExists = await _db.Roles.AsNoTracking()
            .AnyAsync(r => r.NormalizedName == normalized, cancellationToken)
            .ConfigureAwait(false);
        if (roleExists)
            return (true, null);

        return (false, $"Role '{trimmed}' cannot be assigned to a tenant user.");
    }

    private static bool TryValidateTenantCreateRole(string? role, out string? error)
    {
        error = null;
        var trimmed = role?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "Role is required.";
            return false;
        }

        if (!TenantCreateRoles.Contains(trimmed))
        {
            error = $"Role '{trimmed}' cannot be used for tenant user creation.";
            return false;
        }

        return true;
    }

    private static string ResolveFirstName(string? firstName) =>
        string.IsNullOrWhiteSpace(firstName) ? "User" : firstName.Trim();

    private static string FormatName(ApplicationUser user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? user.UserName ?? user.Id : name;
    }

    private static TenantUserDto ToDto(ApplicationUser user, UserTenantMembership membership) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? user.UserName ?? string.Empty,
            FormatName(user),
            user.Role ?? Roles.FallbackUnknown,
            membership.IsOwner,
            membership.CreatedAtUtc);
}

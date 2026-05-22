using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantUserService : ITenantUserService
{
    private const string AuditEntityType = "TenantUser";
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

    private static readonly HashSet<string> InviteRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Manager,
        Roles.Cashier,
        Roles.Accountant,
    };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantMembershipProvisioner _membershipProvisioner;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly ITenantInvitationEmailSender _invitationEmail;
    private readonly IUserSessionInvalidation _sessionInvalidation;
    private readonly IQuickUserGeneratorService _quickUserGenerator;
    private readonly IAuditLogService _auditLog;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<TenantUserService> _logger;

    public TenantUserService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        IUserUniquenessValidationService uniquenessValidation,
        ITenantInvitationEmailSender invitationEmail,
        IUserSessionInvalidation sessionInvalidation,
        IQuickUserGeneratorService quickUserGenerator,
        IAuditLogService auditLog,
        IHttpContextAccessor httpContextAccessor,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<TenantUserService> logger)
    {
        _db = db;
        _userManager = userManager;
        _membershipProvisioner = membershipProvisioner;
        _uniquenessValidation = uniquenessValidation;
        _invitationEmail = invitationEmail;
        _sessionInvalidation = sessionInvalidation;
        _quickUserGenerator = quickUserGenerator;
        _auditLog = auditLog;
        _httpContextAccessor = httpContextAccessor;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantUserDto>?> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return null;

        return await MembershipsUnfiltered()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new TenantUserDto(
                    u.Id,
                    u.Email ?? u.UserName ?? string.Empty,
                    FormatName(u),
                    u.Role ?? Roles.FallbackUnknown,
                    m.IsOwner,
                    m.CreatedAtUtc))
            .OrderByDescending(d => d.IsOwner)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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
        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant not found.");

        if (string.IsNullOrWhiteSpace(request.UserId))
            return (null, "User id is required.");

        if (!TryValidateAssignableRole(request.Role, out var roleError))
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
        CancellationToken cancellationToken = default) =>
        CreateUserCoreAsync(
            tenantId,
            request.Email.Trim(),
            request.Role,
            request.IsOwner,
            actorUserId,
            ActionTenantUserCreated,
            firstName: "Invited",
            notesSuffix: "manual create",
            preGeneratedPassword: null,
            passwordLength: 14,
            cancellationToken: cancellationToken);

    public async Task<(CreateTenantUserResultDto? Result, string? Error)> CreateQuickAsync(
        Guid tenantId,
        CreateQuickTenantUserRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var (plan, error) = await _quickUserGenerator
            .PrepareAsync(tenantId, request.Role, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return (null, error);

        return await CreateUserCoreAsync(
            tenantId,
            plan!.Email,
            plan.Role,
            isOwner: false,
            actorUserId,
            ActionTenantQuickUserCreated,
            firstName: plan.Role,
            notesSuffix: "quick user",
            preGeneratedPassword: plan.Password,
            passwordLength: 12,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<(CreateTenantUserResultDto? Result, string? Error)> CreateUserCoreAsync(
        Guid tenantId,
        string email,
        string role,
        bool isOwner,
        string actorUserId,
        string auditAction,
        string firstName,
        string notesSuffix,
        string? preGeneratedPassword,
        int passwordLength,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        if (string.IsNullOrEmpty(email))
            return (null, "Email is required.");

        if (!TryValidateInviteRole(role, out var roleError))
            return (null, roleError);

        var existing = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing != null)
            return (null, "An account with this email already exists. Assign the existing user instead.");

        if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(email, excludeUserId: null)
                .ConfigureAwait(false))
            return (null, $"Email '{email}' is already in use.");

        var generatedPassword = !string.IsNullOrEmpty(preGeneratedPassword)
            ? preGeneratedPassword
            : PasswordGenerator.GenerateRandomPassword(passwordLength);
        var now = DateTime.UtcNow;
        var normalizedRole = role.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName.Length > 50 ? firstName[..50] : firstName,
            LastName = tenant.Name.Length > 50 ? tenant.Name[..50] : tenant.Name,
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
                    normalizedRole,
                    tenant.Slug,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await LogTenantUserCreatedAsync(
                    actorId,
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

        return (new CreateTenantUserResultDto(
            user.Id,
            email,
            generatedPassword,
            ForcePasswordChangeOnNextLogin: true,
            Success: true,
            portalUrl,
            isQuick ? normalizedRole : null), null);
    }

    public async Task<(TenantUserInviteResultDto? Result, string? Error)> InviteAsync(
        Guid tenantId,
        InviteTenantUserRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var createResult = await CreateAsync(
            tenantId,
            new CreateTenantUserRequest
            {
                Email = request.Email,
                Role = request.Role,
                IsOwner = request.IsOwner,
            },
            actorUserId,
            cancellationToken).ConfigureAwait(false);
        if (createResult.Error != null)
        {
            if (createResult.Error.StartsWith("An account with this email already exists", StringComparison.Ordinal))
                return await InviteAssignExistingAsync(tenantId, request, cancellationToken).ConfigureAwait(false);
            return (null, createResult.Error);
        }

        var created = createResult.Result!;
        var membership = await FindActiveMembershipAsync(created.UserId, tenantId, cancellationToken).ConfigureAwait(false);
        if (membership == null)
            return (null, "Membership was not created for this tenant.");

        var user = await _userManager.FindByIdAsync(created.UserId).ConfigureAwait(false);
        if (user == null)
            return (null, "User not found.");

        return (new TenantUserInviteResultDto(
            ToDto(user, membership),
            UserCreated: true,
            InvitationEmailSent: false,
            "User created. Share the password manually; user must change it on first login.",
            created.GeneratedPassword,
            created.ForcePasswordChangeOnNextLogin,
            created.TenantPortalUrl), null);
    }

    private async Task<(TenantUserInviteResultDto? Result, string? Error)> InviteAssignExistingAsync(
        Guid tenantId,
        InviteTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user == null)
            return (null, "User not found.");

        if (string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return (null, "SuperAdmin users cannot be assigned to a tenant via invite.");

        var alreadyMember = await MembershipsUnfiltered().AsNoTracking()
            .AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyMember)
            return (null, "User is already assigned to this tenant.");

        var assignError = await AssignUserToTenantAsync(user, tenantId, request.Role, request.IsOwner, cancellationToken)
            .ConfigureAwait(false);
        if (assignError != null)
            return (null, assignError);

        var membership = await FindActiveMembershipAsync(user.Id, tenantId, cancellationToken).ConfigureAwait(false);
        if (membership == null)
            return (null, "Membership was not created for this tenant.");

        return (new TenantUserInviteResultDto(
            ToDto(user, membership),
            UserCreated: false,
            InvitationEmailSent: false,
            "Existing user assigned to tenant.",
            GeneratedPassword: null,
            ForcePasswordChangeOnNextLogin: false,
            BuildTenantPortalUrl(tenant.Slug)), null);
    }

    private async Task LogTenantUserCreatedAsync(
        string actorUserId,
        Guid tenantId,
        string targetUserId,
        string email,
        string tenantSlug,
        string role,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                ActionTenantUserCreated,
                AuditEntityType,
                actorUserId,
                Roles.SuperAdmin,
                description: $"Tenant user created for {email} on tenant {tenantSlug}",
                notes: $"tenantId={tenantId};targetUserId={targetUserId};role={role}",
                status: AuditLogStatus.Success,
                requestData: new { tenantId, email, tenantSlug, role },
                responseData: new
                {
                    targetUserId,
                    success = true,
                    passwordReturned = false,
                    actorUserId,
                    createdAtUtc = DateTime.UtcNow,
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for {Action} tenant {TenantId}", ActionTenantUserCreated, tenantId);
        }
    }

    private async Task LogQuickUserCreatedAuditAsync(
        string actorUserId,
        Guid tenantId,
        string targetUserId,
        string email,
        string role,
        string tenantSlug,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = new
            {
                email,
                role,
                generatedPassword = "***HIDDEN***",
                method = "quick_generate",
                forcePasswordChangeOnNextLogin = true,
                tenantId,
            };

            var description =
                $"Super Admin erstellte Schnell-Benutzer '{email}' ({role}) für Mandant '{tenantSlug}'";

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
                RequestData = JsonSerializer.Serialize(details),
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
            if (!TryValidateAssignableRole(request.Role, out var roleError))
                return (null, roleError);

            var roleUpdateError = await ApplyUserRoleAsync(user, request.Role, cancellationToken).ConfigureAwait(false);
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
        ResetTenantUserPasswordRequest? request = null,
        CancellationToken cancellationToken = default)
    {
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

        var generatedPassword = PasswordGenerator.GenerateRandomPassword();
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

        var sendEmail = request?.SendEmail ?? true;
        var smtpConfigured = _invitationEmail.IsConfigured;
        var emailSent = false;
        string deliveryNote;

        if (sendEmail && smtpConfigured)
        {
            var email = user.Email ?? user.UserName ?? string.Empty;
            var portalUrl = BuildTenantPortalUrl(tenant.Slug);
            var subject = $"Neues Passwort – {tenant.Name}";
            var body = BuildPasswordResetEmailBody(tenant.Name, portalUrl, generatedPassword);
            emailSent = await _invitationEmail
                .TrySendInvitationAsync(email, subject, body, cancellationToken)
                .ConfigureAwait(false);
        }

        if (emailSent)
        {
            deliveryNote = "Password reset email sent. User must change password on next login.";
        }
        else if (!smtpConfigured)
        {
            deliveryNote = "SMTP is not configured; share the password manually. User must change password on next login.";
        }
        else if (!sendEmail)
        {
            deliveryNote = "Email not sent (operator choice). Share the password manually. User must change password on next login.";
        }
        else
        {
            deliveryNote = "Password reset applied; email could not be delivered. Share the password manually.";
        }

        _logger.LogInformation(
            "Super-admin reset password for user {UserId} on tenant {TenantId} (emailSent={EmailSent})",
            userId,
            tenantId,
            emailSent);

        return (new TenantUserPasswordResetResultDto(
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            generatedPassword,
            deliveryNote,
            emailSent,
            smtpConfigured,
            ForcePasswordChangeOnNextLogin: true), null);
    }

    private static string BuildPasswordResetEmailBody(string tenantName, string portalUrl, string temporaryPassword) =>
        string.Join(
            Environment.NewLine,
            $"Ihr Passwort für den Mandanten \"{tenantName}\" wurde zurückgesetzt.",
            string.Empty,
            $"Anmeldung: {portalUrl}",
            string.Empty,
            "Bitte melden Sie sich mit folgendem Einmalpasswort an und ändern Sie es anschließend:",
            temporaryPassword,
            string.Empty,
            "Mit freundlichen Grüßen",
            "Regkasse");

    public Task<(TenantUserDto? Result, string? Error)> UpdateRoleAsync(
        Guid tenantId,
        string userId,
        UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            tenantId,
            userId,
            new UpdateAdminTenantUserRequest { Role = request.Role },
            cancellationToken);

    public async Task<(bool Success, string? Error)> RemoveAsync(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
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
        var roleUpdateError = await ApplyUserRoleAsync(user, role, cancellationToken).ConfigureAwait(false);
        if (roleUpdateError != null)
            return roleUpdateError;

        await _membershipProvisioner
            .ProvisionActiveMembershipAsync(user.Id, tenantId, isOwner, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return null;
    }

    private static string BuildTenantPortalUrl(string slug) => $"https://{slug.Trim()}.regkasse.at";

    private static string BuildInvitationEmailBody(
        string tenantName,
        string slug,
        string role,
        string portalUrl,
        bool userCreated,
        string? temporaryPassword)
    {
        var lines = new List<string>
        {
            $"Sie wurden zum Mandanten \"{tenantName}\" ({slug}) eingeladen.",
            "",
            $"Rolle: {role}",
            $"Anmeldung: {portalUrl}",
            "",
        };

        if (userCreated && !string.IsNullOrEmpty(temporaryPassword))
        {
            lines.Add("Ihr Zugang wurde neu erstellt. Bitte melden Sie sich mit folgendem Passwort an");
            lines.Add("(Passwort nach der ersten Anmeldung ändern):");
            lines.Add(temporaryPassword);
            lines.Add("");
        }
        else
        {
            lines.Add("Melden Sie sich mit Ihrem bestehenden Regkasse-Konto an.");
            lines.Add("");
        }

        lines.Add("Mit freundlichen Grüßen");
        lines.Add("Regkasse");
        return string.Join(Environment.NewLine, lines);
    }

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

    private async Task<string?> ApplyUserRoleAsync(ApplicationUser user, string role, CancellationToken cancellationToken)
    {
        var normalized = role.Trim();
        if (string.Equals(user.Role, normalized, StringComparison.OrdinalIgnoreCase))
            return null;

        var previousRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        if (previousRoles.Count > 0)
        {
            var remove = await _userManager.RemoveFromRolesAsync(user, previousRoles).ConfigureAwait(false);
            if (!remove.Succeeded)
                return string.Join("; ", remove.Errors.Select(e => e.Description));
        }

        var add = await _userManager.AddToRoleAsync(user, normalized).ConfigureAwait(false);
        if (!add.Succeeded)
            return string.Join("; ", add.Errors.Select(e => e.Description));

        user.Role = normalized;
        user.UpdatedAt = DateTime.UtcNow;
        var update = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!update.Succeeded)
            return string.Join("; ", update.Errors.Select(e => e.Description));

        _ = cancellationToken;
        return null;
    }

    private static bool TryValidateAssignableRole(string? role, out string? error)
    {
        error = null;
        var trimmed = role?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "Role is required.";
            return false;
        }

        if (!AssignableRoles.Contains(trimmed))
        {
            error = $"Role '{trimmed}' cannot be assigned to a tenant user.";
            return false;
        }

        return true;
    }

    private static bool TryValidateInviteRole(string? role, out string? error)
    {
        error = null;
        var trimmed = role?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "Role is required.";
            return false;
        }

        if (!InviteRoles.Contains(trimmed))
        {
            error = $"Role '{trimmed}' cannot be used for tenant invites.";
            return false;
        }

        return true;
    }

    private static string FormatName(ApplicationUser user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? user.UserName ?? user.Id : name;
    }

    private static TenantUserDto ToDto(ApplicationUser user, UserTenantMembership membership) =>
        new(
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            FormatName(user),
            user.Role ?? Roles.FallbackUnknown,
            membership.IsOwner,
            membership.CreatedAtUtc);
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantUserService : ITenantUserService
{
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
    private readonly ILogger<TenantUserService> _logger;

    public TenantUserService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        IUserUniquenessValidationService uniquenessValidation,
        ITenantInvitationEmailSender invitationEmail,
        ILogger<TenantUserService> logger)
    {
        _db = db;
        _userManager = userManager;
        _membershipProvisioner = membershipProvisioner;
        _uniquenessValidation = uniquenessValidation;
        _invitationEmail = invitationEmail;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantUserDto>?> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return null;

        return await _db.UserTenantMemberships
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

    public async Task<(TenantUserDto? Result, string? Error)> AddAsync(
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

        var membership = await _db.UserTenantMemberships
            .AsNoTracking()
            .FirstAsync(m => m.UserId == user.Id && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);

        return (ToDto(user, membership), null);
    }

    public async Task<(TenantUserInviteResultDto? Result, string? Error)> InviteAsync(
        Guid tenantId,
        InviteTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        var email = request.Email.Trim();
        if (string.IsNullOrEmpty(email))
            return (null, "Email is required.");

        if (!TryValidateInviteRole(request.Role, out var roleError))
            return (null, roleError);

        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        string? generatedPassword = null;
        var userCreated = false;

        if (user == null)
        {
            if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(email, excludeUserId: null)
                    .ConfigureAwait(false))
                return (null, $"Email '{email}' is already in use.");

            generatedPassword = TenantProvisioningService.GenerateCompliantPassword();
            var now = DateTime.UtcNow;
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Invited",
                LastName = tenant.Name.Length > 50 ? tenant.Name[..50] : tenant.Name,
                EmployeeNumber = $"INV{Guid.NewGuid():N}"[..20],
                Role = request.Role.Trim(),
                TaxNumber = string.Empty,
                Notes = $"Invited to tenant {tenant.Slug}",
                IsActive = true,
                EmailConfirmed = true,
                AccountType = "Admin",
                IsDemo = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            var create = await _userManager.CreateAsync(user, generatedPassword).ConfigureAwait(false);
            if (!create.Succeeded)
                return (null, string.Join("; ", create.Errors.Select(e => e.Description)));

            var roleAdd = await _userManager.AddToRoleAsync(user, request.Role.Trim()).ConfigureAwait(false);
            if (!roleAdd.Succeeded)
                return (null, string.Join("; ", roleAdd.Errors.Select(e => e.Description)));

            userCreated = true;
            _logger.LogInformation("Created user {Email} for tenant invite {TenantId}", email, tenantId);
        }
        else
        {
            if (string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return (null, "SuperAdmin users cannot be assigned to a tenant via invite.");

            var alreadyMember = await _db.UserTenantMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == user.Id && m.TenantId == tenantId && m.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (alreadyMember)
                return (null, "User is already assigned to this tenant.");
        }

        var assignError = await AssignUserToTenantAsync(user, tenantId, request.Role, request.IsOwner, cancellationToken)
            .ConfigureAwait(false);
        if (assignError != null)
            return (null, assignError);

        var membership = await _db.UserTenantMemberships
            .AsNoTracking()
            .FirstAsync(m => m.UserId == user.Id && m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var portalUrl = BuildTenantPortalUrl(tenant.Slug);
        var subject = $"Einladung: {tenant.Name} – Regkasse Admin";
        var body = BuildInvitationEmailBody(
            tenant.Name,
            tenant.Slug,
            request.Role.Trim(),
            portalUrl,
            userCreated,
            generatedPassword);

        var emailSent = await _invitationEmail
            .TrySendInvitationAsync(email, subject, body, cancellationToken)
            .ConfigureAwait(false);

        string? deliveryNote = null;
        string? passwordForOperator = null;
        if (emailSent)
        {
            deliveryNote = "Invitation email sent.";
            generatedPassword = null;
        }
        else if (!_invitationEmail.IsConfigured)
        {
            deliveryNote = "SMTP is not configured; share login details manually.";
            if (userCreated)
                passwordForOperator = generatedPassword;
        }
        else
        {
            deliveryNote = "User assigned; invitation email could not be delivered.";
            if (userCreated)
                passwordForOperator = generatedPassword;
        }

        return (new TenantUserInviteResultDto(
            ToDto(user, membership),
            userCreated,
            emailSent,
            deliveryNote,
            passwordForOperator), null);
    }

    public async Task<(TenantUserDto? Result, string? Error)> UpdateAsync(
        Guid tenantId,
        string userId,
        UpdateAdminTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant not found.");

        var membership = await _db.UserTenantMemberships
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

    public async Task<(bool Success, string? Error)> RemoveAsync(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return (false, "Tenant not found.");

        var membership = await _db.UserTenantMemberships
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

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);

    private async Task ClearTenantOwnersExceptAsync(Guid tenantId, Guid exceptMembershipId, CancellationToken cancellationToken)
    {
        var others = await _db.UserTenantMemberships
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

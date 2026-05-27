using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class QuickUserGeneratorService : IQuickUserGeneratorService
{
    public const int MaxQuickUsersPerTenantPerHour = 10;
    public const int EmailAllocationMaxAttempts = 12;

    private static readonly HashSet<string> QuickRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Roles.Manager,
        Roles.Cashier,
        Roles.Accountant,
    };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly IUserCreationService _userCreation;

    public QuickUserGeneratorService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserUniquenessValidationService uniquenessValidation,
        IUserCreationService userCreation)
    {
        _db = db;
        _userManager = userManager;
        _uniquenessValidation = uniquenessValidation;
        _userCreation = userCreation;
    }

    public async Task<(QuickUserGenerationPlan? Plan, string? Error)> PrepareAsync(
        Guid tenantId,
        string role,
        string? requestedUserName = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        var normalizedRole = role?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedRole) || !QuickRoles.Contains(normalizedRole))
            return (null, $"Role '{role}' cannot be used for quick user generation.");

        var (allowed, rateError) = await CheckRateLimitAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!allowed)
            return (null, rateError);

        var email = await AllocateUniqueEmailAsync(normalizedRole, tenant.Slug, cancellationToken)
            .ConfigureAwait(false);
        if (email == null)
            return (null, "Could not allocate a unique email for quick user creation. Try again.");

        var (userName, userNameError) = await _userCreation
            .ResolveUsernameAsync(requestedUserName, normalizedRole, cancellationToken)
            .ConfigureAwait(false);
        if (userNameError != null)
            return (null, userNameError);

        var password = PasswordGenerator.GenerateSecurePassword(12);
        return (new QuickUserGenerationPlan(email, password, normalizedRole, tenant.Slug, userName), null);
    }

    internal async Task<(bool Allowed, string? Error)> CheckRateLimitAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        var count = await _db.AuditLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(
                a => a.TenantId == tenantId
                     && a.Action == AuditLogActions.TENANT_QUICK_USER_CREATED
                     && a.Timestamp >= since,
                cancellationToken)
            .ConfigureAwait(false);

        if (count >= MaxQuickUsersPerTenantPerHour)
        {
            return (false,
                $"Quick user rate limit exceeded. Maximum {MaxQuickUsersPerTenantPerHour} quick users per hour per tenant.");
        }

        return (true, null);
    }

    internal async Task<string?> AllocateUniqueEmailAsync(
        string role,
        string tenantSlug,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < EmailAllocationMaxAttempts; attempt++)
        {
            var candidate = QuickUserEmailGenerator.BuildEmail(role, tenantSlug);
            if (!await IsEmailAvailableAsync(candidate, cancellationToken).ConfigureAwait(false))
                continue;

            return candidate;
        }

        return null;
    }

    private async Task<bool> IsEmailAvailableAsync(string email, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing != null)
            return false;

        if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(email, excludeUserId: null)
                .ConfigureAwait(false))
            return false;

        _ = cancellationToken;
        return true;
    }
}

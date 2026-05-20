using System.Security.Claims;
using System.Text.RegularExpressions;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed partial class AdminTenantService : IAdminTenantService
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9][a-z0-9_-]{0,62}[a-z0-9]$|^[a-z0-9]$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenClaimsService _tokenClaimsService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtAccessTokenIssuer _jwtIssuer;
    private readonly AuthOptions _authOptions;
    private readonly ITenantProvisioningService _provisioningService;
    private readonly ILogger<AdminTenantService> _logger;

    public AdminTenantService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenClaimsService tokenClaimsService,
        IRefreshTokenService refreshTokenService,
        IJwtAccessTokenIssuer jwtIssuer,
        IOptions<AuthOptions> authOptions,
        ITenantProvisioningService provisioningService,
        ILogger<AdminTenantService> logger)
    {
        _db = db;
        _userManager = userManager;
        _tokenClaimsService = tokenClaimsService;
        _refreshTokenService = refreshTokenService;
        _jwtIssuer = jwtIssuer;
        _authOptions = authOptions.Value;
        _provisioningService = provisioningService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminTenantListItemDto>> ListAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Tenants.AsNoTracking();
        if (!includeDeleted)
            query = query.Where(t => t.Status != TenantStatuses.Deleted);

        var tenants = await query
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0)
            return Array.Empty<AdminTenantListItemDto>();

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var ownerRows = await _db.UserTenantMemberships
            .AsNoTracking()
            .Where(m => tenantIds.Contains(m.TenantId) && m.IsActive && m.IsOwner)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { m.TenantId, Email = u.Email ?? u.UserName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ownerByTenant = ownerRows
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => g.First().Email);

        return tenants
            .Select(t => ToListItem(
                t,
                ownerByTenant.TryGetValue(t.Id, out var ownerEmail) ? ownerEmail : null))
            .ToList();
    }

    public async Task<AdminTenantDetailDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => ToDetail(t))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<TenantSlugAvailabilityDto> CheckSlugAvailabilityAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSlug(slug);
        if (!TryValidateSlug(normalized, out _))
            return new TenantSlugAvailabilityDto(normalized, IsValid: false, Available: false);

        var taken = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Slug == normalized, cancellationToken)
            .ConfigureAwait(false);

        return new TenantSlugAvailabilityDto(normalized, IsValid: true, Available: !taken);
    }

    public async Task<(AdminTenantDetailDto? Result, string? Error)> CreateAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var slug = NormalizeSlug(request.Slug);
        if (!TryValidateSlug(slug, out var slugError))
            return (null, slugError);

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken).ConfigureAwait(false))
            return (null, "Tenant slug is already in use.");

        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Email = TrimOrNull(request.Email),
            Phone = TrimOrNull(request.Phone),
            Address = TrimOrNull(request.Address),
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = TrimOrNull(request.LicenseKey),
            LicenseValidUntilUtc = request.LicenseValidUntilUtc.HasValue
                ? DateTime.SpecifyKind(request.LicenseValidUntilUtc.Value, DateTimeKind.Utc)
                : null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorUserId,
            UpdatedBy = actorUserId,
        };

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var (provisioning, provisionError) = await _provisioningService
                .ProvisionAsync(
                    tenant,
                    request.AdminEmail,
                    request.AdminPassword,
                    request.GrantTrialLicense,
                    cancellationToken)
                .ConfigureAwait(false);

            if (provisionError != null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (null, provisionError);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Super-admin created tenant {TenantId} slug {Slug}", tenant.Id, tenant.Slug);
            return (ToDetail(tenant, provisioning?.ToDto()), null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Tenant create failed for slug {Slug}", slug);
            return (null, "Tenant creation failed.");
        }
    }

    public async Task<(AdminTenantDetailDto? Result, string? Error)> UpdateAsync(
        Guid tenantId,
        UpdateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be updated.");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (!TenantStatuses.IsKnown(status))
                return (null, "Invalid status. Use active, suspended, or deleted.");
            tenant.Status = status;
            if (status == TenantStatuses.Suspended)
                tenant.IsActive = false;
            else if (status == TenantStatuses.Active)
                tenant.IsActive = true;
        }

        if (request.IsActive.HasValue)
            tenant.IsActive = request.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(request.Name))
            tenant.Name = request.Name.Trim();

        if (request.Email != null)
            tenant.Email = TrimOrNull(request.Email);
        if (request.Phone != null)
            tenant.Phone = TrimOrNull(request.Phone);
        if (request.Address != null)
            tenant.Address = TrimOrNull(request.Address);
        if (request.LicenseKey != null)
            tenant.LicenseKey = TrimOrNull(request.LicenseKey);
        if (request.LicenseValidUntilUtc.HasValue)
            tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(request.LicenseValidUntilUtc.Value, DateTimeKind.Utc);

        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Super-admin updated tenant {TenantId}", tenantId);
        return (ToDetail(tenant), null);
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == LegacyDefaultTenantIds.Primary)
            return (false, "The legacy default tenant cannot be deleted.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (false, "Tenant not found.");

        if (tenant.Status == TenantStatuses.Deleted)
            return (true, null);

        tenant.Status = TenantStatuses.Deleted;
        tenant.IsActive = false;
        tenant.DeletedAtUtc = DateTime.UtcNow;
        tenant.DeletedByUserId = actorUserId;
        tenant.UpdatedAt = tenant.DeletedAtUtc;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning("Super-admin soft-deleted tenant {TenantId} slug {Slug}", tenant.Id, tenant.Slug);
        return (true, null);
    }

    public async Task<(TenantImpersonationResponseDto? Result, string? Error)> ImpersonateAsync(
        Guid tenantId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Cannot impersonate a deleted tenant.");
        if (!tenant.IsActive || tenant.Status == TenantStatuses.Suspended)
            return (null, "Tenant is not active.");

        var user = await _userManager.FindByIdAsync(actorUserId).ConfigureAwait(false);
        if (user == null || !user.IsActive)
            return (null, "Actor user not found or inactive.");

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        if (!roles.Contains(Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Only SuperAdmin may impersonate tenants.");
        }

        var tenantIdStr = tenant.Id.ToString("D");
        var issued = await _refreshTokenService.IssueLoginTokensAsync(
            user.Id,
            ClientAppPolicy.Admin,
            async (_, jti, sessionId, expiresAtUtc, _, _) =>
            {
                var claims = await _tokenClaimsService.BuildClaimsAsync(
                    user,
                    roles,
                    tenantId: tenantIdStr,
                    branchId: null,
                    appContext: ClientAppPolicy.Admin,
                    cancellationToken).ConfigureAwait(false);
                var claimList = claims.ToList();
                claimList.Add(new Claim("tenant_impersonation", "true"));
                return _jwtIssuer.IssueToken(claimList, jti, sessionId, expiresAtUtc);
            },
            sessionTenantId: tenant.Id,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Super-admin {ActorUserId} impersonating tenant {TenantId} ({Slug})",
            actorUserId,
            tenant.Id,
            tenant.Slug);

        return (new TenantImpersonationResponseDto(
            issued.AccessToken,
            Math.Max(60, _authOptions.AccessTokenLifetimeMinutes * 60),
            issued.RefreshToken,
            issued.RefreshTokenExpiresAtUtc,
            tenant.Id,
            tenant.Slug,
            tenant.Name,
            true), null);
    }

    private static string NormalizeSlug(string raw) =>
        raw.Trim().ToLowerInvariant().Replace(' ', '_');

    private static bool TryValidateSlug(string slug, out string? error)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            error = "Slug is required.";
            return false;
        }

        if (!SlugRegex.IsMatch(slug))
        {
            error = "Slug must be lowercase alphanumeric with optional hyphens/underscores.";
            return false;
        }

        error = null;
        return true;
    }

    private static string? TrimOrNull(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static AdminTenantListItemDto ToListItem(Tenant t, string? ownerAdminEmail = null) =>
        new(
            t.Id,
            t.Name,
            t.Slug,
            t.Email,
            t.Phone,
            t.Status,
            t.IsActive,
            t.LicenseKey,
            t.LicenseValidUntilUtc,
            t.CreatedAt,
            t.UpdatedAt,
            ownerAdminEmail,
            DemoTenantIds.IsDemoPresetSlug(t.Slug));

    private static AdminTenantDetailDto ToDetail(Tenant t, TenantProvisioningDto? provisioning = null) =>
        new(
            t.Id,
            t.Name,
            t.Slug,
            t.Email,
            t.Phone,
            t.Address,
            t.Status,
            t.IsActive,
            t.LicenseKey,
            t.LicenseValidUntilUtc,
            t.CreatedAt,
            t.UpdatedAt,
            t.DeletedAtUtc,
            provisioning);
}

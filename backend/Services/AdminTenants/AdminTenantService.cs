using System.Security.Claims;
using System.Text.RegularExpressions;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
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
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ITenantService _tenantService;
    private readonly ICashRegisterDecommissionService _cashRegisterDecommissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<AdminTenantService> _logger;

    public AdminTenantService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenClaimsService tokenClaimsService,
        IRefreshTokenService refreshTokenService,
        IJwtAccessTokenIssuer jwtIssuer,
        IOptions<AuthOptions> authOptions,
        ITenantOnboardingService onboardingService,
        ITenantService tenantService,
        ICashRegisterDecommissionService cashRegisterDecommissionService,
        IHttpContextAccessor httpContextAccessor,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<AdminTenantService> logger)
    {
        _db = db;
        _userManager = userManager;
        _tokenClaimsService = tokenClaimsService;
        _refreshTokenService = refreshTokenService;
        _jwtIssuer = jwtIssuer;
        _authOptions = authOptions.Value;
        _onboardingService = onboardingService;
        _tenantService = tenantService;
        _cashRegisterDecommissionService = cashRegisterDecommissionService;
        _httpContextAccessor = httpContextAccessor;
        _tenantAccessor = tenantAccessor;
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
            .IgnoreQueryFilters()
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

    public async Task<IReadOnlyList<AdminTenantListItemDto>> ListForSwitcherAsync(
        string? actorUserId,
        bool actorIsSuperAdmin,
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsSuperAdmin)
            includeDeleted = false;

        // Super Admin (or anonymous actor): all tenants from Tenants table only — no membership join.
        if (actorIsSuperAdmin || string.IsNullOrWhiteSpace(actorUserId))
        {
            var all = await ListAsync(includeDeleted, cancellationToken).ConfigureAwait(false);
            return DeduplicateSwitcherItems(all);
        }

        var items = await ListAsync(includeDeleted: false, cancellationToken).ConfigureAwait(false);

        var memberTenantIds = await _db.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == actorUserId && m.IsActive)
            .Join(
                _db.Tenants.AsNoTracking(),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new { m.TenantId, t.Status, t.IsActive })
            .Where(x =>
                x.IsActive
                && !string.Equals(x.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (memberTenantIds.Count == 0)
        {
            return Array.Empty<AdminTenantListItemDto>();
        }

        var allowed = memberTenantIds.ToHashSet();
        return DeduplicateSwitcherItems(items.Where(t => allowed.Contains(t.Id)));
    }

    /// <summary>Guards switcher API against duplicate tenant ids (defensive; ListAsync is already one row per tenant).</summary>
    private static List<AdminTenantListItemDto> DeduplicateSwitcherItems(
        IEnumerable<AdminTenantListItemDto> items) =>
        items.DistinctBy(t => t.Id).ToList();

    public async Task<AdminTenantDetailDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return null;

        var activeUserCount = await _db.UserTenantMemberships
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var registerStats = await _db.CashRegisters
            .AsNoTracking()
            .Where(cr => cr.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                LastUsed = g.Max(cr => (DateTime?)cr.LastBalanceUpdate),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastReceiptAt = await _db.Receipts.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .MaxAsync(r => (DateTime?)r.IssuedAt, cancellationToken)
            .ConfigureAwait(false);

        var ownerEmail = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive && m.IsOwner)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (_, u) => u.Email ?? u.UserName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var lastActivity = MaxUtc(
            tenant.UpdatedAt,
            registerStats?.LastUsed,
            lastReceiptAt);

        return ToDetail(
            tenant,
            provisioning: null,
            ownerAdminEmail: ownerEmail,
            activeUserCount: activeUserCount,
            cashRegisterCount: registerStats?.Count ?? 0,
            lastActivityAtUtc: lastActivity);
    }

    public async Task<IReadOnlyList<AdminTenantCashRegisterDto>?> ListCashRegistersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false))
            return null;

        return await _db.CashRegisters
            .AsNoTracking()
            .Where(cr => cr.TenantId == tenantId)
            .OrderBy(cr => cr.RegisterNumber)
            .Select(cr => new AdminTenantCashRegisterDto(
                cr.Id,
                cr.RegisterNumber,
                cr.Location,
                cr.Status.ToString(),
                cr.IsActive,
                cr.LastBalanceUpdate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TenantDecommissionChecksDto?> GetDecommissionChecksAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantExists = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            return null;

        var registerStats = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(cr => cr.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ActiveRegistersCount = g.Count(cr => cr.Status != RegisterStatus.Decommissioned),
                ReadyRegistersCount = g.Count(cr => cr.Status == RegisterStatus.Closed),
                BlockedRegistersCount = g.Count(cr =>
                    cr.Status != RegisterStatus.Closed && cr.Status != RegisterStatus.Decommissioned),
                HasOpenShifts = g.Any(cr => cr.Status == RegisterStatus.Open),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasOpenPayments = await _db.PaymentDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _db.CashRegisters.IgnoreQueryFilters().AsNoTracking(),
                p => p.CashRegisterId,
                cr => cr.Id,
                (p, cr) => new { Payment = p, Register = cr })
            .AnyAsync(
                x =>
                    x.Register.TenantId == tenantId
                    && x.Payment.IsActive
                    && !x.Payment.IsRefund
                    && !x.Payment.IsStorno
                    && x.Payment.FinanzOnlineStatus != null
                    && (x.Payment.FinanzOnlineStatus == "Pending"
                        || x.Payment.FinanzOnlineStatus == "NeedsReconciliation"),
                cancellationToken)
            .ConfigureAwait(false);

        var activeRegistersCount = registerStats?.ActiveRegistersCount ?? 0;
        var readyRegistersCount = registerStats?.ReadyRegistersCount ?? 0;
        var blockedRegistersCount = registerStats?.BlockedRegistersCount ?? 0;
        var hasOpenShifts = registerStats?.HasOpenShifts ?? false;

        return new TenantDecommissionChecksDto(
            HasOpenPayments: hasOpenPayments,
            HasOpenShifts: hasOpenShifts,
            ActiveRegistersCount: activeRegistersCount,
            ReadyRegistersCount: readyRegistersCount,
            BlockedRegistersCount: blockedRegistersCount,
            CanDecommission: !hasOpenPayments && !hasOpenShifts && blockedRegistersCount == 0);
    }

    public Task<IReadOnlyList<string>> GetSlugSuggestionsAsync(
        string? companyName,
        string? preferredSlug,
        int maxCount = 5,
        CancellationToken cancellationToken = default) =>
        _onboardingService.GetSlugSuggestionsAsync(companyName, preferredSlug, maxCount, cancellationToken);

    public async Task<TenantSlugAvailabilityDto> CheckSlugAvailabilityAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalized = TenantSlugSuggestions.NormalizeSlug(slug);
        if (!TenantSlugSuggestions.IsValidSlug(normalized))
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
        var (result, failure) = await _onboardingService
            .CreateAsync(request, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        return failure == null ? (result, null) : (null, failure.Message);
    }

    public async Task<(AdminTenantDetailDto? Result, TenantOnboardingFailureDto? Failure)> CreateWithFailureDetailAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default) =>
        await _onboardingService.CreateAsync(request, actorUserId, cancellationToken).ConfigureAwait(false);

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

    public Task<(bool Success, string? Error)> HardDeleteAsync(
        Guid tenantId,
        HardDeleteAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default) =>
        _tenantService.HardDeleteAsync(tenantId, request, actorUserId, cancellationToken);

    public Task<(bool Success, string? Error)> SoftDeleteAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default) =>
        _tenantService.SoftDeleteAsync(tenantId, actorUserId, cancellationToken);

    public Task<(bool Success, string? Error)> RestoreAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default) =>
        _tenantService.RestoreAsync(tenantId, actorUserId, cancellationToken);

    public async Task<(bool Success, string? Error, TenantDecommissionChecksDto? Checks)> DecommissionAsync(
        Guid tenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return (false, "Actor user is required.", null);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (false, "Tenant not found.", null);

        if (tenant.Status == TenantStatuses.Deleted)
            return (false, "Tenant is already deleted.", null);

        var checks = await GetDecommissionChecksAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (checks == null)
            return (false, "Tenant not found.", null);
        if (!checks.CanDecommission)
            return (false, "Tenant decommission preflight checks are not satisfied.", checks);

        var registerIds = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(cr => cr.TenantId == tenantId && cr.Status != RegisterStatus.Decommissioned)
            .OrderBy(cr => cr.RegisterNumber)
            .Select(cr => cr.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var registerId in registerIds)
        {
            await RunInTenantScopeAsync(
                tenantId,
                async () =>
                {
                    await _cashRegisterDecommissionService
                        .DecommissionAsync(
                            registerId,
                            "Tenant decommission",
                            actorUserId,
                            actorRole,
                            cancellationToken)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        var (success, error) = await _tenantService
            .SoftDeleteAsync(tenantId, actorUserId, cancellationToken)
            .ConfigureAwait(false);

        return (success, error, checks);
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

    private async Task RunInTenantScopeAsync(Guid tenantId, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Tenant-scoped admin decommission requires an active HTTP context.");
        var originalUser = httpContext.User;
        var originalTenantId = _tenantAccessor.TenantId;

        httpContext.User = CreateTenantScopedPrincipal(originalUser, tenantId);
        _tenantAccessor.TenantId = tenantId;

        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            httpContext.User = originalUser;
            _tenantAccessor.TenantId = originalTenantId;
        }
    }

    private static ClaimsPrincipal CreateTenantScopedPrincipal(ClaimsPrincipal principal, Guid tenantId)
    {
        var clone = new ClaimsPrincipal(principal.Identities.Select(identity => new ClaimsIdentity(identity)));
        foreach (var identity in clone.Identities)
        {
            var existingTenantClaims = identity.FindAll(ScopeCheckService.TenantIdClaim).ToList();
            foreach (var claim in existingTenantClaims)
                identity.RemoveClaim(claim);
        }

        var targetIdentity = clone.Identities.FirstOrDefault(identity => identity.IsAuthenticated)
            ?? clone.Identities.FirstOrDefault();

        if (targetIdentity == null)
        {
            targetIdentity = new ClaimsIdentity(authenticationType: "AdminTenantDecommission");
            clone.AddIdentity(targetIdentity);
        }

        targetIdentity.AddClaim(new Claim(ScopeCheckService.TenantIdClaim, tenantId.ToString("D")));
        return clone;
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

    private static AdminTenantListItemDto ToListItem(Tenant t, string? ownerAdminEmail = null)
    {
        var (licenseDaysRemaining, _) = TenantLicenseStatusMapper.ComputeKindAndDays(
            t.LicenseValidUntilUtc,
            t.LicenseKey);
        return new(
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
            licenseDaysRemaining,
            ownerAdminEmail,
            DemoTenantIds.IsDemoPresetSlug(t.Slug));
    }

    private static AdminTenantDetailDto ToDetail(
        Tenant t,
        TenantProvisioningDto? provisioning = null,
        string? ownerAdminEmail = null,
        int activeUserCount = 0,
        int cashRegisterCount = 0,
        DateTime? lastActivityAtUtc = null) =>
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
            ownerAdminEmail,
            activeUserCount,
            cashRegisterCount,
            lastActivityAtUtc,
            provisioning);

    private static DateTime? MaxUtc(params DateTime?[] values)
    {
        DateTime? max = null;
        foreach (var v in values)
        {
            if (!v.HasValue)
                continue;
            if (!max.HasValue || v.Value > max.Value)
                max = v;
        }

        return max;
    }

}

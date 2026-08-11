using System.Security.Claims;
using System.Text.RegularExpressions;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Tenancy;
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
    private readonly ITenantOnboardingService _onboardingService;
    private readonly ITenantService _tenantService;
    private readonly ITenantDeletionService _tenantDeletion;
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
        ITenantDeletionService tenantDeletion,
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
        _tenantDeletion = tenantDeletion;
        _cashRegisterDecommissionService = cashRegisterDecommissionService;
        _httpContextAccessor = httpContextAccessor;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminTenantListItemDto>> ListAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildEnrichedListAsync(
                includeDeleted,
                status: null,
                search: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedResult<AdminTenantListItemDto>> ListPagedAsync(
        AdminTenantListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);

        IReadOnlyList<AdminTenantListItemDto> items = await BuildEnrichedListAsync(
                query.IncludeDeleted,
                query.Status,
                query.Search,
                cancellationToken)
            .ConfigureAwait(false);

        if (query.LicenseType.HasValue)
        {
            var licenseType = query.LicenseType.Value;
            items = items
                .Where(t => t.LicenseType == licenseType)
                .ToList();
        }

        items = ApplyListSort(items, query.SortBy, query.SortOrder);

        var totalCount = items.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<AdminTenantListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
    }

    public async Task<IReadOnlyList<AdminTenantListItemDto>> ListForExportAsync(
        AdminTenantListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<AdminTenantListItemDto> items = await BuildEnrichedListAsync(
                query.IncludeDeleted,
                query.Status,
                query.Search,
                cancellationToken)
            .ConfigureAwait(false);

        if (query.LicenseType.HasValue)
        {
            var licenseType = query.LicenseType.Value;
            items = items
                .Where(t => t.LicenseType == licenseType)
                .ToList();
        }

        return ApplyListSort(items, query.SortBy, query.SortOrder);
    }

    private async Task<IReadOnlyList<AdminTenantListItemDto>> BuildEnrichedListAsync(
        bool includeDeleted,
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = _db.Tenants.AsNoTracking();
        if (!includeDeleted)
            query = query.Where(t => !TenantStatuses.RemovedStatuses.Contains(t.Status));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusFilter = TenantStatuses.Normalize(status);
            if (statusFilter.Length > 0)
            {
                // Legacy "deleted" normalizes to archived; also match leftover deleted rows.
                if (statusFilter == TenantStatuses.Archived)
                {
                    query = query.Where(t =>
                        t.Status == TenantStatuses.Archived || t.Status == TenantStatuses.Deleted);
                }
                else
                {
                    query = query.Where(t => t.Status == statusFilter);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) || t.Slug.ToLower().Contains(term));
        }

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

        var userCountRows = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => tenantIds.Contains(m.TenantId) && m.IsActive)
            .GroupBy(m => m.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registerCountRows = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(cr => tenantIds.Contains(cr.TenantId))
            .GroupBy(cr => cr.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeSales = await _db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId) && s.Status == LicenseSaleStatuses.Active)
            .Select(s => new
            {
                s.TenantId,
                s.LicenseType,
                s.ValidUntilUtc,
                s.SoldAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentActivityRows = await _db.PaymentDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _db.CashRegisters.IgnoreQueryFilters().AsNoTracking(),
                p => p.CashRegisterId,
                cr => cr.Id,
                (p, cr) => new { cr.TenantId, p.CreatedAt })
            .Where(x => tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, MaxAt = g.Max(x => (DateTime?)x.CreatedAt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var auditActivityRows = await _db.AuditLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => tenantIds.Contains(a.TenantId))
            .GroupBy(a => a.TenantId)
            .Select(g => new { TenantId = g.Key, MaxAt = g.Max(a => (DateTime?)a.Timestamp) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ownerByTenant = ownerRows
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => g.First().Email);

        var userCountByTenant = userCountRows.ToDictionary(x => x.TenantId, x => x.Count);
        var registerCountByTenant = registerCountRows.ToDictionary(x => x.TenantId, x => x.Count);

        var latestSaleByTenant = activeSales
            .GroupBy(s => s.TenantId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.SoldAtUtc)
                    .ThenByDescending(s => s.ValidUntilUtc)
                    .First());

        var lastActivityByTenant = new Dictionary<Guid, DateTime?>();
        foreach (var row in paymentActivityRows)
            lastActivityByTenant[row.TenantId] = row.MaxAt;
        foreach (var row in auditActivityRows)
        {
            lastActivityByTenant.TryGetValue(row.TenantId, out var existing);
            lastActivityByTenant[row.TenantId] = MaxUtc(existing, row.MaxAt);
        }

        return tenants
            .Select(t =>
            {
                latestSaleByTenant.TryGetValue(t.Id, out var sale);
                return ToListItem(
                    t,
                    ownerByTenant.TryGetValue(t.Id, out var ownerEmail) ? ownerEmail : null,
                    hasActiveSale: sale != null,
                    sale?.LicenseType,
                    sale?.ValidUntilUtc,
                    registerCountByTenant.GetValueOrDefault(t.Id),
                    userCountByTenant.GetValueOrDefault(t.Id),
                    lastActivityByTenant.TryGetValue(t.Id, out var lastActivity) ? lastActivity : null);
            })
            .ToList();
    }

    private static IReadOnlyList<AdminTenantListItemDto> ApplyListSort(
        IReadOnlyList<AdminTenantListItemDto> items,
        string? sortBy,
        string? sortOrder)
    {
        var descending = !string.Equals(sortOrder?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
        var key = (sortBy ?? "CreatedAt").Trim();

        IOrderedEnumerable<AdminTenantListItemDto> ordered = key.ToLowerInvariant() switch
        {
            "name" => descending
                ? items.OrderByDescending(t => t.Name, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase),
            "registercount" => descending
                ? items.OrderByDescending(t => t.RegisterCount)
                : items.OrderBy(t => t.RegisterCount),
            "usercount" => descending
                ? items.OrderByDescending(t => t.UserCount)
                : items.OrderBy(t => t.UserCount),
            "licensedaysleft" or "licensedaysremaining" => descending
                ? items.OrderByDescending(t => t.LicenseDaysRemaining.HasValue)
                    .ThenByDescending(t => t.LicenseDaysRemaining)
                : items.OrderBy(t => t.LicenseDaysRemaining.HasValue)
                    .ThenBy(t => t.LicenseDaysRemaining),
            "lastactivity" or "lastactivityatutc" => descending
                ? items.OrderByDescending(t => t.LastActivityAtUtc.HasValue)
                    .ThenByDescending(t => t.LastActivityAtUtc)
                : items.OrderBy(t => t.LastActivityAtUtc.HasValue)
                    .ThenBy(t => t.LastActivityAtUtc),
            _ => descending
                ? items.OrderByDescending(t => t.CreatedAt)
                : items.OrderBy(t => t.CreatedAt),
        };

        return ordered.ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
            return ExcludeUnusedDefaultTenant(DeduplicateSwitcherItems(all));
        }

        var items = await ListAsync(includeDeleted: false, cancellationToken).ConfigureAwait(false);

        var memberTenantIds = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.UserId == actorUserId && m.IsActive)
            .Join(
                _db.Tenants.AsNoTracking(),
                m => m.TenantId,
                t => t.Id,
                (m, t) => new { m.TenantId, t.Status, t.IsActive })
            .Where(x =>
                x.IsActive
                && !TenantStatuses.RemovedStatuses.Contains(x.Status))
            .Select(x => x.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (memberTenantIds.Count == 0)
        {
            return Array.Empty<AdminTenantListItemDto>();
        }

        var allowed = memberTenantIds.ToHashSet();
        return ExcludeUnusedDefaultTenant(DeduplicateSwitcherItems(items.Where(t => allowed.Contains(t.Id))));
    }

    /// <summary>Guards switcher API against duplicate tenant ids (defensive; ListAsync is already one row per tenant).</summary>
    private static List<AdminTenantListItemDto> DeduplicateSwitcherItems(
        IEnumerable<AdminTenantListItemDto> items) =>
        items.DistinctBy(t => t.Id).ToList();

    /// <summary>
    /// Platform sentinel is excluded from switcher (not a business mandant).
    /// </summary>
    private static IReadOnlyList<AdminTenantListItemDto> ExcludeUnusedDefaultTenant(
        IEnumerable<AdminTenantListItemDto> items) =>
        items
            .Where(t => !SystemTenantIds.IsPlatformSlug(t.Slug))
            .ToList();

    public async Task<AdminTenantDetailDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return null;

        var activeUserCount = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken)
            .ConfigureAwait(false);

        var registerStats = await _db.CashRegisters
            .IgnoreQueryFilters()
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

        var lastReceiptAt = await _db.Receipts.IgnoreQueryFilters().AsNoTracking()
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
            .IgnoreQueryFilters()
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

        if (TenantStatuses.IsRemoved(tenant.Status))
            return (null, "Deleted tenants cannot be updated.");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TenantStatuses.Normalize(request.Status);
            if (!TenantStatuses.IsKnown(status))
            {
                return (null,
                    "Invalid status. Use lead, in_onboarding, active, suspended, cancelled, or archived.");
            }

            tenant.Status = status;
            if (TenantStatuses.IsRemoved(status) || status == TenantStatuses.Suspended)
            {
                tenant.IsActive = false;
                if (TenantStatuses.IsRemoved(status))
                {
                    tenant.DeletedAtUtc ??= DateTime.UtcNow;
                    tenant.DeletedByUserId ??= actorUserId;
                }
            }
            else if (status is TenantStatuses.Active or TenantStatuses.InOnboarding or TenantStatuses.Lead)
            {
                tenant.IsActive = true;
                tenant.DeletedAtUtc = null;
                tenant.DeletedByUserId = null;
            }
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

    public async Task<(AdminTenantDetailDto? Result, string? Error)> UpdateOperationModeAsync(
        Guid tenantId,
        UpdateTenantOperationModeRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return (null, "Tenant not found.");

        if (TenantStatuses.IsRemoved(tenant.Status))
            return (null, "Deleted tenants cannot change operation mode.");

        var mode = TenantOperationModes.Normalize(request.OperationMode);
        if (!TenantOperationModes.IsKnown(request.OperationMode))
            return (null, "Invalid operation mode. Use active, readonly, or maintenance.");

        var now = DateTime.UtcNow;
        DateTime? started = request.MaintenanceStartedAt.HasValue
            ? DateTime.SpecifyKind(request.MaintenanceStartedAt.Value, DateTimeKind.Utc)
            : null;
        DateTime? ends = request.MaintenanceEndsAt.HasValue
            ? DateTime.SpecifyKind(request.MaintenanceEndsAt.Value, DateTimeKind.Utc)
            : null;

        if (mode == TenantOperationModes.Maintenance)
        {
            var effectiveStarted = started ?? tenant.MaintenanceStartedAt ?? now;
            if (ends is DateTime endUtc && endUtc <= effectiveStarted)
                return (null, "maintenanceEndsAt must be after maintenanceStartedAt.");

            tenant.OperationMode = mode;
            tenant.MaintenanceMessage = string.IsNullOrWhiteSpace(request.MaintenanceMessage)
                ? null
                : request.MaintenanceMessage.Trim();
            tenant.MaintenanceStartedAt = effectiveStarted;
            tenant.MaintenanceEndsAt = ends;
        }
        else
        {
            tenant.OperationMode = mode;
            tenant.MaintenanceMessage = null;
            tenant.MaintenanceStartedAt = null;
            tenant.MaintenanceEndsAt = null;
        }

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Super-admin set tenant {TenantId} operation mode to {Mode}",
            tenantId,
            mode);
        return (ToDetail(tenant), null);
    }

    public async Task<TenantDeleteDependenciesDto?> GetDeleteDependenciesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tenantDeletion
                .GetDependencySummaryAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public Task<TenantPermanentDeleteResult> HardDeleteAsync(
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

        if (TenantStatuses.IsRemoved(tenant.Status))
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
        if (TenantStatuses.IsRemoved(tenant.Status))
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
            clientMetadata: null,
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

    private static string? TrimOrNull(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static AdminTenantListItemDto ToListItem(
        Tenant t,
        string? ownerAdminEmail = null,
        bool hasActiveSale = false,
        LicenseType? activeSaleLicenseType = null,
        DateTime? activeSaleValidUntilUtc = null,
        int registerCount = 0,
        int userCount = 0,
        DateTime? lastActivityAtUtc = null)
    {
        var licenseUntil = activeSaleValidUntilUtc ?? t.LicenseValidUntilUtc;
        var (licenseDaysRemaining, _) = TenantLicenseStatusMapper.ComputeKindAndDays(
            licenseUntil,
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
            DemoTenantIds.IsDemoPresetSlug(t.Slug),
            ResolveListLicenseType(hasActiveSale, activeSaleLicenseType),
            registerCount,
            userCount,
            lastActivityAtUtc);
    }

    /// <summary>
    /// Active sale → stored tier (Starter fallback); otherwise Trial.
    /// </summary>
    private static LicenseType ResolveListLicenseType(
        bool hasActiveSale,
        LicenseType? saleLicenseType)
    {
        if (hasActiveSale)
            return saleLicenseType ?? LicenseType.Starter;

        return LicenseType.Trial;
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
            provisioning,
            t.OperationMode,
            t.MaintenanceMessage,
            t.MaintenanceStartedAt,
            t.MaintenanceEndsAt);

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

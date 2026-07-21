using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.DigitalServices;

/// <summary>
/// Super Admin lifecycle for per-tenant website/app gates (<see cref="TenantServiceStatus"/>).
/// Uses the request-scoped <see cref="AppDbContext"/> (not <see cref="IDbContextFactory{TContext}"/>)
/// so <see cref="KasseAPI_Final.Tenancy.ICurrentTenantAccessor"/> resolves correctly.
/// </summary>
public sealed class TenantServiceStatusService : ITenantServiceStatusService
{
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string InvalidServiceTypeCode = "INVALID_SERVICE_TYPE";
    public const string InvalidPriceCode = "INVALID_PRICE";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IDigitalServicePricingService _pricing;
    private readonly IBillingAuditService? _audit;
    private readonly TimeProvider _time;
    private readonly ILogger<TenantServiceStatusService> _logger;

    public TenantServiceStatusService(
        AppDbContext db,
        IDigitalServicePricingService pricing,
        TimeProvider time,
        ILogger<TenantServiceStatusService> logger,
        IBillingAuditService? audit = null)
    {
        _db = db;
        _pricing = pricing;
        _time = time;
        _logger = logger;
        _audit = audit;
    }

    public async Task<IReadOnlyList<TenantDigitalServiceRowDto>> ListTenantStatusesAsync(
        CancellationToken ct = default)
    {
        // Super Admin management list: all non-deleted tenants (Tenants are not ITenantEntity-filtered).
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => t.DeletedAtUtc == null)
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToListAsync(ct);

        var tenantIds = tenants.Select(t => t.Id).ToList();
        // Cross-tenant status rows — bypass ambient tenant query filter.
        var statuses = await _db.TenantServiceStatuses.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => tenantIds.Contains(s.TenantId))
            .ToListAsync(ct);

        var byTenant = statuses
            .GroupBy(s => s.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return tenants.Select(t =>
        {
            byTenant.TryGetValue(t.Id, out var rows);
            rows ??= [];
            return new TenantDigitalServiceRowDto
            {
                TenantId = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Website = MapState(rows, TenantServiceTypes.Website),
                App = MapState(rows, TenantServiceTypes.App),
            };
        }).ToList();
    }

    public async Task<TenantDigitalServiceRowDto?> GetForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, ct);
        if (tenant is null)
            return null;

        return await MapTenantRowAsync(tenant, ct);
    }

    public async Task<bool> IsServiceAvailableAsync(
        Guid tenantId,
        string serviceType,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
            return false;

        var row = await GetForTenantAsync(tenantId, ct);
        if (row is null)
            return false;

        var normalized = serviceType.Trim().ToLowerInvariant();
        return normalized == TenantServiceTypes.Website
            ? row.Website.IsAvailable
            : row.App.IsAvailable;
    }

    public async Task<TenantDigitalServiceMutationResponseDto> SetActiveAsync(
        Guid tenantId,
        string serviceType,
        bool active,
        string? actorUserId,
        string? reason,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
        {
            return Fail(InvalidServiceTypeCode, "ServiceType must be 'website' or 'app'.");
        }

        var normalized = serviceType.Trim().ToLowerInvariant();

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, ct);
        if (tenant is null)
            return Fail(TenantNotFoundCode, "Tenant not found.");

        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        var now = _time.GetUtcNow().UtcDateTime;

        if (active)
        {
            row.IsActive = true;
            row.ActivatedAt = now;
            row.DeactivatedAt = null;
            row.DeactivatedByUserId = null;
            row.DeactivationReason = null;
        }
        else
        {
            row.IsActive = false;
            row.DeactivatedAt = now;
            row.DeactivatedByUserId = actorUserId;
            row.DeactivationReason = string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim();
        }

        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} digital service {ServiceType} set active={Active} by {Actor}",
            tenantId,
            normalized,
            active,
            actorUserId ?? "(unknown)");

        await WriteAuditAsync(
            active
                ? BillingAuditEventTypes.DigitalServiceActivated
                : BillingAuditEventTypes.DigitalServiceDeactivated,
            actorUserId,
            tenantId,
            new
            {
                serviceType = normalized,
                active,
                reason = row.DeactivationReason
            },
            ct);

        return new TenantDigitalServiceMutationResponseDto
        {
            Succeeded = true,
            Tenant = await MapTenantRowAsync(tenant, ct),
        };
    }

    public async Task<TenantDigitalServiceMutationResponseDto> SetEnabledAsync(
        Guid tenantId,
        string serviceType,
        bool enabled,
        string? actorUserId,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
        {
            return Fail(InvalidServiceTypeCode, "ServiceType must be 'website' or 'app'.");
        }

        var normalized = serviceType.Trim().ToLowerInvariant();

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, ct);
        if (tenant is null)
            return Fail(TenantNotFoundCode, "Tenant not found.");

        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        row.IsEnabled = enabled;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} digital service {ServiceType} set enabled={Enabled} by {Actor}",
            tenantId,
            normalized,
            enabled,
            actorUserId ?? "(unknown)");

        await WriteAuditAsync(
            enabled
                ? BillingAuditEventTypes.DigitalServiceEnabled
                : BillingAuditEventTypes.DigitalServiceDisabled,
            actorUserId,
            tenantId,
            new { serviceType = normalized, enabled },
            ct);

        return new TenantDigitalServiceMutationResponseDto
        {
            Succeeded = true,
            Tenant = await MapTenantRowAsync(tenant, ct),
        };
    }

    public async Task<TenantDigitalServiceMutationResponseDto> SetCustomPriceAsync(
        Guid tenantId,
        string serviceType,
        decimal? customPrice,
        string? actorUserId,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
        {
            return Fail(InvalidServiceTypeCode, "ServiceType must be 'website' or 'app'.");
        }

        if (customPrice is < 0)
            return Fail(InvalidPriceCode, "CustomPrice must be null or >= 0.");

        var normalized = serviceType.Trim().ToLowerInvariant();

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, ct);
        if (tenant is null)
            return Fail(TenantNotFoundCode, "Tenant not found.");

        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        row.CustomPrice = customPrice;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tenant {TenantId} digital service {ServiceType} custom price set to {Price} by {Actor}",
            tenantId,
            normalized,
            customPrice,
            actorUserId ?? "(unknown)");

        await WriteAuditAsync(
            BillingAuditEventTypes.DigitalServicePriceUpdated,
            actorUserId,
            tenantId,
            new { serviceType = normalized, customPrice },
            ct);

        return new TenantDigitalServiceMutationResponseDto
        {
            Succeeded = true,
            Tenant = await MapTenantRowAsync(tenant, ct),
        };
    }

    public async Task MarkRequestPendingAsync(
        Guid tenantId,
        string serviceType,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
            return;

        var normalized = serviceType.Trim().ToLowerInvariant();
        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = TenantDigitalServiceStatuses.Pending;
        row.RequestedAt = now;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkRequestRejectedAsync(
        Guid tenantId,
        string serviceType,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
            return;

        var normalized = serviceType.Trim().ToLowerInvariant();
        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = TenantDigitalServiceStatuses.Rejected;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearPendingRequestAsync(
        Guid tenantId,
        string serviceType,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
            return;

        var normalized = serviceType.Trim().ToLowerInvariant();
        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        if (!string.Equals(row.Status, TenantDigitalServiceStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return;

        var now = _time.GetUtcNow().UtcDateTime;
        // Restore prior artifact state: published > created > none.
        row.Status = row.PublishedAt.HasValue
            ? TenantDigitalServiceStatuses.Published
            : row.ArtifactCreatedAt.HasValue
                ? TenantDigitalServiceStatuses.Created
                : TenantDigitalServiceStatuses.None;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkCreatedAsync(
        Guid tenantId,
        string serviceType,
        string? url,
        string? templateId,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
            return;

        var normalized = serviceType.Trim().ToLowerInvariant();
        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = TenantDigitalServiceStatuses.Created;
        row.ArtifactCreatedAt = now;
        if (!string.IsNullOrWhiteSpace(url))
            row.Url = url.Trim();
        if (!string.IsNullOrWhiteSpace(templateId))
            row.TemplateId = templateId.Trim();
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkPublishedAsync(
        Guid tenantId,
        string serviceType,
        string? url,
        CancellationToken ct = default)
    {
        if (!TenantServiceTypes.IsValid(serviceType))
            return;

        var normalized = serviceType.Trim().ToLowerInvariant();
        var row = await GetOrCreateAsync(tenantId, normalized, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = TenantDigitalServiceStatuses.Published;
        row.PublishedAt = now;
        row.ArtifactCreatedAt ??= now;
        if (!string.IsNullOrWhiteSpace(url))
            row.Url = url.Trim();
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    private async Task WriteAuditAsync(
        string action,
        string? actorUserId,
        Guid tenantId,
        object details,
        CancellationToken ct)
    {
        if (_audit is null)
            return;

        if (!Guid.TryParse(actorUserId, out var actorGuid) || actorGuid == Guid.Empty)
            return;

        await _audit.LogAsync(
            action,
            actorGuid,
            tenantId,
            saleId: null,
            details: JsonSerializer.Serialize(details, JsonOptions),
            ct: ct);
    }

    private async Task<TenantServiceStatus> GetOrCreateAsync(
        Guid tenantId,
        string serviceType,
        CancellationToken ct)
    {
        var existing = await _db.TenantServiceStatuses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.ServiceType == serviceType,
                ct);
        if (existing is not null)
            return existing;

        var now = _time.GetUtcNow().UtcDateTime;
        var created = new TenantServiceStatus
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceType = serviceType,
            IsEnabled = true,
            IsActive = true,
            Status = TenantDigitalServiceStatuses.None,
            ActivatedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.TenantServiceStatuses.Add(created);
        return created;
    }

    private async Task<TenantDigitalServiceRowDto> MapTenantRowAsync(
        Tenant tenant,
        CancellationToken ct)
    {
        var rows = await _db.TenantServiceStatuses.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenant.Id)
            .ToListAsync(ct);

        return new TenantDigitalServiceRowDto
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Website = MapState(rows, TenantServiceTypes.Website),
            App = MapState(rows, TenantServiceTypes.App),
        };
    }

    private TenantDigitalServiceStateDto MapState(
        IReadOnlyList<TenantServiceStatus> rows,
        string serviceType)
    {
        var row = rows.FirstOrDefault(r =>
            string.Equals(r.ServiceType, serviceType, StringComparison.OrdinalIgnoreCase));
        var listPrice = GetDefaultListPrice(serviceType);
        var custom = row?.CustomPrice;
        var isEnabled = row?.IsEnabled ?? true;
        var isActive = row?.IsActive ?? true;

        return new TenantDigitalServiceStateDto
        {
            ServiceType = serviceType,
            IsEnabled = isEnabled,
            IsActive = isActive,
            IsAvailable = isEnabled && isActive,
            Status = row?.Status ?? TenantDigitalServiceStatuses.None,
            HasRequest = row?.HasRequest ?? false,
            Url = row?.Url,
            TemplateId = row?.TemplateId,
            Customization = row?.Customization,
            RequestedAt = row?.RequestedAt,
            ArtifactCreatedAt = row?.ArtifactCreatedAt,
            PublishedAt = row?.PublishedAt,
            CustomPrice = custom,
            ListPrice = listPrice,
            Price = custom ?? listPrice,
            Currency = "EUR",
            ActivatedAt = row?.ActivatedAt,
            DeactivatedAt = row?.DeactivatedAt,
            DeactivationReason = row?.DeactivationReason,
        };
    }

    private decimal GetDefaultListPrice(string serviceType)
    {
        var items = _pricing.GetPricing(serviceType);
        if (items.Count == 0)
            return 0m;
        return items.Min(p => p.PriceMonthly);
    }

    private static TenantDigitalServiceMutationResponseDto Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error,
        };
}

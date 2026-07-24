using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Platform-scoped TSE resource pool CRUD / assignment / metrics for Super Admin.
/// </summary>
public sealed class TseResourcePoolService : ITseResourcePoolService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TseResourcePoolService> _logger;

    public TseResourcePoolService(AppDbContext db, ILogger<TseResourcePoolService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TseResourcePoolDto> CreateResourcePoolAsync(
        CreateTseResourcePoolRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is < 2 or > 120)
            throw new ArgumentException("Name must be 2–120 characters.", nameof(request));

        var type = (request.Type ?? TseResourcePoolTypes.Shared).Trim();
        if (!TseResourcePoolTypes.IsValid(type))
            throw new ArgumentException("Type must be Shared, Dedicated, or Hybrid.", nameof(request));

        var capacity = request.TotalCapacity;
        if (capacity < 1 || capacity > 100_000)
            throw new ArgumentException("TotalCapacity must be between 1 and 100000.", nameof(request));

        // Normalize casing
        type = TseResourcePoolTypes.All.First(t =>
            string.Equals(t, type, StringComparison.OrdinalIgnoreCase));

        var pool = new TseResourcePool
        {
            Id = Guid.NewGuid(),
            Name = name,
            PoolType = type,
            TotalCapacity = capacity,
            IsActive = true,
            Description = Truncate(request.Description, 500),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Truncate(actorUserId, 450),
        };

        if (request.Rules is { Count: > 0 })
        {
            foreach (var rule in request.Rules)
            {
                var ruleType = (rule.RuleType ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(ruleType))
                    continue;

                pool.Rules.Add(new TseResourcePoolRule
                {
                    Id = Guid.NewGuid(),
                    PoolId = pool.Id,
                    RuleType = Truncate(ruleType, 64)!,
                    RuleValue = Truncate(rule.RuleValue, 256),
                    Description = Truncate(rule.Description, 500),
                    IsEnabled = rule.IsEnabled,
                });
            }
        }
        else if (type == TseResourcePoolTypes.Dedicated)
        {
            pool.Rules.Add(new TseResourcePoolRule
            {
                Id = Guid.NewGuid(),
                PoolId = pool.Id,
                RuleType = TseResourcePoolRuleTypes.MaxTenants,
                RuleValue = "1",
                Description = "Dedicated pools allow a single tenant.",
                IsEnabled = true,
            });
        }

        _db.TseResourcePools.Add(pool);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created TSE resource pool PoolId={PoolId} Name={Name} Type={Type} Capacity={Capacity}",
            pool.Id,
            pool.Name,
            pool.PoolType,
            pool.TotalCapacity);

        return await MapPoolAsync(pool.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TseResourcePoolDto>> ListResourcePoolsAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.TseResourcePools.AsNoTracking()
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = new List<TseResourcePoolDto>(ids.Count);
        foreach (var id in ids)
            list.Add(await MapPoolAsync(id, cancellationToken).ConfigureAwait(false));
        return list;
    }

    public async Task<TseResourcePoolDto> GetPoolAsync(
        Guid poolId,
        CancellationToken cancellationToken = default)
    {
        if (poolId == Guid.Empty)
            throw new ArgumentException("poolId is required.", nameof(poolId));
        return await MapPoolAsync(poolId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TsePoolAssignmentResultDto> AssignTenantToPoolAsync(
        Guid tenantId,
        Guid poolId,
        int reservedCapacity = 1,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || poolId == Guid.Empty)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = "tenantId and poolId are required.",
            };
        }

        reservedCapacity = Math.Clamp(reservedCapacity, 1, 10_000);

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = "Tenant was not found.",
                TenantId = tenantId,
                PoolId = poolId,
            };
        }

        var pool = await _db.TseResourcePools
            .Include(p => p.Assignments)
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == poolId, cancellationToken)
            .ConfigureAwait(false);
        if (pool is null)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = "Pool was not found.",
                TenantId = tenantId,
                PoolId = poolId,
            };
        }

        if (!pool.IsActive)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = "Pool is inactive.",
                TenantId = tenantId,
                PoolId = poolId,
            };
        }

        Guid? previousPoolId = null;
        var existing = await _db.TseResourcePoolAssignments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.PoolId == poolId)
            {
                existing.ReservedCapacity = reservedCapacity;
                existing.AssignedAt = DateTime.UtcNow;
                existing.AssignedBy = Truncate(actorUserId, 450);
                pool.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return new TsePoolAssignmentResultDto
                {
                    Success = true,
                    Message = "Tenant reservation updated in the same pool.",
                    TenantId = tenantId,
                    PoolId = poolId,
                    Pool = await MapPoolAsync(poolId, cancellationToken).ConfigureAwait(false),
                };
            }

            previousPoolId = existing.PoolId;
            _db.TseResourcePoolAssignments.Remove(existing);
        }

        var used = pool.Assignments
            .Where(a => a.TenantId != tenantId)
            .Sum(a => Math.Max(1, a.ReservedCapacity));
        if (used + reservedCapacity > pool.TotalCapacity)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message =
                    $"Insufficient capacity: need {reservedCapacity}, available {Math.Max(0, pool.TotalCapacity - used)}.",
                TenantId = tenantId,
                PoolId = poolId,
                PreviousPoolId = previousPoolId,
            };
        }

        var maxTenants = ResolveMaxTenants(pool);
        var otherTenants = pool.Assignments.Count(a => a.TenantId != tenantId);
        if (otherTenants + 1 > maxTenants)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = $"Pool tenant limit reached (max {maxTenants}).",
                TenantId = tenantId,
                PoolId = poolId,
                PreviousPoolId = previousPoolId,
            };
        }

        _db.TseResourcePoolAssignments.Add(new TseResourcePoolAssignment
        {
            Id = Guid.NewGuid(),
            PoolId = poolId,
            TenantId = tenantId,
            ReservedCapacity = reservedCapacity,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = Truncate(actorUserId, 450),
        });
        pool.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Assigned tenant {TenantId} to TSE pool {PoolId} (previous={Previous})",
            tenantId,
            poolId,
            previousPoolId);

        return new TsePoolAssignmentResultDto
        {
            Success = true,
            Message = previousPoolId is null
                ? "Tenant assigned to pool."
                : "Tenant moved to pool.",
            TenantId = tenantId,
            PoolId = poolId,
            PreviousPoolId = previousPoolId,
            Pool = await MapPoolAsync(poolId, cancellationToken).ConfigureAwait(false),
        };
    }

    public async Task<TsePoolAssignmentResultDto> UnassignTenantAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = "tenantId is required.",
            };
        }

        var existing = await _db.TseResourcePoolAssignments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return new TsePoolAssignmentResultDto
            {
                Success = false,
                Message = "Tenant is not assigned to any pool.",
                TenantId = tenantId,
            };
        }

        var poolId = existing.PoolId;
        _db.TseResourcePoolAssignments.Remove(existing);
        var pool = await _db.TseResourcePools.FirstOrDefaultAsync(p => p.Id == poolId, cancellationToken)
            .ConfigureAwait(false);
        if (pool is not null)
            pool.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Unassigned tenant {TenantId} from TSE pool {PoolId} by {Actor}",
            tenantId,
            poolId,
            actorUserId);

        return new TsePoolAssignmentResultDto
        {
            Success = true,
            Message = "Tenant unassigned from pool.",
            TenantId = tenantId,
            PreviousPoolId = poolId,
            Pool = await MapPoolAsync(poolId, cancellationToken).ConfigureAwait(false),
        };
    }

    public async Task<TsePoolStatusDto> GetPoolStatusAsync(
        Guid poolId,
        CancellationToken cancellationToken = default)
    {
        var pool = await MapPoolAsync(poolId, cancellationToken).ConfigureAwait(false);
        var utilization = pool.TotalCapacity == 0
            ? 0
            : Math.Round(100.0 * pool.UsedCapacity / pool.TotalCapacity, 1);

        var warnings = new List<string>();
        if (!pool.IsActive)
            warnings.Add("Pool is inactive.");
        if (utilization >= 90)
            warnings.Add("Pool capacity is nearly exhausted (≥90%).");
        else if (utilization >= 75)
            warnings.Add("Pool capacity utilization is high (≥75%).");
        if (pool.Type == TseResourcePoolTypes.Dedicated && pool.AssignedTenants.Count > 1)
            warnings.Add("Dedicated pool has more than one tenant assigned.");

        var health = !pool.IsActive
            ? "Inactive"
            : utilization >= 90
                ? "Critical"
                : utilization >= 75
                    ? "Degraded"
                    : "Healthy";

        return new TsePoolStatusDto
        {
            PoolId = pool.Id,
            Name = pool.Name,
            Type = pool.Type,
            IsActive = pool.IsActive,
            TotalCapacity = pool.TotalCapacity,
            UsedCapacity = pool.UsedCapacity,
            AvailableCapacity = pool.AvailableCapacity,
            UtilizationPercent = utilization,
            AssignedTenantCount = pool.AssignedTenants.Count,
            HealthLabel = health,
            Warnings = warnings,
        };
    }

    public async Task<TsePoolMetricsDto> GetPoolMetricsAsync(
        Guid poolId,
        CancellationToken cancellationToken = default)
    {
        var pool = await MapPoolAsync(poolId, cancellationToken).ConfigureAwait(false);
        var tenantIds = pool.AssignedTenants.ToList();
        var utilization = pool.TotalCapacity == 0
            ? 0
            : Math.Round(100.0 * pool.UsedCapacity / pool.TotalCapacity, 1);

        var devices = tenantIds.Count == 0
            ? new List<TseDevice>()
            : await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
                .Where(d => d.TenantId != null && tenantIds.Contains(d.TenantId.Value) && d.IsActive)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var scores = devices.Select(d => (double)d.HealthScore).ToList();
        var fromUtc = DateTime.UtcNow.AddDays(-30);
        var signed = tenantIds.Count == 0
            ? 0
            : await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
                .CountAsync(
                    r => tenantIds.Contains(r.TenantId)
                         && r.IssuedAt >= fromUtc
                         && r.SignatureValue != null
                         && r.SignatureValue != "",
                    cancellationToken)
                .ConfigureAwait(false);

        var byProvider = devices
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Provider) ? (d.DeviceType ?? "unknown") : d.Provider!)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return new TsePoolMetricsDto
        {
            PoolId = pool.Id,
            Name = pool.Name,
            GeneratedAt = DateTime.UtcNow,
            TotalCapacity = pool.TotalCapacity,
            UsedCapacity = pool.UsedCapacity,
            AvailableCapacity = pool.AvailableCapacity,
            UtilizationPercent = utilization,
            AssignedTenantCount = tenantIds.Count,
            ActiveDeviceCount = devices.Count,
            HealthyDeviceCount = devices.Count(d => d.HealthStatus == TseHealthStatus.Healthy),
            AverageHealthScore = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 1),
            SignedTransactionsLast30Days = signed,
            DevicesByProvider = byProvider,
        };
    }

    private async Task<TseResourcePoolDto> MapPoolAsync(Guid poolId, CancellationToken cancellationToken)
    {
        var pool = await _db.TseResourcePools.AsNoTracking()
            .Include(p => p.Assignments)
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == poolId, cancellationToken)
            .ConfigureAwait(false);
        if (pool is null)
            throw new KeyNotFoundException($"TSE resource pool {poolId} was not found.");

        var tenantIds = pool.Assignments.Select(a => a.TenantId).Distinct().ToList();
        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, Tenant>()
            : await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken)
                .ConfigureAwait(false);

        var used = pool.Assignments.Sum(a => Math.Max(1, a.ReservedCapacity));
        var available = Math.Max(0, pool.TotalCapacity - used);

        return new TseResourcePoolDto
        {
            Id = pool.Id,
            Name = pool.Name,
            Type = pool.PoolType,
            TotalCapacity = pool.TotalCapacity,
            UsedCapacity = used,
            AvailableCapacity = available,
            IsActive = pool.IsActive,
            Description = pool.Description,
            AssignedTenants = tenantIds,
            TenantSummaries = pool.Assignments
                .OrderBy(a => a.AssignedAt)
                .Select(a =>
                {
                    tenants.TryGetValue(a.TenantId, out var t);
                    return new TsePoolTenantSummaryDto
                    {
                        TenantId = a.TenantId,
                        TenantName = t?.Name,
                        TenantSlug = t?.Slug,
                        ReservedCapacity = a.ReservedCapacity,
                        AssignedAt = a.AssignedAt,
                    };
                })
                .ToList(),
            Rules = pool.Rules
                .OrderBy(r => r.RuleType)
                .Select(r => new TsePoolRuleDto
                {
                    Id = r.Id,
                    RuleType = r.RuleType,
                    RuleValue = r.RuleValue,
                    Description = r.Description,
                    IsEnabled = r.IsEnabled,
                })
                .ToList(),
            CreatedAt = pool.CreatedAt,
            UpdatedAt = pool.UpdatedAt,
        };
    }

    private static int ResolveMaxTenants(TseResourcePool pool)
    {
        if (string.Equals(pool.PoolType, TseResourcePoolTypes.Dedicated, StringComparison.OrdinalIgnoreCase))
            return 1;

        var rule = pool.Rules.FirstOrDefault(r =>
            r.IsEnabled
            && string.Equals(r.RuleType, TseResourcePoolRuleTypes.MaxTenants, StringComparison.OrdinalIgnoreCase));
        if (rule is not null && int.TryParse(rule.RuleValue, out var max) && max > 0)
            return max;

        return pool.TotalCapacity;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}

using System.Collections.Concurrent;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Operations;

public interface IOperationLimitService
{
    TenantOperationLimitKind? MatchOperation(string method, string path);

    Task<OperationLimitCheckResult> CheckLimitAsync(
        Guid tenantId,
        string? userId,
        TenantOperationLimitKind kind,
        int quantity,
        bool hasApprovalHeader,
        CancellationToken cancellationToken = default);

    Task RecordOperationAsync(
        Guid tenantId,
        string? userId,
        TenantOperationLimitKind kind,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<OperationLimitStatusDto> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory tenant quotas (UTC day / hour windows). Suitable for single-instance and local enforcement;
/// multi-instance deployments should later back this with a shared store.
/// </summary>
public sealed class OperationLimitService : IOperationLimitService
{
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<TenantOperationLimitsOptions> _options;
    private readonly ConcurrentDictionary<string, object> _keyLocks = new(StringComparer.Ordinal);

    public OperationLimitService(
        IMemoryCache cache,
        IOptionsMonitor<TenantOperationLimitsOptions> options)
    {
        _cache = cache;
        _options = options;
    }

    public TenantOperationLimitKind? MatchOperation(string method, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var m = method.ToUpperInvariant();
        var p = path;

        if (m == "POST" && Contains(p, "/api/admin/products/bulk-deactivate"))
            return TenantOperationLimitKind.BulkDelete;
        if (m == "POST" && Contains(p, "/api/admin/products/deactivate-all"))
            return TenantOperationLimitKind.BulkDelete;

        if (m == "POST" && IsProductCreate(p))
            return TenantOperationLimitKind.ProductCreate;

        if ((m == "PUT" || m == "PATCH") && IsProductUpdate(p))
            return TenantOperationLimitKind.PriceUpdate;

        if (m == "POST" && IsUserCreate(p))
            return TenantOperationLimitKind.UserCreate;

        if (m == "POST" && Contains(p, "/api/admin/backup/trigger"))
            return TenantOperationLimitKind.Backup;

        if ((m == "GET" || m == "POST") && IsExport(p))
            return TenantOperationLimitKind.Export;

        return null;
    }

    public Task<OperationLimitCheckResult> CheckLimitAsync(
        Guid tenantId,
        string? userId,
        TenantOperationLimitKind kind,
        int quantity,
        bool hasApprovalHeader,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        _ = cancellationToken;
        var opts = _options.CurrentValue;
        quantity = Math.Max(1, quantity);

        var (limit, current, resetAt) = Snapshot(tenantId, kind, opts);

        if (kind == TenantOperationLimitKind.BulkDelete
            && quantity >= Math.Max(1, opts.RequireApprovalForBulkDelete)
            && !hasApprovalHeader)
        {
            return Task.FromResult(OperationLimitCheckResult.Deny(
                kind,
                "REQUIRES_APPROVAL",
                $"Bulk delete of {quantity} items requires SuperAdmin approval (threshold {opts.RequireApprovalForBulkDelete}).",
                limit,
                current,
                resetAt,
                requiresApproval: true));
        }

        if (kind == TenantOperationLimitKind.PriceUpdate
            && quantity >= Math.Max(1, opts.RequireApprovalForPriceUpdate)
            && !hasApprovalHeader)
        {
            return Task.FromResult(OperationLimitCheckResult.Deny(
                kind,
                "REQUIRES_APPROVAL",
                $"Price update of {quantity} items requires SuperAdmin approval (threshold {opts.RequireApprovalForPriceUpdate}).",
                limit,
                current,
                resetAt,
                requiresApproval: true));
        }

        if (current + quantity > limit)
        {
            return Task.FromResult(OperationLimitCheckResult.Deny(
                kind,
                "OPERATION_LIMIT_EXCEEDED",
                $"Tenant operation limit exceeded for {kind}. Limit={limit}, used={current}, requested={quantity}. Resets at {resetAt:O}.",
                limit,
                current,
                resetAt));
        }

        return Task.FromResult(OperationLimitCheckResult.Allow(kind, limit, current, resetAt));
    }

    public Task RecordOperationAsync(
        Guid tenantId,
        string? userId,
        TenantOperationLimitKind kind,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        _ = userId;
        _ = cancellationToken;
        quantity = Math.Max(1, quantity);
        var opts = _options.CurrentValue;
        var key = CacheKey(tenantId, kind, opts);
        var gate = _keyLocks.GetOrAdd(key, static _ => new object());
        lock (gate)
        {
            var (_, _, resetAt) = Snapshot(tenantId, kind, opts);
            var current = _cache.TryGetValue(key, out int used) ? used : 0;
            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = resetAt,
            };
            _cache.Set(key, current + quantity, entryOptions);
        }

        return Task.CompletedTask;
    }

    public Task<OperationLimitStatusDto> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var opts = _options.CurrentValue;
        var (bulkLimit, bulkUsed, bulkReset) = Snapshot(tenantId, TenantOperationLimitKind.BulkDelete, opts);
        var (priceLimit, priceUsed, priceReset) = Snapshot(tenantId, TenantOperationLimitKind.PriceUpdate, opts);

        return Task.FromResult(new OperationLimitStatusDto
        {
            Enabled = opts.Enabled,
            MaxBulkDeletePerDay = opts.MaxBulkDeletePerDay,
            MaxPriceUpdatePerHour = opts.MaxPriceUpdatePerHour,
            MaxProductCreatePerDay = opts.MaxProductCreatePerDay,
            MaxUserCreatePerDay = opts.MaxUserCreatePerDay,
            MaxBackupPerDay = opts.MaxBackupPerDay,
            MaxExportPerDay = opts.MaxExportPerDay,
            RequireApprovalForBulkDelete = opts.RequireApprovalForBulkDelete,
            RequireApprovalForPriceUpdate = opts.RequireApprovalForPriceUpdate,
            BulkDeleteUsedToday = bulkUsed,
            BulkDeleteRemainingToday = Math.Max(0, bulkLimit - bulkUsed),
            BulkDeleteResetAtUtc = bulkReset,
            PriceUpdateUsedThisHour = priceUsed,
            PriceUpdateRemainingThisHour = Math.Max(0, priceLimit - priceUsed),
            PriceUpdateResetAtUtc = priceReset,
        });
    }

    private (int Limit, int Current, DateTime ResetAt) Snapshot(
        Guid tenantId,
        TenantOperationLimitKind kind,
        TenantOperationLimitsOptions opts)
    {
        var limit = ResolveLimit(kind, opts);
        var key = CacheKey(tenantId, kind, opts);
        var current = _cache.TryGetValue(key, out int used) ? used : 0;
        var resetAt = ResolveResetAt(kind);
        return (limit, current, resetAt);
    }

    private static int ResolveLimit(TenantOperationLimitKind kind, TenantOperationLimitsOptions opts) =>
        kind switch
        {
            TenantOperationLimitKind.BulkDelete => Math.Max(0, opts.MaxBulkDeletePerDay),
            TenantOperationLimitKind.PriceUpdate => Math.Max(0, opts.MaxPriceUpdatePerHour),
            TenantOperationLimitKind.ProductCreate => Math.Max(0, opts.MaxProductCreatePerDay),
            TenantOperationLimitKind.UserCreate => Math.Max(0, opts.MaxUserCreatePerDay),
            TenantOperationLimitKind.Backup => Math.Max(0, opts.MaxBackupPerDay),
            TenantOperationLimitKind.Export => Math.Max(0, opts.MaxExportPerDay),
            _ => 0,
        };

    private static DateTime ResolveResetAt(TenantOperationLimitKind kind)
    {
        var utc = DateTime.UtcNow;
        return kind == TenantOperationLimitKind.PriceUpdate
            ? new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc).AddHours(1)
            : utc.Date.AddDays(1);
    }

    private static string CacheKey(Guid tenantId, TenantOperationLimitKind kind, TenantOperationLimitsOptions opts)
    {
        var utc = DateTime.UtcNow;
        var window = kind == TenantOperationLimitKind.PriceUpdate
            ? $"{utc:yyyyMMddHH}"
            : $"{utc:yyyyMMdd}";
        // Include limit in key so config changes start a fresh counter window.
        var limit = ResolveLimit(kind, opts);
        return $"tenant-op-limit:{tenantId:D}:{kind}:{window}:L{limit}";
    }

    private static bool Contains(string path, string fragment) =>
        path.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static bool IsProductCreate(string path)
    {
        // POST /api/admin/products (no extra segment)
        if (!Contains(path, "/api/admin/products"))
            return false;
        var trimmed = path.TrimEnd('/');
        return trimmed.EndsWith("/api/admin/products", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProductUpdate(string path)
    {
        // PUT/PATCH /api/admin/products/{guid}
        if (!Contains(path, "/api/admin/products/"))
            return false;
        if (Contains(path, "/stock") || Contains(path, "/export") || Contains(path, "/demo")
            || Contains(path, "/bulk") || Contains(path, "/deactivate"))
            return false;
        return Guid.TryParse(path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), out _);
    }

    private static bool IsUserCreate(string path)
    {
        if (!Contains(path, "/api/admin/users"))
            return false;
        var trimmed = path.TrimEnd('/');
        return trimmed.EndsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExport(string path) =>
        Contains(path, "/api/admin/products/export")
        || Contains(path, "/api/admin/customers/export")
        || Contains(path, "/api/Customer/export")
        || Contains(path, "/api/admin/audit") && Contains(path, "export")
        || Contains(path, "/api/admin/rksv/dep-export");
}

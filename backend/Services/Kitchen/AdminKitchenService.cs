using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Kitchen;

public interface IAdminKitchenService
{
    Task<KitchenSettingsDto?> GetSettingsAsync(Guid tenantId, bool ignoreFilters, CancellationToken cancellationToken);
    Task<KitchenSettingsDto?> UpdateSettingsAsync(
        Guid tenantId,
        UpdateKitchenSettingsRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<KitchenAdminOrderDto>> ListOrdersAsync(
        Guid tenantId,
        bool ignoreFilters,
        CancellationToken cancellationToken);
    Task<KitchenAnalyticsDto> GetAnalyticsAsync(
        Guid tenantId,
        bool ignoreFilters,
        CancellationToken cancellationToken);
}

public sealed class AdminKitchenService : IAdminKitchenService
{
    private readonly AppDbContext _db;

    public AdminKitchenService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<KitchenSettingsDto?> GetSettingsAsync(
        Guid tenantId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var row = await SettingsQuery(ignoreFilters)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : MapSettings(row);
    }

    public async Task<KitchenSettingsDto?> UpdateSettingsAsync(
        Guid tenantId,
        UpdateKitchenSettingsRequest request,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var row = await SettingsQuery(ignoreFilters)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return null;

        row.KitchenOrderAutoClearMinutes = Math.Clamp(
            request.AutoClearMinutes,
            KitchenSettingsDefaults.MinAutoClearMinutes,
            KitchenSettingsDefaults.MaxAutoClearMinutes);
        row.KitchenOrderSound = request.SoundEnabled;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapSettings(row);
    }

    public async Task<IReadOnlyList<KitchenAdminOrderDto>> ListOrdersAsync(
        Guid tenantId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var query = OrdersQuery(ignoreFilters)
            .Where(row => row.TenantId == tenantId)
            .OrderByDescending(row => row.CreatedAtUtc);
        var rows = await query.Take(100).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(MapOrder).ToList();
    }

    public async Task<KitchenAnalyticsDto> GetAnalyticsAsync(
        Guid tenantId,
        bool ignoreFilters,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var from = now.AddHours(-24);
        var lastHour = now.AddHours(-1);

        var logs = await AuditQuery(ignoreFilters)
            .Where(row =>
                row.TenantId == tenantId
                && (row.ActionType == AuditEventType.KitchenOrderCreated
                    || row.ActionType == AuditEventType.KitchenOrderStatusChanged)
                && row.Timestamp >= from)
            .Select(row => new AuditSlice(row.EntityId, row.ActionType, row.Timestamp, row.NewValues))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var createdLast24 = logs.Count(row => row.ActionType == AuditEventType.KitchenOrderCreated);
        var createdLastHour = logs.Count(row =>
            row.ActionType == AuditEventType.KitchenOrderCreated && row.Timestamp >= lastHour);

        var createdByEntity = logs
            .Where(row => row.ActionType == AuditEventType.KitchenOrderCreated && row.EntityId is Guid)
            .GroupBy(row => row.EntityId!.Value)
            .ToDictionary(group => group.Key, group => group.Min(item => item.Timestamp));

        var prepMinutes = new List<double>();
        foreach (var ready in logs.Where(IsReadyStatusChange))
        {
            if (ready.EntityId is not Guid entityId)
                continue;
            if (!createdByEntity.TryGetValue(entityId, out var createdAt))
                continue;
            var minutes = (ready.Timestamp - createdAt).TotalMinutes;
            if (minutes >= 0)
                prepMinutes.Add(minutes);
        }

        return new KitchenAnalyticsDto
        {
            AveragePrepMinutes = prepMinutes.Count == 0 ? null : Math.Round(prepMinutes.Average(), 1),
            OrdersPerHour = Math.Round(createdLast24 / 24d, 2),
            CreatedLast24Hours = createdLast24,
            CreatedLastHour = createdLastHour,
        };
    }

    private IQueryable<CompanySettings> SettingsQuery(bool ignoreFilters) =>
        ignoreFilters ? _db.CompanySettings.IgnoreQueryFilters() : _db.CompanySettings;

    private IQueryable<KitchenOrder> OrdersQuery(bool ignoreFilters)
    {
        var query = ignoreFilters ? _db.KitchenOrders.IgnoreQueryFilters() : _db.KitchenOrders;
        return query.AsNoTracking().Include(row => row.Items);
    }

    private IQueryable<AuditLog> AuditQuery(bool ignoreFilters) =>
        ignoreFilters ? _db.AuditLogs.IgnoreQueryFilters().AsNoTracking() : _db.AuditLogs.AsNoTracking();

    private static KitchenSettingsDto MapSettings(CompanySettings row) =>
        new()
        {
            AutoClearMinutes = row.KitchenOrderAutoClearMinutes <= 0
                ? KitchenSettingsDefaults.AutoClearMinutes
                : row.KitchenOrderAutoClearMinutes,
            SoundEnabled = row.KitchenOrderSound,
        };

    private static KitchenAdminOrderDto MapOrder(KitchenOrder row) =>
        new()
        {
            Id = row.Id,
            TableNumber = row.TableNumber,
            Status = row.Status,
            Notes = row.Notes,
            CreatedAtUtc = row.CreatedAtUtc,
            Items = row.Items
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new KitchenAdminOrderItemDto
                {
                    Id = item.Id,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    Notes = item.Notes,
                    Status = item.Status,
                })
                .ToList(),
        };

    private static bool IsReadyStatusChange(AuditSlice row)
    {
        if (row.ActionType != AuditEventType.KitchenOrderStatusChanged)
            return false;
        var json = row.NewValues ?? string.Empty;
        return json.Contains("\"status\":\"Ready\"", StringComparison.OrdinalIgnoreCase)
            || json.Contains("\"Status\":\"Ready\"", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AuditSlice(
        Guid? EntityId,
        AuditEventType? ActionType,
        DateTime Timestamp,
        string? NewValues);
}

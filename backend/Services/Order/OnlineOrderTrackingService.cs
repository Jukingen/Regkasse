using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Read-side tracking (timeline) for online orders. Writes go through <see cref="IOnlineOrderStatusService"/>.
/// </summary>
public interface IOnlineOrderTrackingService
{
    Task<IReadOnlyList<OnlineOrderStatusChangeDto>> GetTimelineAsync(
        Guid onlineOrderId,
        CancellationToken ct = default);
}

public sealed class OnlineOrderTrackingService : IOnlineOrderTrackingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public OnlineOrderTrackingService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenantAccessor)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyList<OnlineOrderStatusChangeDto>> GetTimelineAsync(
        Guid onlineOrderId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.OnlineOrders.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == onlineOrderId, ct);
        if (order is null)
            return Array.Empty<OnlineOrderStatusChangeDto>();

        if (_tenantAccessor.TenantId is Guid ambient
            && ambient != Guid.Empty
            && order.TenantId != ambient)
        {
            return Array.Empty<OnlineOrderStatusChangeDto>();
        }

        return await db.OnlineOrderStatusChanges.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.OnlineOrderId == onlineOrderId)
            .OrderBy(c => c.ChangedAt)
            .Select(c => new OnlineOrderStatusChangeDto
            {
                Id = c.Id,
                FromStatus = c.FromStatus,
                ToStatus = c.ToStatus,
                ChangedAt = c.ChangedAt
            })
            .ToListAsync(ct);
    }
}

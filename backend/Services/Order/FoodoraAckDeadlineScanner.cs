using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Marks Foodora orders that are still <c>pending</c> after <see cref="FoodoraAckDeadline.Window"/>.
/// Does not call Foodora and does not cancel the order.
/// </summary>
public sealed class FoodoraAckDeadlineScanner
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly TimeProvider _time;

    public FoodoraAckDeadlineScanner(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
    {
        _dbFactory = dbFactory;
        _time = time;
    }

    public async Task MarkExpiredAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var cutoff = now.Subtract(FoodoraAckDeadline.Window);
        var rows = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .Where(r => r.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider
                        && r.IsActive
                        && r.OnlineOrderId != null
                        && r.ReceivedAt <= cutoff
                        && r.LastError != FoodoraAckDeadline.ErrorCode)
            .ToListAsync(ct);
        if (rows.Count == 0)
            return;

        var orderIds = rows.Select(r => r.OnlineOrderId!.Value).ToList();
        var pending = await db.OnlineOrders
            .IgnoreQueryFilters()
            .Where(o => orderIds.Contains(o.Id) && o.OrderStatus == OnlineOrderStatuses.Pending)
            .Select(o => o.Id)
            .ToListAsync(ct);
        var pendingIds = pending.ToHashSet();
        var changed = false;
        foreach (var row in rows)
        {
            if (row.OnlineOrderId is not Guid orderId || !pendingIds.Contains(orderId))
                continue;
            if (!FoodoraAckDeadline.ShouldFlag(row.ReceivedAt, OnlineOrderStatuses.Pending, now))
                continue;

            row.LastError = FoodoraAckDeadline.ErrorCode;
            row.UpdatedAt = now;
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

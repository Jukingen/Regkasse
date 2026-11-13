using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public interface IFinanzOnlineOutboxMonitoringService
{
    /// <summary>
    /// <paramref name="tenantId"/> null counts every mandant (Super Admin). Otherwise only that tenant's rows.
    /// </summary>
    Task<FinanzOnlineOutboxSummaryDto> GetSummaryAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task<int> CountFailedSinceAsync(Guid? tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}

public sealed class FinanzOnlineOutboxMonitoringService : IFinanzOnlineOutboxMonitoringService
{
    public const int HourlyFailureAlertThreshold = 5;

    /// <summary>Terminal or operator-blocked rows. Pending and in-flight statuses are not failures.</summary>
    public static readonly string[] FailedStatuses =
    [
        FinanzOnlineOutboxStatuses.RetryableFailure,
        FinanzOnlineOutboxStatuses.ProtocolFailure,
        FinanzOnlineOutboxStatuses.PermanentFailure,
        FinanzOnlineOutboxStatuses.DeadLetter,
        FinanzOnlineOutboxStatuses.ManualReviewRequired,
    ];

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public FinanzOnlineOutboxMonitoringService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<FinanzOnlineOutboxSummaryDto> GetSummaryAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var query = Scope(tenantId);
        var pending = await query
            .CountAsync(x => x.Status == FinanzOnlineOutboxStatuses.Pending, cancellationToken)
            .ConfigureAwait(false);
        var failed = await CountFailedSinceAsync(tenantId, now.AddHours(-24), cancellationToken)
            .ConfigureAwait(false);
        var lastSuccess = await query
            .Where(x => x.Status == FinanzOnlineOutboxStatuses.ProtocolSuccess)
            .OrderByDescending(x => x.ProcessedAt ?? x.CreatedAt)
            .Select(x => (DateTime?)(x.ProcessedAt ?? x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new FinanzOnlineOutboxSummaryDto
        {
            PendingCount = pending,
            FailedLast24Hours = failed,
            LastSuccessfulSubmissionUtc = lastSuccess,
        };
    }

    public Task<int> CountFailedSinceAsync(
        Guid? tenantId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var failed = FailedStatuses;
        return Scope(tenantId).CountAsync(
            x => failed.Contains(x.Status)
                 && ((x.ProcessedAt != null && x.ProcessedAt >= sinceUtc)
                     || (x.ProcessedAt == null && x.CreatedAt >= sinceUtc)),
            cancellationToken);
    }

    private IQueryable<FinanzOnlineOutboxMessage> Scope(Guid? tenantId)
    {
        var query = _db.FinanzOnlineOutboxMessages.AsNoTracking();
        if (tenantId is not Guid id)
            return query;

        var dashed = id.ToString("D");
        var compact = id.ToString("N");
        return query.Where(x => x.TenantId == dashed || x.TenantId == compact);
    }
}

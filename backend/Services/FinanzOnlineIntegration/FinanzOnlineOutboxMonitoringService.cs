using System.Text.RegularExpressions;
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

    Task<FinanzOnlineOutboxHealthDto> GetHealthAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task<int> CountFailedSinceAsync(Guid? tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}

public sealed class FinanzOnlineOutboxMonitoringService : IFinanzOnlineOutboxMonitoringService
{
    public const int HourlyFailureAlertThreshold = 5;
    public const int RecentErrorLimit = 5;
    public const int RedactedMessageMaxLength = 160;

    private static readonly Regex EmailPattern = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LongDigitPattern = new(
        @"\d{8,}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public async Task<FinanzOnlineOutboxHealthDto> GetHealthAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var failed = FailedStatuses;
        var rows = await Scope(tenantId)
            .Where(x => failed.Contains(x.Status))
            .OrderByDescending(x => x.ProcessedAt ?? x.CreatedAt)
            .Take(RecentErrorLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new FinanzOnlineOutboxHealthDto
        {
            PendingCount = summary.PendingCount,
            FailedLast24Hours = summary.FailedLast24Hours,
            LastSuccessfulSubmissionUtc = summary.LastSuccessfulSubmissionUtc,
            RecentErrors = rows.Select(row => new FinanzOnlineOutboxRecentErrorDto
            {
                Status = row.Status,
                ErrorCode = string.IsNullOrWhiteSpace(row.LastErrorCode) ? null : row.LastErrorCode.Trim(),
                Message = SanitizeErrorMessage(row.LastErrorMessage),
                OccurredAtUtc = row.ProcessedAt ?? row.CreatedAt,
            }).ToList(),
        };
    }

    /// <summary>Drops email addresses and long digit runs (phones, ids) from an operator-facing error line.</summary>
    internal static string? SanitizeErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var cleaned = EmailPattern.Replace(message, "[redacted]");
        cleaned = LongDigitPattern.Replace(cleaned, "[redacted]");
        cleaned = cleaned.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (cleaned.Length > RedactedMessageMaxLength)
            cleaned = cleaned[..RedactedMessageMaxLength];
        return cleaned.Length == 0 ? null : cleaned;
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

using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Analytics;

/// <summary>Super Admin fleet TSE usage (registers + signed payments). Diagnostic only.</summary>
public sealed class TseUsageAnalyticsService : ITseUsageAnalyticsService
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
    private const int MaxRangeDays = 366;

    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<TseUsageAnalyticsService> _logger;

    public TseUsageAnalyticsService(
        AppDbContext db,
        ICacheService cache,
        ILogger<TseUsageAnalyticsService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public Task<TseAnalyticsDto> GetTseAnalyticsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var (from, toExclusive) = NormalizeRange(fromUtc, toUtc);
        var cacheKey = CacheKeys.Format(
            CacheKeys.TseUsageAnalytics,
            from.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            toExclusive.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        return _cache.GetOrCreateAsync(
            cacheKey,
            ct => LoadAsync(from, toExclusive, ct),
            CacheTtl,
            cancellationToken);
    }

    internal static (DateTime FromUtc, DateTime ToExclusiveUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var now = DateTime.UtcNow;
        var toExclusive = toUtc?.ToUniversalTime() ?? now;
        var from = fromUtc?.ToUniversalTime() ?? toExclusive.AddDays(-30);
        if (from > toExclusive)
            (from, toExclusive) = (toExclusive, from);

        if ((toExclusive - from).TotalDays > MaxRangeDays)
            from = toExclusive.AddDays(-MaxRangeDays);

        return (from, toExclusive);
    }

    private async Task<TseAnalyticsDto> LoadAsync(
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrow = todayStart.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var platformId = SystemTenantIds.Platform;
        var registers = await _db.CashRegisters
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId != platformId)
            .Select(r => new
            {
                r.Id,
                r.IsActive,
                r.Status,
                r.DecommissionedAtUtc,
                r.StartbelegCreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = registers.Count;
        var active = registers.Count(r =>
            r.IsActive
            && r.DecommissionedAtUtc == null
            && r.Status != RegisterStatus.Decommissioned
            && r.Status != RegisterStatus.Disabled);
        var tseEnabled = registers.Count(r => r.StartbelegCreatedAt != null);
        var tseDisabled = Math.Max(0, total - tseEnabled);

        var registerIds = registers.Select(r => r.Id).ToHashSet();

        var payments = await _db.PaymentDetails
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.IsActive && !p.IsStorno && !p.IsRefund)
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc)
            .Select(p => new { p.CashRegisterId, p.CreatedAt, p.TseSignature })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        payments = payments.Where(p => registerIds.Contains(p.CashRegisterId)).ToList();

        var monthPayments = await _db.PaymentDetails
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.IsActive && !p.IsStorno && !p.IsRefund)
            .Where(p => p.CreatedAt >= monthStart && p.CreatedAt < tomorrow)
            .Select(p => new { p.CashRegisterId, p.CreatedAt, p.TseSignature })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        monthPayments = monthPayments.Where(p => registerIds.Contains(p.CashRegisterId)).ToList();

        var signaturesToday = monthPayments.Count(p =>
            p.CreatedAt >= todayStart && HasSignature(p.TseSignature));
        var signaturesThisMonth = monthPayments.Count(p => HasSignature(p.TseSignature));
        var failedInRange = payments.Count(p => !HasSignature(p.TseSignature));

        var denom = tseEnabled > 0 ? tseEnabled : Math.Max(total, 0);
        var avg = denom > 0
            ? decimal.Round((decimal)signaturesThisMonth / denom, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var byDay = payments
            .Where(p => HasSignature(p.TseSignature))
            .GroupBy(p => p.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var daily = new List<TseDailyUsageDto>();
        for (var d = fromUtc.Date; d < toExclusiveUtc.Date.AddDays(1) && d <= now.Date; d = d.AddDays(1))
        {
            byDay.TryGetValue(d, out var count);
            daily.Add(new TseDailyUsageDto(d, count));
        }

        _logger.LogDebug(
            "TSE usage analytics: registers={Total} enabled={Enabled} signedMonth={Signed} failed={Failed}",
            total,
            tseEnabled,
            signaturesThisMonth,
            failedInRange);

        return new TseAnalyticsDto(
            TotalRegisters: total,
            ActiveRegisters: active,
            TseEnabled: tseEnabled,
            TseDisabled: tseDisabled,
            SignaturesToday: signaturesToday,
            SignaturesThisMonth: signaturesThisMonth,
            FailedSignatures: failedInRange,
            AverageSignaturesPerRegister: avg,
            DailyUsage: daily,
            DiagnosticOnly: true);
    }

    private static bool HasSignature(string? signature) =>
        !string.IsNullOrWhiteSpace(signature);
}

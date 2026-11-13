using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Derives engagement, feature adoption, funnel drop-off, and cohort retention from
/// <see cref="AuthSession"/> and <see cref="AuditLog"/> — diagnostic UX metrics only.
/// </summary>
public sealed class TseUserAnalyticsService : ITseUserAnalyticsService
{
    private const int MaxPeriodDays = 366;
    private const int DefaultLookbackDays = 30;
    private const int MaxCohortWeeks = 12;

    private static readonly string[] FunnelOrder =
    {
        FeatureKeys.Login,
        FeatureKeys.CashRegister,
        FeatureKeys.Cart,
        FeatureKeys.Payment,
        FeatureKeys.Receipt,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<TseUserAnalyticsService> _logger;

    public TseUserAnalyticsService(AppDbContext db, ILogger<TseUserAnalyticsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TseUserBehaviorReportDto> GenerateUserReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        (fromUtc, toUtc) = NormalizePeriod(fromUtc, toUtc);
        var tenant = await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var sessions = await LoadSessionsAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        var audits = await LoadAuditsAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        var (featureUsage, adoption, uniqueUsers) = BuildFeatureMetrics(audits, sessions);
        var funnel = BuildFunnel(audits);
        var dropoffs = BuildDropoffs(funnel);
        var satisfaction = BuildSatisfactionScores(audits, sessions);
        var recommendations = BuildRecommendations(featureUsage, adoption, dropoffs, satisfaction, sessions);

        var periodDays = Math.Max(1.0, (toUtc - fromUtc).TotalDays);
        var dau = uniqueUsers == 0
            ? 0
            : Math.Round(
                sessions
                    .Where(s => s.LastActivityAtUtc.HasValue || s.CreatedAtUtc >= fromUtc)
                    .Select(s => (s.LastActivityAtUtc ?? s.CreatedAtUtc).Date)
                    .Distinct()
                    .Count() / periodDays,
                2);

        // Prefer distinct active users from audits when sessions are sparse.
        if (uniqueUsers == 0 && audits.Count > 0)
            uniqueUsers = audits.Select(a => a.UserId).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal).Count();

        var avgSessionMinutes = AverageSessionMinutes(sessions);

        _logger.LogInformation(
            "TSE user analytics TenantId={TenantId} Period={From:o}..{To:o} Sessions={Sessions} Users={Users}",
            tenantId,
            fromUtc,
            toUtc,
            sessions.Count,
            uniqueUsers);

        return new TseUserBehaviorReportDto
        {
            TenantId = tenantId,
            TenantName = tenant.Name,
            PeriodStart = fromUtc,
            PeriodEnd = toUtc,
            GeneratedAt = DateTime.UtcNow,
            TotalSessions = sessions.Count,
            AverageSessionDuration = avgSessionMinutes,
            UniqueUsers = uniqueUsers,
            DailyActiveUsers = dau > 0
                ? dau
                : Math.Round(uniqueUsers / periodDays, 2),
            FeatureUsage = featureUsage,
            FeatureAdoptionRate = adoption,
            DropoffPoints = dropoffs,
            UserSatisfactionScores = satisfaction,
            FunnelSteps = funnel,
            Recommendations = recommendations,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseFeatureUsageReportDto> GetFeatureUsageReportAsync(
        Guid? tenantId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var to = NormalizeUtc(toUtc ?? DateTime.UtcNow);
        var from = NormalizeUtc(fromUtc ?? to.AddDays(-DefaultLookbackDays));
        (from, to) = NormalizePeriod(from, to);

        if (tenantId is { } tid && tid != Guid.Empty)
            await RequireTenantAsync(tid, cancellationToken).ConfigureAwait(false);

        var audits = await LoadAuditsAsync(tenantId, from, to, cancellationToken).ConfigureAwait(false);
        var sessions = await LoadSessionsAsync(tenantId, from, to, cancellationToken).ConfigureAwait(false);
        var (usage, adoption, uniqueUsers) = BuildFeatureMetrics(audits, sessions);
        var heatmap = BuildHeatmap(audits);

        return new TseFeatureUsageReportDto
        {
            TenantId = tenantId is { } t && t != Guid.Empty ? t : null,
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = DateTime.UtcNow,
            UniqueUsers = uniqueUsers,
            FeatureUsage = usage,
            FeatureAdoptionRate = adoption,
            Heatmap = heatmap,
            DiagnosticOnly = true,
        };
    }

    public async Task<TseCohortAnalysisResultDto> PerformCohortAnalysisAsync(
        Guid? tenantId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var to = NormalizeUtc(toUtc ?? DateTime.UtcNow);
        var from = NormalizeUtc(fromUtc ?? to.AddDays(-(DefaultLookbackDays * 3)));
        (from, to) = NormalizePeriod(from, to);

        if (tenantId is { } tid && tid != Guid.Empty)
            await RequireTenantAsync(tid, cancellationToken).ConfigureAwait(false);

        var sessions = await LoadSessionsAsync(tenantId, from, to, cancellationToken)
            .ConfigureAwait(false);
        var audits = await LoadAuditsAsync(tenantId, from, to, cancellationToken)
            .ConfigureAwait(false);

        var firstSeen = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var s in sessions.OrderBy(x => x.CreatedAtUtc))
        {
            if (string.IsNullOrWhiteSpace(s.UserId))
                continue;
            if (!firstSeen.ContainsKey(s.UserId))
                firstSeen[s.UserId] = s.CreatedAtUtc.Date;
        }

        foreach (var a in audits.OrderBy(x => x.Timestamp))
        {
            if (string.IsNullOrWhiteSpace(a.UserId))
                continue;
            var day = a.Timestamp.Date;
            if (!firstSeen.TryGetValue(a.UserId, out var existing) || day < existing)
                firstSeen[a.UserId] = day;
        }

        var activityByUserWeek = audits
            .Where(a => !string.IsNullOrWhiteSpace(a.UserId))
            .Select(a => (a.UserId, Week: StartOfWeek(a.Timestamp.Date)))
            .Concat(sessions
                .Where(s => !string.IsNullOrWhiteSpace(s.UserId))
                .Select(s => (s.UserId, Week: StartOfWeek((s.LastActivityAtUtc ?? s.CreatedAtUtc).Date))))
            .GroupBy(x => x.UserId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Week).Distinct().ToHashSet(),
                StringComparer.Ordinal);

        var cohortGroups = firstSeen
            .GroupBy(kv => StartOfWeek(kv.Value))
            .OrderBy(g => g.Key)
            .TakeLast(MaxCohortWeeks)
            .ToList();

        var rows = new List<TseCohortRowDto>();
        foreach (var group in cohortGroups)
        {
            var cohortStart = group.Key;
            var members = group.Select(kv => kv.Key).ToList();
            var size = members.Count;
            var retention = new List<double>();
            for (var weekOffset = 0; weekOffset < MaxCohortWeeks; weekOffset++)
            {
                var week = cohortStart.AddDays(7 * weekOffset);
                if (week > to.Date)
                    break;

                var active = members.Count(uid =>
                    activityByUserWeek.TryGetValue(uid, out var weeks) && weeks.Contains(week));
                retention.Add(size == 0 ? 0 : Math.Round(100.0 * active / size, 1));
            }

            rows.Add(new TseCohortRowDto
            {
                CohortWeek = $"W{ISOWeek.GetWeekOfYear(cohortStart):00}-{cohortStart.Year}",
                CohortStart = cohortStart,
                CohortSize = size,
                RetentionByWeek = retention,
            });
        }

        return new TseCohortAnalysisResultDto
        {
            TenantId = tenantId is { } t && t != Guid.Empty ? t : null,
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = DateTime.UtcNow,
            CohortWeeks = rows.Count,
            Cohorts = rows,
            DiagnosticOnly = true,
        };
    }

    private async Task<Tenant> RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
        return tenant;
    }

    private async Task<List<AuthSession>> LoadSessionsAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var q = _db.AuthSessions.AsNoTracking()
            .Where(s => s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc < toUtc);
        if (tenantId is { } tid && tid != Guid.Empty)
            q = q.Where(s => s.TenantId == tid);
        return await q.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<AuditSnippet>> LoadAuditsAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var q = _db.AuditLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.Timestamp >= fromUtc && a.Timestamp < toUtc);
        if (tenantId is { } tid && tid != Guid.Empty)
            q = q.Where(a => a.TenantId == tid);

        return await q
            .Select(a => new AuditSnippet(
                a.UserId,
                a.Action,
                a.EntityType,
                a.Timestamp,
                a.Status,
                a.HttpStatusCode))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static (Dictionary<string, int> Usage, Dictionary<string, double> Adoption, int UniqueUsers)
        BuildFeatureMetrics(IReadOnlyList<AuditSnippet> audits, IReadOnlyList<AuthSession> sessions)
    {
        var usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var usersByFeature = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var allUsers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var s in sessions)
        {
            if (!string.IsNullOrWhiteSpace(s.UserId))
                allUsers.Add(s.UserId);
        }

        foreach (var a in audits)
        {
            if (!string.IsNullOrWhiteSpace(a.UserId))
                allUsers.Add(a.UserId);

            var feature = MapFeature(a.Action, a.EntityType);
            usage[feature] = usage.GetValueOrDefault(feature) + 1;

            if (!string.IsNullOrWhiteSpace(a.UserId))
            {
                if (!usersByFeature.TryGetValue(feature, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    usersByFeature[feature] = set;
                }

                set.Add(a.UserId);
            }
        }

        var adoption = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var denom = Math.Max(1, allUsers.Count);
        foreach (var feature in usage.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var adopters = usersByFeature.TryGetValue(feature, out var set) ? set.Count : 0;
            adoption[feature] = Math.Round(100.0 * adopters / denom, 1);
        }

        return (usage, adoption, allUsers.Count);
    }

    private static IReadOnlyList<TseFunnelStepDto> BuildFunnel(IReadOnlyList<AuditSnippet> audits)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in FunnelOrder)
            counts[key] = 0;

        foreach (var a in audits)
        {
            var feature = MapFeature(a.Action, a.EntityType);
            if (counts.ContainsKey(feature))
                counts[feature]++;
        }

        var first = FunnelOrder.Select(f => counts[f]).DefaultIfEmpty(0).Max();
        if (first <= 0)
            first = Math.Max(1, counts.Values.Sum());

        return FunnelOrder.Select(step =>
        {
            var count = counts[step];
            return new TseFunnelStepDto
            {
                Step = step,
                Label = FeatureLabels.TryGetValue(step, out var label) ? label : step,
                Count = count,
                ConversionPercent = Math.Round(100.0 * count / first, 1),
            };
        }).ToList();
    }

    private static IReadOnlyList<TseDropoffPointDto> BuildDropoffs(IReadOnlyList<TseFunnelStepDto> funnel)
    {
        var list = new List<TseDropoffPointDto>();
        for (var i = 0; i < funnel.Count - 1; i++)
        {
            var from = funnel[i];
            var to = funnel[i + 1];
            var fromCount = Math.Max(from.Count, 0);
            var toCount = Math.Max(to.Count, 0);
            // Monotonic funnel: treat later step cannot exceed earlier for drop-off math.
            var effectiveTo = Math.Min(toCount, fromCount);
            var drop = fromCount == 0
                ? 0
                : Math.Round(100.0 * (fromCount - effectiveTo) / fromCount, 1);

            var severity = drop >= 50 ? "High" : drop >= 25 ? "Medium" : "Info";
            list.Add(new TseDropoffPointDto
            {
                FromStep = from.Step,
                ToStep = to.Step,
                FromCount = fromCount,
                ToCount = toCount,
                DropoffPercent = drop,
                Severity = severity,
            });
        }

        return list;
    }

    private static Dictionary<string, double> BuildSatisfactionScores(
        IReadOnlyList<AuditSnippet> audits,
        IReadOnlyList<AuthSession> sessions)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var paymentAttempts = audits.Count(a => MapFeature(a.Action, a.EntityType) == FeatureKeys.Payment);
        var paymentOk = audits.Count(a =>
            MapFeature(a.Action, a.EntityType) == FeatureKeys.Payment && IsSuccess(a));
        scores["payment"] = ScoreFromRatio(paymentOk, paymentAttempts);

        var tseAttempts = audits.Count(a => MapFeature(a.Action, a.EntityType) == FeatureKeys.TseStatus);
        var tseOk = audits.Count(a =>
            MapFeature(a.Action, a.EntityType) == FeatureKeys.TseStatus && IsSuccess(a));
        scores["tse_status"] = ScoreFromRatio(tseOk, tseAttempts);

        var loginAttempts = audits.Count(a =>
            string.Equals(a.Action, AuditLogActions.USER_LOGIN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Action, AuditLogActions.USER_LOGIN_FAILED, StringComparison.OrdinalIgnoreCase));
        var loginOk = audits.Count(a =>
            string.Equals(a.Action, AuditLogActions.USER_LOGIN, StringComparison.OrdinalIgnoreCase)
            && IsSuccess(a));
        scores["login"] = ScoreFromRatio(loginOk, loginAttempts);

        var overallParts = scores.Values.Where(v => v > 0).ToList();
        scores["overall"] = overallParts.Count == 0
            ? (sessions.Count > 0 ? 7.0 : 0)
            : Math.Round(overallParts.Average(), 1);

        return scores;
    }

    private static IReadOnlyList<TseUxRecommendationDto> BuildRecommendations(
        Dictionary<string, int> usage,
        Dictionary<string, double> adoption,
        IReadOnlyList<TseDropoffPointDto> dropoffs,
        Dictionary<string, double> satisfaction,
        IReadOnlyList<AuthSession> sessions)
    {
        var list = new List<TseUxRecommendationDto>();

        var worstDrop = dropoffs.OrderByDescending(d => d.DropoffPercent).FirstOrDefault();
        if (worstDrop is { DropoffPercent: >= 25 })
        {
            list.Add(new TseUxRecommendationDto
            {
                Code = "reduce_funnel_dropoff",
                Title = "Reduce funnel drop-off",
                Description =
                    $"High drop-off ({worstDrop.DropoffPercent:0.#}%) between {worstDrop.FromStep} and {worstDrop.ToStep}. Review UX friction and error rates on that step.",
                Severity = worstDrop.Severity,
                RelatedFeature = worstDrop.ToStep,
            });
        }

        if (adoption.TryGetValue(FeatureKeys.SpecialReceipt, out var specialAdoption) && specialAdoption < 20
            && usage.GetValueOrDefault(FeatureKeys.Payment) > 10)
        {
            list.Add(new TseUxRecommendationDto
            {
                Code = "promote_special_receipts",
                Title = "Promote RKSV special receipts",
                Description =
                    "Special-receipt feature adoption is low relative to payment volume. Surface Monats-/Jahresbeleg reminders in FA.",
                Severity = "Medium",
                RelatedFeature = FeatureKeys.SpecialReceipt,
            });
        }

        if (satisfaction.TryGetValue("payment", out var payScore) && payScore > 0 && payScore < 6)
        {
            list.Add(new TseUxRecommendationDto
            {
                Code = "improve_payment_reliability",
                Title = "Improve payment success rate",
                Description =
                    $"Payment satisfaction score is {payScore:0.#}/10. Investigate POS_PAY failures and TSE signing latency.",
                Severity = "High",
                RelatedFeature = FeatureKeys.Payment,
            });
        }

        if (sessions.Count > 0)
        {
            var avgMin = AverageSessionMinutes(sessions);
            if (avgMin > 0 && avgMin < 2)
            {
                list.Add(new TseUxRecommendationDto
                {
                    Code = "short_sessions",
                    Title = "Sessions are very short",
                    Description =
                        $"Average session duration is {avgMin:0.#} minutes. Check early errors after login or register-ready failures.",
                    Severity = "Medium",
                    RelatedFeature = FeatureKeys.Login,
                });
            }
        }

        if (list.Count == 0)
        {
            list.Add(new TseUxRecommendationDto
            {
                Code = "healthy_engagement",
                Title = "Engagement looks stable",
                Description = "No critical UX drop-offs detected in this period. Continue monitoring DAU and payment success.",
                Severity = "Info",
            });
        }

        return list;
    }

    private static IReadOnlyList<TseFeatureHeatmapCellDto> BuildHeatmap(IReadOnlyList<AuditSnippet> audits)
    {
        return audits
            .GroupBy(a => new
            {
                Feature = MapFeature(a.Action, a.EntityType),
                Day = a.Timestamp.DayOfWeek.ToString(),
            })
            .Select(g => new TseFeatureHeatmapCellDto
            {
                Feature = g.Key.Feature,
                DayOfWeek = g.Key.Day,
                Count = g.Count(),
            })
            .OrderBy(c => c.Feature, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.DayOfWeek, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double AverageSessionMinutes(IReadOnlyList<AuthSession> sessions)
    {
        if (sessions.Count == 0)
            return 0;

        var durations = sessions
            .Select(s =>
            {
                var end = s.RevokedAtUtc ?? s.LastActivityAtUtc ?? s.CreatedAtUtc;
                var minutes = (end - s.CreatedAtUtc).TotalMinutes;
                return minutes < 0 ? 0 : Math.Min(minutes, 12 * 60); // cap at 12h
            })
            .Where(m => m > 0)
            .ToList();

        return durations.Count == 0 ? 0 : Math.Round(durations.Average(), 1);
    }

    internal static string MapFeature(string? action, string? entityType)
    {
        var a = action ?? string.Empty;
        var e = entityType ?? string.Empty;

        if (a.Contains("LOGIN", StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.Login;
        if (a.Contains("SPL_RCPT", StringComparison.OrdinalIgnoreCase)
            || a.Contains("SPECIAL", StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.SpecialReceipt;
        if (a.Contains("TSE", StringComparison.OrdinalIgnoreCase)
            || e.Equals(AuditLogEntityTypes.TSE_DEVICE, StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.TseStatus;
        if (a.Contains("CASH_REGISTER", StringComparison.OrdinalIgnoreCase)
            || a.Contains("REG_READY", StringComparison.OrdinalIgnoreCase)
            || e.Equals(AuditLogEntityTypes.CASH_REGISTER, StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.CashRegister;
        if (a.Contains("BACKUP", StringComparison.OrdinalIgnoreCase)
            || a.Contains("RESTORE", StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.Backup;
        if (a.Contains("DEP", StringComparison.OrdinalIgnoreCase)
            || e.Contains("FiscalExport", StringComparison.OrdinalIgnoreCase)
            || e.Contains("Dep", StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.DepExport;
        if (a.Contains("PAYMENT", StringComparison.OrdinalIgnoreCase)
            || a.Contains("POS_PAY", StringComparison.OrdinalIgnoreCase)
            || e.Equals(AuditLogEntityTypes.PAYMENT, StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.Payment;
        if (a.Contains("CART", StringComparison.OrdinalIgnoreCase)
            || e.Equals(AuditLogEntityTypes.CART, StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.Cart;
        if (a.Contains("RECEIPT", StringComparison.OrdinalIgnoreCase)
            || e.Equals(AuditLogEntityTypes.RECEIPT, StringComparison.OrdinalIgnoreCase))
            return FeatureKeys.Receipt;

        return FeatureKeys.Other;
    }

    private static bool IsSuccess(AuditSnippet a)
    {
        if (a.HttpStatusCode is >= 400)
            return false;
        return a.Status is AuditLogStatus.Success or AuditLogStatus.Warning;
    }

    private static double ScoreFromRatio(int ok, int total)
    {
        if (total <= 0)
            return 0;
        return Math.Round(10.0 * ok / total, 1);
    }

    private static (DateTime From, DateTime To) NormalizePeriod(DateTime fromUtc, DateTime toUtc)
    {
        fromUtc = NormalizeUtc(fromUtc);
        toUtc = NormalizeUtc(toUtc);
        if (toUtc <= fromUtc)
            throw new ArgumentException("toUtc must be strictly greater than fromUtc.");
        if ((toUtc - fromUtc).TotalDays > MaxPeriodDays)
            throw new ArgumentException($"Period must be at most {MaxPeriodDays} days.");
        return (fromUtc, toUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    private static DateTime StartOfWeek(DateTime date)
    {
        var d = date.Date;
        var diff = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
        return d.AddDays(-diff);
    }

    private sealed record AuditSnippet(
        string UserId,
        string Action,
        string EntityType,
        DateTime Timestamp,
        AuditLogStatus Status,
        int? HttpStatusCode);

    private static class FeatureKeys
    {
        public const string Login = "login";
        public const string CashRegister = "cash_register";
        public const string Cart = "cart";
        public const string Payment = "payment";
        public const string Receipt = "receipt";
        public const string SpecialReceipt = "special_receipt";
        public const string TseStatus = "tse_status";
        public const string Backup = "backup";
        public const string DepExport = "dep_export";
        public const string Other = "other";
    }

    private static readonly Dictionary<string, string> FeatureLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        [FeatureKeys.Login] = "Login",
        [FeatureKeys.CashRegister] = "Cash register ready",
        [FeatureKeys.Cart] = "Cart",
        [FeatureKeys.Payment] = "Payment",
        [FeatureKeys.Receipt] = "Receipt",
        [FeatureKeys.SpecialReceipt] = "Special receipt",
        [FeatureKeys.TseStatus] = "TSE status",
        [FeatureKeys.Backup] = "Backup",
        [FeatureKeys.DepExport] = "DEP export",
        [FeatureKeys.Other] = "Other",
    };
}

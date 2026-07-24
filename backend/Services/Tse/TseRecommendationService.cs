using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Generates and tracks TSE smart recommendations from devices, certs, failovers, and cost signals.
/// Apply/dismiss/rate are advisory workflow markers — never mutate fiscal signing chains.
/// </summary>
public sealed class TseRecommendationService : ITseRecommendationService
{
    private const int LookbackDays = 30;

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseRecommendationService> _logger;

    public TseRecommendationService(
        AppDbContext db,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseRecommendationService> logger)
    {
        _db = db;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TseRecommendationDto>> GetRecommendationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(tenantId));

        await RequireTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        await RefreshCandidatesAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var rows = await _db.TseRecommendations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDismissed)
            .OrderByDescending(r => ImpactRank(r.Impact))
            .ThenByDescending(r => r.EstimatedSavings)
            .ThenBy(r => r.EffortScore)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(Map).ToList();
    }

    public async Task<TseRecommendationResultDto> ApplyRecommendationAsync(
        Guid recommendationId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadMutableAsync(recommendationId, cancellationToken).ConfigureAwait(false);
        if (row.IsDismissed)
        {
            return new TseRecommendationResultDto
            {
                RecommendationId = recommendationId,
                Success = false,
                Message = "Recommendation was dismissed and cannot be applied.",
                Recommendation = Map(row),
            };
        }

        if (row.IsApplied)
        {
            return new TseRecommendationResultDto
            {
                RecommendationId = recommendationId,
                Success = true,
                Message = "Recommendation was already marked as applied (advisory only).",
                Recommendation = Map(row),
            };
        }

        row.IsApplied = true;
        row.AppliedAt = DateTime.UtcNow;
        row.AppliedBy = TruncateActor(actorUserId);
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "TSE recommendation applied Id={Id} Code={Code} TenantId={TenantId} Actor={Actor}",
            row.Id,
            row.Code,
            row.TenantId,
            actorUserId);

        return new TseRecommendationResultDto
        {
            RecommendationId = row.Id,
            Success = true,
            Message =
                "Recommendation marked as applied. No fiscal TSE signing state was changed — follow the description manually if action is still required.",
            Recommendation = Map(row),
        };
    }

    public async Task<TseRecommendationResultDto> DismissRecommendationAsync(
        Guid recommendationId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadMutableAsync(recommendationId, cancellationToken).ConfigureAwait(false);
        if (row.IsDismissed)
        {
            return new TseRecommendationResultDto
            {
                RecommendationId = recommendationId,
                Success = true,
                Message = "Recommendation was already dismissed.",
                Recommendation = Map(row),
            };
        }

        row.IsDismissed = true;
        row.DismissedAt = DateTime.UtcNow;
        row.DismissedBy = TruncateActor(actorUserId);
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseRecommendationResultDto
        {
            RecommendationId = row.Id,
            Success = true,
            Message = "Recommendation dismissed.",
            Recommendation = Map(row),
        };
    }

    public async Task<TseRecommendationFeedbackDto> RateRecommendationAsync(
        Guid recommendationId,
        int rating,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        var row = await LoadMutableAsync(recommendationId, cancellationToken).ConfigureAwait(false);
        row.Rating = rating;
        row.RatedAt = DateTime.UtcNow;
        row.RatedBy = TruncateActor(actorUserId);
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TseRecommendationFeedbackDto
        {
            RecommendationId = row.Id,
            Rating = rating,
            RatedAt = row.RatedAt.Value,
            Success = true,
            Message = "Feedback recorded.",
            Recommendation = Map(row),
        };
    }

    private async Task RefreshCandidatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var candidates = await BuildCandidatesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0)
            return;

        var open = await _db.TseRecommendations
            .Where(r => r.TenantId == tenantId && !r.IsApplied && !r.IsDismissed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byCode = open.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var touched = false;

        foreach (var candidate in candidates)
        {
            if (byCode.TryGetValue(candidate.Code, out var existing))
            {
                existing.Title = candidate.Title;
                existing.Description = candidate.Description;
                existing.Category = candidate.Category;
                existing.Impact = candidate.Impact;
                existing.EstimatedSavings = candidate.EstimatedSavings;
                existing.EffortScore = candidate.EffortScore;
                existing.UpdatedAt = now;
                touched = true;
                continue;
            }

            // Skip recreating if an applied/dismissed row already exists for this code recently.
            var alreadyClosed = await _db.TseRecommendations.AsNoTracking()
                .AnyAsync(
                    r => r.TenantId == tenantId
                         && r.Code == candidate.Code
                         && (r.IsApplied || r.IsDismissed)
                         && r.CreatedAt >= now.AddDays(-LookbackDays),
                    cancellationToken)
                .ConfigureAwait(false);
            if (alreadyClosed)
                continue;

            _db.TseRecommendations.Add(new TseRecommendation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = candidate.Code,
                Category = candidate.Category,
                Title = candidate.Title,
                Description = candidate.Description,
                Impact = candidate.Impact,
                EstimatedSavings = candidate.EstimatedSavings,
                EffortScore = candidate.EffortScore,
                CreatedAt = now,
            });
            touched = true;
        }

        if (touched)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<Candidate>> BuildCandidatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var opts = _tseOptions.CurrentValue;
        var devices = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = new List<Candidate>();
        var primaries = devices.Where(d => d.IsPrimary || d.IsFailoverActive).ToList();
        var backups = devices.Where(d => d.IsBackup && !d.IsFailoverActive).ToList();
        var now = DateTime.UtcNow;
        var fromUtc = now.AddDays(-LookbackDays);

        foreach (var primary in primaries)
        {
            var hasHealthyBackup = backups.Any(b =>
                (b.PrimaryDeviceId == primary.Id || b.PrimaryDeviceId is null)
                && b.HealthStatus == TseHealthStatus.Healthy
                && b.HealthScore >= opts.FailoverHealthyMinScore);

            if (!hasHealthyBackup)
            {
                list.Add(new Candidate(
                    "ensure_healthy_backup",
                    TseRecommendationCategories.Reliability,
                    "Ensure a healthy backup TSE",
                    $"Primary {DeviceLabel(primary)} has no healthy backup suitable for failover. Provision or repair a backup device.",
                    TseRecommendationImpacts.High,
                    EstimatedSavings: 0,
                    EffortScore: 6));
            }
        }

        var expiringSoonDays = Math.Max(1, opts.CertificateExpiringSoonDays);
        foreach (var device in devices.Where(d => d.ExpiresAt.HasValue))
        {
            var days = (device.ExpiresAt!.Value.ToUniversalTime().Date - now.Date).Days;
            if (days < 0)
            {
                list.Add(new Candidate(
                    $"cert_expired_{device.Id:N}",
                    TseRecommendationCategories.Security,
                    "Renew expired TSE certificate",
                    $"Device {DeviceLabel(device)} certificate expired {-days} day(s) ago. Renew before production signing continues.",
                    TseRecommendationImpacts.High,
                    0,
                    8));
            }
            else if (days <= expiringSoonDays)
            {
                list.Add(new Candidate(
                    $"cert_expiring_{device.Id:N}",
                    TseRecommendationCategories.Security,
                    "Schedule TSE certificate renewal",
                    $"Device {DeviceLabel(device)} certificate expires in {days} day(s). Schedule renewal to avoid signing outage.",
                    days <= 7 ? TseRecommendationImpacts.High : TseRecommendationImpacts.Medium,
                    0,
                    5));
            }
        }

        var degraded = devices.Where(d =>
            d.HealthStatus is TseHealthStatus.Degraded or TseHealthStatus.Offline
            || d.HealthScore < opts.FailoverDegradedMinScore).ToList();
        if (degraded.Count > 0)
        {
            list.Add(new Candidate(
                "repair_degraded_devices",
                TseRecommendationCategories.Performance,
                "Investigate degraded / offline TSE devices",
                $"{degraded.Count} active device(s) are degraded or offline. Run health checks and review latency/error samples.",
                TseRecommendationImpacts.High,
                0,
                7));
        }

        if (backups.Count > Math.Max(1, primaries.Count))
        {
            var excess = backups.Count - Math.Max(1, primaries.Count);
            var savings = (int)Math.Round(excess * Math.Max(0m, opts.CostMonthlyBackupDeviceEur));
            list.Add(new Candidate(
                "reduce_idle_backups",
                TseRecommendationCategories.Cost,
                "Reduce idle backup TSE devices",
                $"{excess} backup device(s) exceed primary capacity needs. Keep one healthy spare per primary where possible.",
                TseRecommendationImpacts.Medium,
                savings,
                4));
        }

        var failoverCount = await _db.TseFailoverLogs.AsNoTracking().IgnoreQueryFilters()
            .CountAsync(
                l => l.TenantId == tenantId
                     && l.StartedAt >= fromUtc
                     && l.IsSuccessful,
                cancellationToken)
            .ConfigureAwait(false);
        if (failoverCount >= Math.Max(1, opts.CostHighFailoverCountThreshold))
        {
            list.Add(new Candidate(
                "stabilize_failover_churn",
                TseRecommendationCategories.Reliability,
                "Stabilize frequent TSE failovers",
                $"{failoverCount} successful failover(s) in the last {LookbackDays} days. Investigate primary stability to reduce churn.",
                TseRecommendationImpacts.High,
                (int)Math.Round(failoverCount * Math.Max(0m, opts.CostPerFailoverEventEur)),
                8));
        }

        var receiptStats = await _db.Receipts.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.IssuedAt >= fromUtc)
            .Select(r => new { r.SignatureValue })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var total = receiptStats.Count;
        var signed = receiptStats.Count(r => !string.IsNullOrWhiteSpace(r.SignatureValue));
        if (total > 0 && signed < total)
        {
            var unsigned = total - signed;
            list.Add(new Candidate(
                "fix_unsigned_receipts",
                TseRecommendationCategories.Security,
                "Reduce unsigned fiscal receipts",
                $"{unsigned} of {total} receipt(s) in the last {LookbackDays} days lack a TSE signature. Fix signing path / offline backlog.",
                TseRecommendationImpacts.High,
                0,
                7));
        }

        if (primaries.Count > 1 && total > 0)
        {
            var dailyPerPrimary = (signed / (double)primaries.Count) / LookbackDays;
            if (dailyPerPrimary < opts.CostLowUtilizationDailyTxThreshold)
            {
                var savings = (int)Math.Round(
                    (primaries.Count - 1) * Math.Max(0m, opts.CostMonthlyPrimaryDeviceEur));
                list.Add(new Candidate(
                    "consolidate_low_utilization",
                    TseRecommendationCategories.Cost,
                    "Consolidate low-utilization primary TSE devices",
                    $"Average ~{dailyPerPrimary:0.#} signed tx/day per primary is below the {opts.CostLowUtilizationDailyTxThreshold} threshold.",
                    TseRecommendationImpacts.Low,
                    savings,
                    9));
            }
        }

        if (list.Count == 0)
        {
            list.Add(new Candidate(
                "healthy_operations",
                TseRecommendationCategories.Reliability,
                "TSE operations look healthy",
                "No high-priority cost, security, or reliability issues detected for this tenant in the current lookback window.",
                TseRecommendationImpacts.Low,
                0,
                1));
        }

        // Cap device-specific cert codes explosion: keep max 20 candidates.
        return list
            .OrderByDescending(c => ImpactRank(c.Impact))
            .ThenByDescending(c => c.EstimatedSavings)
            .Take(20)
            .ToList();
    }

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new KeyNotFoundException($"Tenant {tenantId} was not found.");
    }

    private async Task<TseRecommendation> LoadMutableAsync(
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        if (recommendationId == Guid.Empty)
            throw new ArgumentException("recommendationId is required.", nameof(recommendationId));

        var row = await _db.TseRecommendations
            .FirstOrDefaultAsync(r => r.Id == recommendationId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Recommendation {recommendationId} was not found.");
        return row;
    }

    private static TseRecommendationDto Map(TseRecommendation row) =>
        new()
        {
            Id = row.Id,
            TenantId = row.TenantId,
            Code = row.Code,
            Category = row.Category,
            Title = row.Title,
            Description = row.Description,
            Impact = row.Impact,
            EstimatedSavings = row.EstimatedSavings,
            EffortScore = row.EffortScore,
            CreatedAt = row.CreatedAt,
            IsApplied = row.IsApplied,
            AppliedAt = row.AppliedAt,
            IsDismissed = row.IsDismissed,
            Rating = row.Rating,
            DiagnosticOnly = true,
        };

    private static int ImpactRank(string? impact) => impact switch
    {
        TseRecommendationImpacts.High => 3,
        TseRecommendationImpacts.Medium => 2,
        TseRecommendationImpacts.Low => 1,
        _ => 0,
    };

    private static string DeviceLabel(TseDevice d) =>
        string.IsNullOrWhiteSpace(d.SerialNumber) ? d.Id.ToString("N")[..8] : d.SerialNumber;

    private static string? TruncateActor(string? actor) =>
        string.IsNullOrWhiteSpace(actor)
            ? null
            : (actor.Length <= 450 ? actor : actor[..450]);

    private sealed record Candidate(
        string Code,
        string Category,
        string Title,
        string Description,
        string Impact,
        int EstimatedSavings,
        int EffortScore);
}

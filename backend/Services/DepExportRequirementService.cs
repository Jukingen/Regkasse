using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportRequirementService
{
    Task<IReadOnlyList<DepExportRequirement>> GetRequirementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<DepExportRequirement?> GetNextRequirementAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<DepExportComplianceStatus> GetComplianceStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Current open compliance period (Pending / InProgress / Overdue), preferring Yearly then Quarterly then Monthly.
    /// </summary>
    Task<DepExportCompliancePeriod?> GetCurrentPeriodAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures yearly/quarterly (and optionally monthly) period rows exist and refreshes Overdue status.
    /// </summary>
    Task EnsurePeriodsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks overlapping open periods Completed when a DEP export history row is recorded.
    /// </summary>
    Task TryCompletePeriodsForExportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        string exportedBy,
        string? fileName,
        string? fileHash,
        Guid? historyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Computes DEP export legal / recommended / optional requirements and tracks period completion.
/// Yearly legal deadline aligns with RKSV Jahresbeleg practice (31 January of the following year).
/// </summary>
public sealed class DepExportRequirementService : IDepExportRequirementService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public DepExportRequirementService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<DepExportRequirement>> GetRequirementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePeriodsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var requirements = new List<DepExportRequirement>();

        var yearStart = UtcDate(now.Year - 1, 1, 1);
        var yearEnd = UtcDate(now.Year - 1, 12, 31);
        var yearDeadline = UtcDate(now.Year, 1, 31);

        var yearCompleted = await IsPeriodCompletedAsync(
                tenantId,
                DepExportPeriodTypes.Yearly,
                yearStart,
                yearEnd,
                cancellationToken)
            .ConfigureAwait(false);

        requirements.Add(new DepExportRequirement
        {
            TenantId = tenantId,
            RequirementType = DepExportRequirementTypes.Legal,
            Title = "Jährlicher DEP Export",
            Description = $"Export für das Jahr {yearStart.Year}",
            DueDate = yearDeadline,
            IsCompleted = yearCompleted,
            Priority = 5,
            Category = DepExportRequirementCategories.Yearly,
            PeriodStart = yearStart,
            PeriodEnd = yearEnd,
        });

        if (!yearCompleted && (yearDeadline - now).TotalDays is >= 0 and < 30)
        {
            requirements.Add(new DepExportRequirement
            {
                TenantId = tenantId,
                RequirementType = DepExportRequirementTypes.Legal,
                Title = "DEP Export fällig",
                Description =
                    $"DEP Export für {yearStart.Year} muss bis {yearDeadline:dd.MM.yyyy} erstellt werden.",
                DueDate = yearDeadline,
                IsCompleted = false,
                Priority = 5,
                Category = DepExportRequirementCategories.Urgent,
                PeriodStart = yearStart,
                PeriodEnd = yearEnd,
            });
        }

        var quarterNumber = (now.Month - 1) / 3 + 1;
        var quarterStart = UtcDate(now.Year, (quarterNumber - 1) * 3 + 1, 1);
        var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
        var quarterDue = quarterEnd.AddMonths(1);
        var quarterCompleted = await IsPeriodCompletedAsync(
                tenantId,
                DepExportPeriodTypes.Quarterly,
                quarterStart,
                quarterEnd,
                cancellationToken)
            .ConfigureAwait(false);

        requirements.Add(new DepExportRequirement
        {
            TenantId = tenantId,
            RequirementType = DepExportRequirementTypes.Recommended,
            Title = $"Quartals DEP Export - Q{quarterNumber}",
            Description = $"Export für Q{quarterNumber} {now.Year}",
            DueDate = quarterDue,
            IsCompleted = quarterCompleted,
            Priority = 3,
            Category = DepExportRequirementCategories.Quarterly,
            PeriodStart = quarterStart,
            PeriodEnd = quarterEnd,
        });

        // Optional monthly reminder on the first day of the month (previous calendar month).
        if (now.Day == 1)
        {
            var monthStart = UtcDate(now.Year, now.Month, 1).AddMonths(-1);
            var monthEnd = UtcDate(now.Year, now.Month, 1).AddDays(-1);
            var monthCompleted = await IsPeriodCompletedAsync(
                    tenantId,
                    DepExportPeriodTypes.Monthly,
                    monthStart,
                    monthEnd,
                    cancellationToken)
                .ConfigureAwait(false);

            requirements.Add(new DepExportRequirement
            {
                TenantId = tenantId,
                RequirementType = DepExportRequirementTypes.Optional,
                Title = $"Monatlicher DEP Export - {monthStart:MMMM yyyy}",
                Description = $"Export für {monthStart:MMMM yyyy}",
                DueDate = UtcDate(now.Year, now.Month, 1).AddMonths(1),
                IsCompleted = monthCompleted,
                Priority = 2,
                Category = DepExportRequirementCategories.Monthly,
                PeriodStart = monthStart,
                PeriodEnd = monthEnd,
            });
        }

        return requirements
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
            .ToList();
    }

    public async Task<DepExportRequirement?> GetNextRequirementAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var requirements = await GetRequirementsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return requirements
            .Where(r => !r.IsCompleted)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
            .FirstOrDefault();
    }

    public async Task<DepExportComplianceStatus> GetComplianceStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var requirements = await GetRequirementsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var currentPeriod = await GetCurrentPeriodAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var completed = requirements.Count(r => r.IsCompleted);
        var overdue = requirements.Count(r =>
            !r.IsCompleted && r.DueDate is DateTime due && due < now);
        var pending = requirements.Count(r => !r.IsCompleted) - overdue;
        var legalIncomplete = requirements.Count(r =>
            !r.IsCompleted && r.RequirementType == DepExportRequirementTypes.Legal);

        return new DepExportComplianceStatus
        {
            TenantId = tenantId,
            IsCompliant = legalIncomplete == 0,
            TotalRequirements = requirements.Count,
            CompletedCount = completed,
            PendingCount = Math.Max(0, pending),
            OverdueCount = overdue,
            LegalIncompleteCount = legalIncomplete,
            NextRequirement = requirements
                .Where(r => !r.IsCompleted)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.DueDate ?? DateTime.MaxValue)
                .FirstOrDefault(),
            CurrentPeriod = currentPeriod,
            CheckedAtUtc = now,
        };
    }

    public async Task<DepExportCompliancePeriod?> GetCurrentPeriodAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePeriodsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var openStatuses = new[]
        {
            DepExportPeriodStatuses.Pending,
            DepExportPeriodStatuses.InProgress,
            DepExportPeriodStatuses.Overdue,
        };

        var periods = await _db.DepExportCompliancePeriods
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && openStatuses.Contains(p.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return periods
            .OrderBy(p => PeriodTypeRank(p.PeriodType))
            .ThenBy(p => p.PeriodStart)
            .FirstOrDefault();
    }

    public async Task EnsurePeriodsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;

        var yearStart = UtcDate(now.Year - 1, 1, 1);
        var yearEnd = UtcDate(now.Year - 1, 12, 31);
        changed |= await EnsurePeriodRowAsync(
                tenantId,
                DepExportPeriodTypes.Yearly,
                yearStart,
                yearEnd,
                dueDate: UtcDate(now.Year, 1, 31),
                cancellationToken)
            .ConfigureAwait(false);

        var quarterNumber = (now.Month - 1) / 3 + 1;
        var quarterStart = UtcDate(now.Year, (quarterNumber - 1) * 3 + 1, 1);
        var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
        changed |= await EnsurePeriodRowAsync(
                tenantId,
                DepExportPeriodTypes.Quarterly,
                quarterStart,
                quarterEnd,
                dueDate: quarterEnd.AddMonths(1),
                cancellationToken)
            .ConfigureAwait(false);

        if (changed)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RefreshOverdueStatusesAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
        await SyncCompletedFromHistoryAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task TryCompletePeriodsForExportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        string exportedBy,
        string? fileName,
        string? fileHash,
        Guid? historyId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePeriodsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var from = NormalizeUtc(fromUtc);
        var to = NormalizeUtc(toUtc);

        var candidates = await _db.DepExportCompliancePeriods
            .Where(p => p.TenantId == tenantId && p.Status != DepExportPeriodStatuses.Completed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targets = candidates
            .Where(p => ExportCoversPeriod(from, to, p.PeriodStart, p.PeriodEnd))
            .ToList();

        if (targets.Count == 0)
            return;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var period in targets)
        {
            period.Status = DepExportPeriodStatuses.Completed;
            period.ExportedAt = now;
            period.ExportedBy = exportedBy;
            period.FileName = fileName;
            period.FileHash = fileHash;
            period.HistoryId = historyId;
            period.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private async Task<bool> EnsurePeriodRowAsync(
        Guid tenantId,
        string periodType,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime dueDate,
        CancellationToken cancellationToken)
    {
        var exists = await _db.DepExportCompliancePeriods
            .AnyAsync(
                p => p.TenantId == tenantId &&
                     p.PeriodType == periodType &&
                     p.PeriodStart == periodStart &&
                     p.PeriodEnd == periodEnd,
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var status = now.Date > dueDate.Date
            ? DepExportPeriodStatuses.Overdue
            : DepExportPeriodStatuses.Pending;

        _db.DepExportCompliancePeriods.Add(new DepExportCompliancePeriod
        {
            TenantId = tenantId,
            PeriodType = periodType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = status,
            CreatedAt = now,
        });
        return true;
    }

    private async Task RefreshOverdueStatusesAsync(
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var yearDeadline = UtcDate(now.Year, 1, 31);
        var open = await _db.DepExportCompliancePeriods
            .Where(p =>
                p.TenantId == tenantId &&
                (p.Status == DepExportPeriodStatuses.Pending || p.Status == DepExportPeriodStatuses.InProgress))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var changed = false;
        foreach (var period in open)
        {
            var due = period.PeriodType switch
            {
                DepExportPeriodTypes.Yearly => yearDeadline,
                DepExportPeriodTypes.Quarterly => period.PeriodEnd.AddMonths(1),
                _ => period.PeriodEnd.AddMonths(1),
            };

            if (now.Date <= due.Date)
                continue;

            period.Status = DepExportPeriodStatuses.Overdue;
            period.UpdatedAt = now;
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncCompletedFromHistoryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var open = await _db.DepExportCompliancePeriods
            .Where(p => p.TenantId == tenantId && p.Status != DepExportPeriodStatuses.Completed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (open.Count == 0)
            return;

        var histories = await _db.DepExportHistories
            .AsNoTracking()
            .Where(h =>
                h.TenantId == tenantId &&
                h.Status == DepExportStatus.Completed.ToString())
            .Select(h => new { h.Id, h.FromUtc, h.ToUtc, h.ExportedAt, h.ExportedByUserId, h.FileName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (histories.Count == 0)
            return;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;
        foreach (var period in open)
        {
            var match = histories
                .Where(h => ExportCoversPeriod(h.FromUtc, h.ToUtc, period.PeriodStart, period.PeriodEnd))
                .OrderByDescending(h => h.ExportedAt)
                .FirstOrDefault();
            if (match is null)
                continue;

            period.Status = DepExportPeriodStatuses.Completed;
            period.ExportedAt = match.ExportedAt;
            period.ExportedBy = match.ExportedByUserId;
            period.FileName = match.FileName;
            period.HistoryId = match.Id;
            period.UpdatedAt = now;
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsPeriodCompletedAsync(
        Guid tenantId,
        string periodType,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var periodDone = await _db.DepExportCompliancePeriods
            .AsNoTracking()
            .AnyAsync(
                p => p.TenantId == tenantId &&
                     p.PeriodType == periodType &&
                     p.PeriodStart == periodStart &&
                     p.PeriodEnd == periodEnd &&
                     p.Status == DepExportPeriodStatuses.Completed,
                cancellationToken)
            .ConfigureAwait(false);
        if (periodDone)
            return true;

        return await _db.DepExportHistories
            .AsNoTracking()
            .AnyAsync(
                h => h.TenantId == tenantId &&
                     h.Status == DepExportStatus.Completed.ToString() &&
                     h.FromUtc <= periodStart &&
                     h.ToUtc >= periodEnd,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static bool ExportCoversPeriod(
        DateTime exportFrom,
        DateTime exportTo,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var from = exportFrom.Date;
        var to = exportTo.Date;
        var start = periodStart.Date;
        var end = periodEnd.Date;
        return from <= start && to >= end;
    }

    private static int PeriodTypeRank(string periodType) =>
        periodType switch
        {
            DepExportPeriodTypes.Yearly => 0,
            DepExportPeriodTypes.Quarterly => 1,
            DepExportPeriodTypes.Monthly => 2,
            _ => 9,
        };

    private static DateTime UtcDate(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}

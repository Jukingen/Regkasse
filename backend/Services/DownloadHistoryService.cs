using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class DownloadHistoryRecordRequest
{
    public required Guid TenantId { get; init; }
    public required string UserId { get; init; }
    public required string FileName { get; init; }
    public required string FileType { get; init; }
    public long? FileSize { get; init; }
    public string? DownloadUrl { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public DateTime? DownloadedAtUtc { get; init; }
    public int? DurationMs { get; init; }
}

public sealed class DownloadHistoryListItemDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public long? FileSize { get; init; }
    public string? DownloadUrl { get; init; }
    public DateTime DownloadedAt { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public bool CanRedownload { get; init; }
}

public sealed class DownloadHistoryListResponse
{
    public IReadOnlyList<DownloadHistoryListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class DownloadHistoryStatsDto
{
    public int FileCount { get; init; }
    public long TotalBytes { get; init; }
    public int RetentionDays { get; init; }
}

public sealed class DownloadHistoryCleanupResultDto
{
    public int DeletedCount { get; init; }
    public int RetentionDays { get; init; }
}

public interface IDownloadHistoryService
{
    Task<DownloadHistory> RecordAsync(DownloadHistoryRecordRequest request, CancellationToken cancellationToken = default);

    Task<DownloadHistoryListResponse> ListAsync(
        Guid tenantId,
        string? userId = null,
        string? fileType = null,
        string? sourceKind = null,
        string? search = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<DownloadHistoryStatsDto> GetStatsAsync(
        Guid tenantId,
        string? userId = null,
        int retentionDays = 30,
        CancellationToken cancellationToken = default);

    Task<DownloadHistoryAnalyticsDto> GetAnalyticsAsync(
        Guid? tenantId,
        bool includePlatformTenants,
        int retentionDays = 30,
        CancellationToken cancellationToken = default);

    Task<DownloadHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes rows older than <paramref name="olderThanUtc"/> across all tenants (hosted retention).</summary>
    Task<int> CleanupOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);

    /// <summary>Deletes rows older than <paramref name="olderThanUtc"/> for a single tenant (manual UI action).</summary>
    Task<int> CleanupTenantOlderThanAsync(
        Guid tenantId,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default);
}

public sealed class DownloadHistoryService : IDownloadHistoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DownloadHistoryService> _logger;
    private readonly IAuditLogService _audit;

    public DownloadHistoryService(
        AppDbContext db,
        ILogger<DownloadHistoryService> logger,
        IAuditLogService audit)
    {
        _db = db;
        _logger = logger;
        _audit = audit;
    }

    public async Task<DownloadHistory> RecordAsync(
        DownloadHistoryRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileType);

        var row = new DownloadHistory
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            UserId = request.UserId.Trim(),
            FileName = request.FileName.Trim(),
            FileType = NormalizeFileType(request.FileType),
            FileSize = request.FileSize is > 0 ? request.FileSize : null,
            DownloadUrl = NormalizeOptionalText(request.DownloadUrl, maxLength: 2000),
            DownloadedAt = request.DownloadedAtUtc ?? DateTime.UtcNow,
            IpAddress = NormalizeOptionalText(request.IpAddress, maxLength: 128),
            UserAgent = NormalizeOptionalText(request.UserAgent, maxLength: 500),
            SourceKind = string.IsNullOrWhiteSpace(request.SourceKind) ? null : request.SourceKind.Trim().ToLowerInvariant(),
            SourceId = request.SourceId,
            DurationMs = request.DurationMs is > 0 ? request.DurationMs : null,
        };

        _db.DownloadHistories.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Download history recorded. Id={Id} TenantId={TenantId} FileName={FileName} SourceKind={SourceKind}",
            row.Id,
            row.TenantId,
            row.FileName,
            row.SourceKind);

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "FILE_DOWNLOAD",
                entityType: "DownloadHistory",
                userId: row.UserId,
                userRole: "Unknown",
                description: $"File download recorded: {row.FileName}",
                requestData: new
                {
                    row.FileName,
                    row.FileType,
                    row.FileSize,
                    row.SourceKind,
                    row.SourceId,
                },
                actionType: AuditEventType.FileDownloaded,
                entityId: row.Id,
                tenantId: row.TenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to audit download history record {Id}", row.Id);
        }

        return row;
    }

    public async Task<DownloadHistoryListResponse> ListAsync(
        Guid tenantId,
        string? userId = null,
        string? fileType = null,
        string? sourceKind = null,
        string? search = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildFilteredQuery(tenantId, userId, fileType, sourceKind, search, fromUtc, toUtc);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(h => h.DownloadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new DownloadHistoryListItemDto
            {
                Id = h.Id,
                FileName = h.FileName,
                FileType = h.FileType,
                FileSize = h.FileSize,
                DownloadUrl = h.DownloadUrl,
                DownloadedAt = h.DownloadedAt,
                UserId = h.UserId,
                IpAddress = h.IpAddress,
                UserAgent = h.UserAgent,
                SourceKind = h.SourceKind,
                SourceId = h.SourceId,
                CanRedownload = h.SourceKind != null && h.SourceId != null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DownloadHistoryListResponse
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<DownloadHistoryStatsDto> GetStatsAsync(
        Guid tenantId,
        string? userId = null,
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        var query = _db.DownloadHistories.AsNoTracking()
            .Where(h => h.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(h => h.UserId == userId);

        var fileCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalBytes = await query
            .Where(h => h.FileSize != null)
            .SumAsync(h => h.FileSize!.Value, cancellationToken)
            .ConfigureAwait(false);

        return new DownloadHistoryStatsDto
        {
            FileCount = fileCount,
            TotalBytes = totalBytes,
            RetentionDays = Math.Clamp(retentionDays, 1, 365),
        };
    }

    public async Task<DownloadHistoryAnalyticsDto> GetAnalyticsAsync(
        Guid? tenantId,
        bool includePlatformTenants,
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        retentionDays = Math.Clamp(retentionDays, 1, 365);
        var now = DateTime.UtcNow;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dailyFrom = todayStart.AddDays(-29);
        var monthlyFrom = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        IQueryable<DownloadHistory> baseQuery = includePlatformTenants
            ? _db.DownloadHistories.AsNoTracking().IgnoreQueryFilters()
            : _db.DownloadHistories.AsNoTracking();

        if (!includePlatformTenants)
        {
            if (tenantId is null)
                throw new ArgumentNullException(nameof(tenantId));
            baseQuery = baseQuery.Where(h => h.TenantId == tenantId.Value);
        }

        var rows = await baseQuery
            .Select(h => new
            {
                h.Id,
                h.TenantId,
                h.UserId,
                h.FileName,
                h.FileType,
                h.FileSize,
                h.SourceKind,
                h.DownloadedAt,
                h.DurationMs,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = rows.Count;
        var todayCount = rows.Count(r => r.DownloadedAt >= todayStart);
        var monthCount = rows.Count(r => r.DownloadedAt >= monthStart);
        var totalBytes = rows.Where(r => r.FileSize is > 0).Sum(r => r.FileSize!.Value);

        var kindGroups = rows
            .GroupBy(r => BuildKindKey(r.SourceKind, r.FileType))
            .Select(g => new { Key = g.Key, Label = FormatKindLabel(g.Key), Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(8)
            .ToList();

        var topKinds = kindGroups
            .Select(g => new DownloadHistoryKindStatDto
            {
                Key = g.Key,
                Label = g.Label,
                Count = g.Count,
                Percent = totalCount == 0 ? 0 : Math.Round(100.0 * g.Count / totalCount, 1),
            })
            .ToList();

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var nameById = userNames.ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.UserName)
                ? (u.Email ?? u.Id)
                : u.UserName!);

        var topUsers = rows
            .GroupBy(r => r.UserId)
            .Select(g => new DownloadHistoryUserStatDto
            {
                UserId = g.Key,
                DisplayName = nameById.TryGetValue(g.Key, out var n) ? n : g.Key,
                Count = g.Count(),
            })
            .OrderByDescending(u => u.Count)
            .Take(10)
            .ToList();

        IReadOnlyList<DownloadHistoryTenantStatDto> topTenants = [];
        if (includePlatformTenants)
        {
            var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
            var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Slug, t.Name })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var tenantMap = tenants.ToDictionary(t => t.Id);

            topTenants = rows
                .GroupBy(r => r.TenantId)
                .Select(g =>
                {
                    tenantMap.TryGetValue(g.Key, out var t);
                    return new DownloadHistoryTenantStatDto
                    {
                        TenantId = g.Key,
                        TenantSlug = t?.Slug ?? g.Key.ToString("N")[..8],
                        TenantName = t?.Name ?? t?.Slug ?? g.Key.ToString("D"),
                        Count = g.Count(),
                        Percent = totalCount == 0 ? 0 : Math.Round(100.0 * g.Count() / totalCount, 1),
                    };
                })
                .OrderByDescending(t => t.Count)
                .Take(10)
                .ToList();
        }

        var dailyTrend = BuildDailyTrend(rows.Select(r => (r.DownloadedAt, r.FileSize)), dailyFrom, todayStart, 30);
        var weeklyTrend = BuildWeeklyTrend(rows.Select(r => (r.DownloadedAt, r.FileSize)), todayStart, 12);
        var monthlyTrend = BuildMonthlyTrend(rows.Select(r => (r.DownloadedAt, r.FileSize)), monthlyFrom, 12);

        var slowExports = rows
            .OrderByDescending(r => r.DurationMs ?? 0)
            .ThenByDescending(r => r.FileSize ?? 0)
            .Take(15)
            .Select(r => new DownloadHistorySlowExportDto
            {
                Id = r.Id,
                FileName = r.FileName,
                SourceKind = r.SourceKind,
                FileType = r.FileType,
                FileSize = r.FileSize,
                DurationMs = r.DurationMs,
                DownloadedAt = r.DownloadedAt,
                UserId = r.UserId,
                DisplayName = nameById.TryGetValue(r.UserId, out var n) ? n : r.UserId,
                RankBy = r.DurationMs is > 0 ? "duration" : "size",
            })
            .Where(r => r.DurationMs is > 0 || r.FileSize is > 0)
            .Take(10)
            .ToList();

        return new DownloadHistoryAnalyticsDto
        {
            TotalCount = totalCount,
            TodayCount = todayCount,
            MonthCount = monthCount,
            TotalBytes = totalBytes,
            RetentionDays = retentionDays,
            IncludesPlatformTenants = includePlatformTenants,
            TopKinds = topKinds,
            TopUsers = topUsers,
            TopTenants = topTenants,
            DailyTrend = dailyTrend,
            WeeklyTrend = weeklyTrend,
            MonthlyTrend = monthlyTrend,
            SlowExports = slowExports,
        };
    }

    private static string BuildKindKey(string? sourceKind, string fileType)
    {
        var type = string.IsNullOrWhiteSpace(fileType) ? "bin" : fileType.Trim().ToLowerInvariant();
        var kind = string.IsNullOrWhiteSpace(sourceKind) ? "" : sourceKind.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(kind) ? $"type:{type}" : $"{kind}|{type}";
    }

    internal static string FormatKindLabel(string kindKey)
    {
        var parts = kindKey.Split('|', 2);
        string kind;
        string type;
        if (parts.Length == 2)
        {
            kind = parts[0];
            type = parts[1].ToUpperInvariant();
        }
        else if (kindKey.StartsWith("type:", StringComparison.Ordinal))
        {
            kind = "";
            type = kindKey["type:".Length..].ToUpperInvariant();
        }
        else
        {
            kind = kindKey;
            type = "BIN";
        }

        return kind switch
        {
            "invoice" => $"Invoice ({type})",
            "dep-export" or "dep-export-live" => $"DEP Export ({type})",
            "backup" or "tenant-backup" or "system-backup" => $"Backup ({type})",
            "receipt-pdf-batch" => $"Belege ({type})",
            "tagesabschluss" or "tagesbericht" or "daily-closing" => $"Tagesbericht ({type})",
            "" => type,
            _ => $"{kind} ({type})",
        };
    }

    private static IReadOnlyList<DownloadHistoryTrendPointDto> BuildDailyTrend(
        IEnumerable<(DateTime At, long? Size)> rows,
        DateTime fromDay,
        DateTime todayStart,
        int days)
    {
        var map = rows
            .Where(r => r.At >= fromDay)
            .GroupBy(r => r.At.Date)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Bytes: g.Sum(x => x.Size ?? 0)));

        var list = new List<DownloadHistoryTrendPointDto>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i).Date;
            map.TryGetValue(day, out var v);
            list.Add(new DownloadHistoryTrendPointDto
            {
                PeriodKey = day.ToString("yyyy-MM-dd"),
                Label = day.ToString("dd.MM"),
                Count = v.Count,
                TotalBytes = v.Bytes,
            });
        }

        return list;
    }

    private static IReadOnlyList<DownloadHistoryTrendPointDto> BuildWeeklyTrend(
        IEnumerable<(DateTime At, long? Size)> rows,
        DateTime todayStart,
        int weeks)
    {
        static DateTime StartOfIsoWeek(DateTime d)
        {
            var diff = ((int)d.DayOfWeek + 6) % 7; // Monday=0
            return d.Date.AddDays(-diff);
        }

        var from = StartOfIsoWeek(todayStart).AddDays(-7 * (weeks - 1));
        var map = rows
            .Where(r => r.At >= from)
            .GroupBy(r => StartOfIsoWeek(r.At))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Bytes: g.Sum(x => x.Size ?? 0)));

        var list = new List<DownloadHistoryTrendPointDto>(weeks);
        for (var i = weeks - 1; i >= 0; i--)
        {
            var week = StartOfIsoWeek(todayStart).AddDays(-7 * i);
            map.TryGetValue(week, out var v);
            list.Add(new DownloadHistoryTrendPointDto
            {
                PeriodKey = week.ToString("yyyy-MM-dd"),
                Label = week.ToString("dd.MM"),
                Count = v.Count,
                TotalBytes = v.Bytes,
            });
        }

        return list;
    }

    private static IReadOnlyList<DownloadHistoryTrendPointDto> BuildMonthlyTrend(
        IEnumerable<(DateTime At, long? Size)> rows,
        DateTime fromMonth,
        int months)
    {
        var map = rows
            .Where(r => r.At >= fromMonth)
            .GroupBy(r => new DateTime(r.At.Year, r.At.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Bytes: g.Sum(x => x.Size ?? 0)));

        var list = new List<DownloadHistoryTrendPointDto>(months);
        var cursor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = months - 1; i >= 0; i--)
        {
            var month = cursor.AddMonths(-i);
            map.TryGetValue(month, out var v);
            list.Add(new DownloadHistoryTrendPointDto
            {
                PeriodKey = month.ToString("yyyy-MM"),
                Label = month.ToString("MM.yyyy"),
                Count = v.Count,
                TotalBytes = v.Bytes,
            });
        }

        return list;
    }

    public Task<DownloadHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.DownloadHistories.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<int> CleanupOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        // Cross-tenant retention: ignore ambient tenant filter.
        var deleted = await _db.DownloadHistories
            .IgnoreQueryFilters()
            .Where(h => h.DownloadedAt < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Download history cleanup removed {DeletedCount} row(s) older than {OlderThanUtc:o}.",
                deleted,
                olderThanUtc);
        }

        return deleted;
    }

    public async Task<int> CleanupTenantOlderThanAsync(
        Guid tenantId,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _db.DownloadHistories
            .Where(h => h.TenantId == tenantId && h.DownloadedAt < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Download history tenant cleanup removed {DeletedCount} row(s) for TenantId={TenantId} older than {OlderThanUtc:o}.",
                deleted,
                tenantId,
                olderThanUtc);
        }

        return deleted;
    }

    private IQueryable<DownloadHistory> BuildFilteredQuery(
        Guid tenantId,
        string? userId,
        string? fileType,
        string? sourceKind,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var query = _db.DownloadHistories.AsNoTracking()
            .Where(h => h.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(h => h.UserId == userId);

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            var normalized = NormalizeFileType(fileType);
            query = query.Where(h => h.FileType == normalized);
        }

        if (!string.IsNullOrWhiteSpace(sourceKind))
        {
            var kind = sourceKind.Trim().ToLowerInvariant();
            query = query.Where(h => h.SourceKind == kind);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(h => h.FileName.ToLower().Contains(term));
        }

        if (fromUtc is not null)
            query = query.Where(h => h.DownloadedAt >= fromUtc.Value);

        if (toUtc is not null)
            query = query.Where(h => h.DownloadedAt <= toUtc.Value);

        return query;
    }

    private static string NormalizeFileType(string fileType)
    {
        var t = fileType.Trim().TrimStart('.').ToLowerInvariant();
        return string.IsNullOrEmpty(t) ? "bin" : t.Length <= 64 ? t : t[..64];
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

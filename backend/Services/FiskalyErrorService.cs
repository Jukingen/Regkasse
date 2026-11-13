using System.Globalization;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class FiskalyErrorService : IFiskalyErrorService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxExportRows = 5000;
    public const int TopErrorLimit = 10;
    public static readonly TimeSpan StatsCacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLog;
    private readonly ICacheService _cache;

    public FiskalyErrorService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLog,
        ICacheService cache)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _auditLog = auditLog;
        _cache = cache;
    }

    public async Task<PagedResult<FiskalyErrorListItemDto>> ListAsync(
        FiskalyErrorQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = FiskalyStatisticsService.ResolveRange(query.FromUtc, query.ToUtc);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        var failedQuery = ApplyListFilters(
            ApplyScopeFilters(BaseQuery(actorIsSuperAdmin), query, actorIsSuperAdmin, fromUtc, toUtc),
            query);

        var totalCount = await failedQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var rows = await failedQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ErrorRow(
                x.Id,
                x.CreatedAtUtc,
                x.CompletedAtUtc,
                x.OperationType,
                x.Status,
                x.ErrorCode,
                x.ErrorMessage,
                x.TenantId,
                x.TenantName,
                x.UserId,
                x.UserDisplayName,
                x.CashRegisterId,
                x.ReceiptNumber,
                x.CashRegisterName,
                x.ErrorReviewStatus,
                x.ErrorReviewedAtUtc,
                x.ErrorReviewedByUserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<FiskalyErrorListItemDto>
        {
            Items = rows.Select(ToItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<FiskalyErrorStatsDto> GetStatsAsync(
        FiskalyErrorQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtc) = FiskalyStatisticsService.ResolveRange(query.FromUtc, query.ToUtc);
        var cacheKey = CacheKeys.Format(
            CacheKeys.FiskalyErrorStats,
            actorIsSuperAdmin ? "sa" : (_tenantAccessor.TenantId?.ToString("N") ?? "none"),
            fromUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            toUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(query.OperationType) ? "-" : query.OperationType.Trim().ToLowerInvariant(),
            actorIsSuperAdmin && query.TenantId is { } tenantFilter && tenantFilter != Guid.Empty
                ? tenantFilter.ToString("N")
                : "-");

        return await _cache
            .GetOrCreateAsync(
                cacheKey,
                ct => LoadStatsAsync(query, actorIsSuperAdmin, fromUtc, toUtc, ct),
                StatsCacheTtl,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FiskalyErrorDetailDto?> GetByIdAsync(
        Guid id,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        IQueryable<FiskalyOperationHistory> query = _db.FiskalyOperationHistories.AsNoTracking();
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();

        var row = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;
        if (!string.Equals(row.Status, FiskalyOperationHistoryStatuses.Failed, StringComparison.OrdinalIgnoreCase))
            return null;

        return ToDetail(row);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> ExportAsync(
        FiskalyErrorQuery query,
        string format,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var stats = await GetStatsAsync(query, actorIsSuperAdmin, cancellationToken).ConfigureAwait(false);
        var list = await ListAsync(
                new FiskalyErrorQuery
                {
                    FromUtc = query.FromUtc,
                    ToUtc = query.ToUtc,
                    OperationType = query.OperationType,
                    TenantId = query.TenantId,
                    ReviewStatus = query.ReviewStatus,
                    ErrorCode = query.ErrorCode,
                    Search = query.Search,
                    Page = 1,
                    PageSize = MaxExportRows
                },
                actorIsSuperAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "Z";
        var kind = (format ?? "csv").Trim().ToLowerInvariant();
        if (kind is "pdf")
        {
            return (
                FiskalyErrorPdfGenerator.Generate(stats, list.Items),
                "application/pdf",
                $"fiskaly-errors_{stamp}.pdf");
        }

        if (kind is not "csv")
            throw new ArgumentException("Export format must be csv or pdf.");

        return (
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(FiskalyErrorCsvBuilder.Build(stats, list.Items))).ToArray(),
            "text/csv; charset=utf-8",
            $"fiskaly-errors_{stamp}.csv");
    }

    public async Task<FiskalyErrorListItemDto> SetReviewStatusAsync(
        Guid id,
        string reviewStatus,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!FiskalyErrorReviewStatuses.IsKnown(reviewStatus))
            throw new ArgumentException("Review status must be open, resolved, or known_issue.");

        var normalized = FiskalyErrorReviewStatuses.Normalize(reviewStatus);
        IQueryable<FiskalyOperationHistory> query = _db.FiskalyOperationHistories;
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();

        var row = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException("Fiskaly error entry not found.");
        if (!string.Equals(row.Status, FiskalyOperationHistoryStatuses.Failed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only failed operations can be reviewed.");

        var previous = row.ErrorReviewStatus;
        row.ErrorReviewStatus = normalized;
        row.ErrorReviewedAtUtc = DateTime.UtcNow;
        row.ErrorReviewedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? "unknown" : actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.RemoveByPrefixAsync(CacheKeys.FiskalyErrorStatsPrefix, cancellationToken).ConfigureAwait(false);

        try
        {
            await _auditLog
                .LogSystemOperationAsync(
                    action: AuditLogActions.FISKALY_ERROR_REVIEWED,
                    entityType: "FiskalyOperationHistory",
                    userId: row.ErrorReviewedByUserId,
                    userRole: actorIsSuperAdmin ? Roles.SuperAdmin : Roles.Manager,
                    description: $"Fiskaly error review updated to {normalized}.",
                    status: AuditLogStatus.Success,
                    actionType: AuditEventType.FiskalyErrorReviewUpdated,
                    tenantId: row.TenantId,
                    entityId: row.Id,
                    oldValues: new { ReviewStatus = previous },
                    newValues: new { ReviewStatus = normalized })
                .ConfigureAwait(false);
        }
        catch
        {
            // Review persist must not fail if audit write is unavailable.
        }

        return ToItem(new ErrorRow(
            row.Id,
            row.CreatedAtUtc,
            row.CompletedAtUtc,
            row.OperationType,
            row.Status,
            row.ErrorCode,
            row.ErrorMessage,
            row.TenantId,
            row.TenantName,
            row.UserId,
            row.UserDisplayName,
            row.CashRegisterId,
            row.ReceiptNumber,
            row.CashRegisterName,
            row.ErrorReviewStatus,
            row.ErrorReviewedAtUtc,
            row.ErrorReviewedByUserId));
    }

    internal static FiskalyErrorStatsDto Build(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? tenantId,
        int totalOperations,
        IReadOnlyList<ErrorRow> failed)
    {
        var topErrors = failed
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ErrorCode) ? "unknown" : r.ErrorCode.Trim())
            .Select(g => new FiskalyErrorCountDto
            {
                Key = g.Key,
                Count = g.Count(),
                SampleMessage = g.Select(x => x.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(TopErrorLimit)
            .ToList();

        var byTenant = failed
            .GroupBy(r => r.TenantId)
            .Select(g => new FiskalyErrorTenantCountDto
            {
                TenantId = g.Key,
                TenantName = g.Select(x => x.TenantName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.TenantName ?? x.TenantId.ToString("D"), StringComparer.Ordinal)
            .ToList();

        var topTenant = byTenant.FirstOrDefault();
        var mostCommon = topErrors.FirstOrDefault();
        var totalCount = failed.Count;

        return new FiskalyErrorStatsDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TenantId = tenantId is { } id && id != Guid.Empty ? id : null,
            Kpis = new FiskalyErrorKpisDto
            {
                TotalErrors = totalCount,
                TotalOperations = totalOperations,
                ErrorRatePercent = totalOperations == 0 ? 0 : Math.Round(100d * totalCount / totalOperations, 2),
                MostCommonErrorCode = mostCommon?.Key,
                MostCommonErrorCount = mostCommon?.Count ?? 0,
                TenantWithMostErrorsId = topTenant?.TenantId,
                TenantWithMostErrorsName = topTenant?.TenantName,
                TenantWithMostErrorsCount = topTenant?.Count ?? 0,
                OpenCount = failed.Count(r => string.Equals(r.ReviewStatus, FiskalyErrorReviewStatuses.Open, StringComparison.OrdinalIgnoreCase)),
                ResolvedCount = failed.Count(r => string.Equals(r.ReviewStatus, FiskalyErrorReviewStatuses.Resolved, StringComparison.OrdinalIgnoreCase)),
                KnownIssueCount = failed.Count(r => string.Equals(r.ReviewStatus, FiskalyErrorReviewStatuses.KnownIssue, StringComparison.OrdinalIgnoreCase))
            },
            Daily = FillDaily(fromUtc, toUtc, failed),
            Weekly = FillWeekly(fromUtc, toUtc, failed),
            TopErrors = topErrors,
            ByOperationType = failed
                .GroupBy(r => string.IsNullOrWhiteSpace(r.OperationType) ? "unknown" : r.OperationType.Trim().ToLowerInvariant())
                .Select(g => new FiskalyErrorCountDto { Key = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .ToList(),
            ByTenant = byTenant
        };
    }

    private async Task<FiskalyErrorStatsDto> LoadStatsAsync(
        FiskalyErrorQuery query,
        bool actorIsSuperAdmin,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var scoped = ApplyScopeFilters(BaseQuery(actorIsSuperAdmin), query, actorIsSuperAdmin, fromUtc, toUtc);
        var totalOperations = await scoped.CountAsync(cancellationToken).ConfigureAwait(false);
        var failedQuery = scoped.Where(x => x.Status == FiskalyOperationHistoryStatuses.Failed);
        var failed = await failedQuery
            .Select(x => new ErrorRow(
                x.Id,
                x.CreatedAtUtc,
                x.CompletedAtUtc,
                x.OperationType,
                x.Status,
                x.ErrorCode,
                x.ErrorMessage,
                x.TenantId,
                x.TenantName,
                x.UserId,
                x.UserDisplayName,
                x.CashRegisterId,
                x.ReceiptNumber,
                x.CashRegisterName,
                x.ErrorReviewStatus,
                x.ErrorReviewedAtUtc,
                x.ErrorReviewedByUserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Build(
            fromUtc,
            toUtc,
            actorIsSuperAdmin ? query.TenantId : _tenantAccessor.TenantId,
            totalOperations,
            failed);
    }

    private IQueryable<FiskalyOperationHistory> BaseQuery(bool actorIsSuperAdmin)
    {
        IQueryable<FiskalyOperationHistory> query = _db.FiskalyOperationHistories.AsNoTracking();
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();
        return query;
    }

    private static IQueryable<FiskalyOperationHistory> ApplyScopeFilters(
        IQueryable<FiskalyOperationHistory> query,
        FiskalyErrorQuery filter,
        bool actorIsSuperAdmin,
        DateTime fromUtc,
        DateTime toUtc)
    {
        query = query.Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc);
        if (actorIsSuperAdmin && filter.TenantId is { } tenantFilter && tenantFilter != Guid.Empty)
            query = query.Where(x => x.TenantId == tenantFilter);
        if (!string.IsNullOrWhiteSpace(filter.OperationType) && FiskalyOperationTypes.IsKnown(filter.OperationType))
        {
            var op = filter.OperationType.Trim().ToLowerInvariant();
            query = query.Where(x => x.OperationType == op);
        }

        return query;
    }

    private static IQueryable<FiskalyOperationHistory> ApplyListFilters(
        IQueryable<FiskalyOperationHistory> query,
        FiskalyErrorQuery filter)
    {
        query = query.Where(x => x.Status == FiskalyOperationHistoryStatuses.Failed);
        if (!string.IsNullOrWhiteSpace(filter.ReviewStatus) && FiskalyErrorReviewStatuses.IsKnown(filter.ReviewStatus))
        {
            var review = FiskalyErrorReviewStatuses.Normalize(filter.ReviewStatus);
            query = query.Where(x => x.ErrorReviewStatus == review);
        }

        if (!string.IsNullOrWhiteSpace(filter.ErrorCode))
        {
            var code = filter.ErrorCode.Trim();
            query = query.Where(x => x.ErrorCode == code);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var needle = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                (x.ErrorMessage != null && x.ErrorMessage.ToLower().Contains(needle))
                || (x.ReceiptNumber != null && x.ReceiptNumber.ToLower().Contains(needle)));
        }

        return query;
    }

    private static List<FiskalyErrorDailyPointDto> FillDaily(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<ErrorRow> rows)
    {
        var grouped = rows
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAtUtc))
            .ToDictionary(g => g.Key, g => g.Count());
        var list = new List<FiskalyErrorDailyPointDto>();
        for (var day = DateOnly.FromDateTime(fromUtc); day <= DateOnly.FromDateTime(toUtc); day = day.AddDays(1))
        {
            grouped.TryGetValue(day, out var count);
            list.Add(new FiskalyErrorDailyPointDto
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Count = count
            });
        }

        return list;
    }

    private static List<FiskalyErrorWeeklyPointDto> FillWeekly(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<ErrorRow> rows)
    {
        static DateOnly IsoWeekMonday(DateTime utc)
        {
            var year = ISOWeek.GetYear(utc);
            var week = ISOWeek.GetWeekOfYear(utc);
            return DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
        }

        var grouped = rows
            .GroupBy(r => IsoWeekMonday(r.CreatedAtUtc))
            .ToDictionary(g => g.Key, g => g.Count());
        var list = new List<FiskalyErrorWeeklyPointDto>();
        var start = IsoWeekMonday(fromUtc);
        var end = IsoWeekMonday(toUtc);
        for (var weekStart = start; weekStart <= end; weekStart = weekStart.AddDays(7))
        {
            grouped.TryGetValue(weekStart, out var count);
            var weekDate = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var isoYear = ISOWeek.GetYear(weekDate);
            var isoWeek = ISOWeek.GetWeekOfYear(weekDate);
            list.Add(new FiskalyErrorWeeklyPointDto
            {
                Week = string.Create(CultureInfo.InvariantCulture, $"{isoYear}-W{isoWeek:D2}"),
                WeekStart = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Count = count
            });
        }

        return list;
    }

    private static FiskalyErrorListItemDto ToItem(ErrorRow row) =>
        new()
        {
            Id = row.Id,
            CreatedAtUtc = row.CreatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            OperationType = row.OperationType,
            Status = string.IsNullOrWhiteSpace(row.Status) ? FiskalyOperationHistoryStatuses.Failed : row.Status,
            ErrorCode = row.ErrorCode,
            ErrorMessage = row.ErrorMessage,
            TenantId = row.TenantId,
            TenantName = row.TenantName,
            UserId = row.UserId,
            UserDisplayName = row.UserDisplayName,
            CashRegisterId = row.CashRegisterId,
            ReceiptNumber = row.ReceiptNumber,
            CashRegisterName = row.CashRegisterName,
            ReviewStatus = string.IsNullOrWhiteSpace(row.ReviewStatus)
                ? FiskalyErrorReviewStatuses.Open
                : row.ReviewStatus,
            ReviewedAtUtc = row.ReviewedAtUtc,
            ReviewedByUserId = row.ReviewedByUserId,
            KnownSolution = FiskalyKnownErrorSolutions.Resolve(row.ErrorCode)
        };

    private static FiskalyErrorDetailDto ToDetail(FiskalyOperationHistory row) =>
        new()
        {
            Id = row.Id,
            CreatedAtUtc = row.CreatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            OperationType = row.OperationType,
            Status = row.Status,
            ErrorCode = row.ErrorCode,
            ErrorMessage = row.ErrorMessage,
            TenantId = row.TenantId,
            TenantName = row.TenantName,
            UserId = row.UserId,
            UserDisplayName = row.UserDisplayName,
            CashRegisterId = row.CashRegisterId,
            ReceiptNumber = row.ReceiptNumber,
            CashRegisterName = row.CashRegisterName,
            ReviewStatus = string.IsNullOrWhiteSpace(row.ErrorReviewStatus)
                ? FiskalyErrorReviewStatuses.Open
                : row.ErrorReviewStatus,
            ReviewedAtUtc = row.ErrorReviewedAtUtc,
            ReviewedByUserId = row.ErrorReviewedByUserId,
            KnownSolution = FiskalyKnownErrorSolutions.Resolve(row.ErrorCode),
            ReceiptId = row.ReceiptId,
            RequestPayloadJson = FiskalyPayloadSanitizer.SanitizeJson(row.RequestPayloadJson),
            ResponsePayloadJson = FiskalyPayloadSanitizer.SanitizeJson(row.ResponsePayloadJson),
            StackTrace = FiskalyPayloadSanitizer.ExtractStackTrace(row.ResponsePayloadJson, row.ErrorMessage)
        };

    internal sealed record ErrorRow(
        Guid Id,
        DateTime CreatedAtUtc,
        DateTime? CompletedAtUtc,
        string OperationType,
        string Status,
        string? ErrorCode,
        string? ErrorMessage,
        Guid TenantId,
        string? TenantName,
        string UserId,
        string? UserDisplayName,
        Guid CashRegisterId,
        string? ReceiptNumber,
        string? CashRegisterName,
        string ReviewStatus,
        DateTime? ReviewedAtUtc,
        string? ReviewedByUserId);
}

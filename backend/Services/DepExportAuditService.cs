using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDepExportAuditService
{
    Task<DepExportAuditEntry> LogExportActionAsync(
        DepExportAuditEntry entry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepExportAuditEntryDto>> GetAuditTrailAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        string? action = null,
        string? userSearch = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<DepExportAuditReportDto> GenerateAuditReportAsync(
        Guid tenantId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Append-only DEP export lifecycle trail (<c>dep_export_audit_entries</c>) with fiscal
/// <see cref="IAuditLogService"/> mirror for Created/Downloaded/Archived/Deleted.
/// </summary>
public sealed class DepExportAuditService : IDepExportAuditService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _time;
    private readonly ILogger<DepExportAuditService> _logger;

    public DepExportAuditService(
        AppDbContext db,
        IAuditLogService auditLog,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider time,
        ILogger<DepExportAuditService> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _httpContextAccessor = httpContextAccessor;
        _time = time;
        _logger = logger;
    }

    public async Task<DepExportAuditEntry> LogExportActionAsync(
        DepExportAuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Id == Guid.Empty)
            entry.Id = Guid.NewGuid();

        if (entry.ActionAt == default)
            entry.ActionAt = _time.GetUtcNow().UtcDateTime;

        entry.Action = NormalizeAction(entry.Action);
        entry.ExportName = string.IsNullOrWhiteSpace(entry.ExportName)
            ? "DEP Export"
            : entry.ExportName.Trim();

        EnrichFromHttpContext(entry);

        _db.DepExportAuditEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await MirrorToFiscalAuditLogAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to mirror DEP audit entry {EntryId} to fiscal AuditLog",
                entry.Id);
        }

        return entry;
    }

    public async Task<IReadOnlyList<DepExportAuditEntryDto>> GetAuditTrailAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        string? action = null,
        string? userSearch = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        fromUtc = EnsureUtc(fromUtc);
        toUtc = EnsureUtc(toUtc);
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        limit = Math.Clamp(limit, 1, 500);

        var query = _db.DepExportAuditEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                e.ActionAt >= fromUtc &&
                e.ActionAt <= toUtc);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalized = NormalizeAction(action);
            query = query.Where(e => e.Action == normalized);
        }

        if (!string.IsNullOrWhiteSpace(userSearch))
        {
            var term = userSearch.Trim().ToLowerInvariant();
            query = query.Where(e =>
                (e.UserEmail != null && e.UserEmail.ToLower().Contains(term)) ||
                (e.UserId != null && e.UserId.ToLower().Contains(term)) ||
                (e.UserRole != null && e.UserRole.ToLower().Contains(term)));
        }

        var rows = await query
            .OrderByDescending(e => e.ActionAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDto).ToList();
    }

    public async Task<DepExportAuditReportDto> GenerateAuditReportAsync(
        Guid tenantId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var to = EnsureUtc(toUtc ?? _time.GetUtcNow().UtcDateTime);
        var from = EnsureUtc(fromUtc ?? to.AddMonths(-12));
        if (to < from)
            (from, to) = (to, from);

        var rows = await _db.DepExportAuditEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                e.ActionAt >= from &&
                e.ActionAt <= to)
            .OrderByDescending(e => e.ActionAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var counts = rows
            .GroupBy(r => r.Action)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var last = rows.FirstOrDefault();

        return new DepExportAuditReportDto
        {
            TenantId = tenantId,
            GeneratedAtUtc = _time.GetUtcNow().UtcDateTime,
            FromUtc = from,
            ToUtc = to,
            TotalEntries = rows.Count,
            CountsByAction = counts,
            LastActionAt = last?.ActionAt,
            LastAction = last?.Action,
            LastExportName = last?.ExportName,
            RecentEntries = rows.Take(20).Select(ToDto).ToList(),
        };
    }

    private void EnrichFromHttpContext(DepExportAuditEntry entry)
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null)
            return;

        if (string.IsNullOrWhiteSpace(entry.IpAddress))
        {
            var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            entry.IpAddress = !string.IsNullOrWhiteSpace(forwarded)
                ? forwarded.Split(',')[0].Trim()
                : http.Connection.RemoteIpAddress?.ToString();
        }

        if (string.IsNullOrWhiteSpace(entry.UserAgent))
        {
            var ua = http.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrWhiteSpace(ua))
                entry.UserAgent = ua.Length <= 500 ? ua : ua[..500];
        }
    }

    private async Task MirrorToFiscalAuditLogAsync(
        DepExportAuditEntry entry,
        CancellationToken cancellationToken)
    {
        var (actionString, eventType) = MapFiscalAudit(entry.Action);
        if (actionString is null)
            return;

        await _auditLog.LogSystemOperationAsync(
                actionString,
                AuditLogEntityTypes.FISCAL_EXPORT,
                entry.UserId ?? "system",
                entry.UserRole ?? "System",
                description: $"{entry.Action}: {entry.ExportName}",
                notes: entry.Details,
                requestData: new
                {
                    entry.Action,
                    entry.ExportName,
                    entry.ExportHistoryId,
                    entry.UserEmail,
                    entry.IpAddress,
                },
                actionType: eventType,
                entityId: entry.ExportHistoryId,
                tenantId: entry.TenantId,
                entityName: entry.ExportName)
            .ConfigureAwait(false);
    }

    private static (string? Action, AuditEventType? Type) MapFiscalAudit(string action) =>
        action switch
        {
            DepExportAuditActions.Created => ("RksvDepExportCreated", AuditEventType.RksvDepExportCreated),
            DepExportAuditActions.Downloaded => ("RksvDepExportDownloaded", AuditEventType.RksvDepExportDownloaded),
            DepExportAuditActions.Archived => ("RksvDepExportArchived", AuditEventType.RksvDepExportArchived),
            DepExportAuditActions.Deleted => ("RksvDepExportPurged", AuditEventType.RksvDepExportPurged),
            DepExportAuditActions.Validated => ("RksvDepExportValidated", AuditEventType.RksvDepExportValidated),
            DepExportAuditActions.Failed => ("RksvDepExportFailed", AuditEventType.RksvDepExportFailed),
            _ => (null, null),
        };

    internal static string NormalizeAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return DepExportAuditActions.Created;

        var trimmed = action.Trim();
        return trimmed switch
        {
            "Created" or "created" or "CREATE" => DepExportAuditActions.Created,
            "Downloaded" or "downloaded" or "DOWNLOAD" => DepExportAuditActions.Downloaded,
            "Archived" or "archived" or "ARCHIVE" => DepExportAuditActions.Archived,
            "Deleted" or "deleted" or "DELETE" or "Purged" or "purged" => DepExportAuditActions.Deleted,
            "Validated" or "validated" or "VALIDATE" => DepExportAuditActions.Validated,
            "Failed" or "failed" or "FAIL" => DepExportAuditActions.Failed,
            _ => trimmed.Length <= 32 ? trimmed : trimmed[..32],
        };
    }

    private static DepExportAuditEntryDto ToDto(DepExportAuditEntry e) =>
        new()
        {
            Id = e.Id,
            TenantId = e.TenantId,
            Action = e.Action,
            ExportName = e.ExportName,
            ExportHistoryId = e.ExportHistoryId,
            UserEmail = e.UserEmail,
            UserId = e.UserId,
            UserRole = e.UserRole,
            ActionAt = e.ActionAt,
            IpAddress = e.IpAddress,
            UserAgent = e.UserAgent,
            Details = e.Details,
        };

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}

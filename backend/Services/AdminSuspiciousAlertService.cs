using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IAdminSuspiciousAlertService
{
    Task<SuspiciousAlertsListResponseDto> ListAsync(
        Guid tenantId,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        Guid tenantId,
        Guid alertId,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class AdminSuspiciousAlertService : IAdminSuspiciousAlertService
{
    private readonly AppDbContext _db;

    public AdminSuspiciousAlertService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SuspiciousAlertsListResponseDto> ListAsync(
        Guid tenantId,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SuspiciousTransactionAlerts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive);

        if (unreadOnly)
            query = query.Where(a => a.Status == SuspiciousAlertStatus.Open);

        var entities = await query
            .OrderByDescending(a => a.DetectedAtUtc)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        if (unreadOnly)
        {
            entities = entities
                .GroupBy(a => a.DedupKey, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderByDescending(a => a.DetectedAtUtc)
                .ThenByDescending(a => a.CreatedAt)
                .ToList();
        }

        var items = entities.Select(Map).ToList();
        return new SuspiciousAlertsListResponseDto
        {
            Items = items,
            Total = items.Count,
        };
    }

    public async Task<bool> MarkAsReadAsync(
        Guid tenantId,
        Guid alertId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _db.SuspiciousTransactionAlerts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.Id == alertId && a.TenantId == tenantId && a.IsActive,
                cancellationToken);

        if (alert == null)
            return false;

        if (alert.Status == SuspiciousAlertStatus.Open)
        {
            alert.Status = SuspiciousAlertStatus.Acknowledged;
            alert.UpdatedAt = DateTime.UtcNow;
            alert.UpdatedBy = actorUserId;

            // Legacy rows: acknowledge all open duplicates sharing the same dedup key.
            var siblings = await _db.SuspiciousTransactionAlerts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId
                    && a.IsActive
                    && a.Status == SuspiciousAlertStatus.Open
                    && a.DedupKey == alert.DedupKey
                    && a.Id != alert.Id)
                .ToListAsync(cancellationToken);

            foreach (var sibling in siblings)
            {
                sibling.Status = SuspiciousAlertStatus.Acknowledged;
                sibling.UpdatedAt = alert.UpdatedAt;
                sibling.UpdatedBy = actorUserId;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    internal static SuspiciousAlertDto Map(SuspiciousTransactionAlert entity)
    {
        var isRead = entity.Status != SuspiciousAlertStatus.Open;
        return new SuspiciousAlertDto
        {
            Id = entity.Id,
            Type = entity.AlertType,
            Severity = entity.Severity,
            Status = entity.Status,
            PaymentId = entity.PaymentId,
            CustomerId = entity.CustomerId,
            UserId = entity.UserId,
            Message = entity.Message,
            SuggestedAction = entity.SuggestedAction,
            Details = ParseDetails(entity.DetailsJson),
            IsRead = isRead,
            ReadAtUtc = isRead ? entity.UpdatedAt : null,
            DetectedAtUtc = entity.DetectedAtUtc,
            CreatedAtUtc = entity.CreatedAt,
        };
    }

    private static Dictionary<string, object>? ParseDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(detailsJson);
        }
        catch
        {
            return null;
        }
    }
}

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
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive);

        if (unreadOnly)
            query = query.Where(a => a.Status == SuspiciousAlertStatus.Open);

        var entities = await query
            .OrderByDescending(a => a.DetectedAtUtc)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

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

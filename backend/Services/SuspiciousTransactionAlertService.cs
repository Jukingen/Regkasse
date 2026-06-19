using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed record SuspiciousAlertDraft(
    Guid TenantId,
    SuspiciousAlertType Type,
    SuspiciousAlertSeverity Severity,
    string Message,
    string SuggestedAction,
    string DedupKey,
    Guid? PaymentId = null,
    Guid? CustomerId = null,
    string? UserId = null,
    object? Details = null);

public interface ISuspiciousTransactionAlertService
{
    Task TryPublishAlertAsync(SuspiciousAlertDraft draft, CancellationToken cancellationToken = default);
}

public sealed class SuspiciousTransactionAlertService : ISuspiciousTransactionAlertService
{
    private readonly AppDbContext _db;
    private readonly IActivityEventService _activity;
    private readonly IOptionsMonitor<SuspiciousTransactionDetectionOptions> _options;
    private readonly ILogger<SuspiciousTransactionAlertService> _logger;

    public SuspiciousTransactionAlertService(
        AppDbContext db,
        IActivityEventService activity,
        IOptionsMonitor<SuspiciousTransactionDetectionOptions> options,
        ILogger<SuspiciousTransactionAlertService> logger)
    {
        _db = db;
        _activity = activity;
        _options = options;
        _logger = logger;
    }

    public async Task TryPublishAlertAsync(SuspiciousAlertDraft draft, CancellationToken cancellationToken = default)
    {
        var hasOpenDuplicate = await _db.SuspiciousTransactionAlerts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                a => a.TenantId == draft.TenantId
                     && a.DedupKey == draft.DedupKey
                     && a.Status == SuspiciousAlertStatus.Open,
                cancellationToken);

        if (hasOpenDuplicate)
            return;

        var dedupHours = Math.Max(1, _options.CurrentValue.DedupWindowHours);
        var dedupSince = DateTime.UtcNow.AddHours(-dedupHours);
        var existsRecent = await _db.SuspiciousTransactionAlerts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                a => a.TenantId == draft.TenantId
                     && a.DedupKey == draft.DedupKey
                     && a.DetectedAtUtc >= dedupSince,
                cancellationToken);

        if (existsRecent)
            return;

        var detailsJson = draft.Details == null ? null : JsonSerializer.Serialize(draft.Details);

        var entity = new SuspiciousTransactionAlert
        {
            TenantId = draft.TenantId,
            AlertType = draft.Type,
            Severity = draft.Severity,
            Status = SuspiciousAlertStatus.Open,
            PaymentId = draft.PaymentId,
            CustomerId = draft.CustomerId,
            UserId = draft.UserId,
            Message = draft.Message,
            SuggestedAction = draft.SuggestedAction,
            DetailsJson = detailsJson,
            DedupKey = draft.DedupKey,
            DetectedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        _db.SuspiciousTransactionAlerts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _activity.PublishAsync(
            new ActivityEventPublishRequest(
                draft.TenantId,
                MapActivityType(draft.Type),
                draft.Message,
                Description: draft.SuggestedAction,
                Severity: MapActivitySeverity(draft.Severity),
                DedupKey: $"suspicious_{draft.DedupKey}",
                ActorUserId: draft.UserId,
                EntityType: draft.PaymentId.HasValue ? "payment" : draft.CustomerId.HasValue ? "customer" : "user",
                EntityId: draft.PaymentId?.ToString()
                          ?? draft.CustomerId?.ToString()
                          ?? draft.UserId,
                Metadata: ToMetadataDictionary(detailsJson)),
            cancellationToken);

        _logger.LogInformation(
            "Suspicious transaction alert published. TenantId={TenantId} Type={Type} DedupKey={DedupKey}",
            draft.TenantId,
            draft.Type,
            draft.DedupKey);
    }

    private static Dictionary<string, object>? ToMetadataDictionary(string? detailsJson)
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

    private static ActivityEventType MapActivityType(SuspiciousAlertType type) =>
        type switch
        {
            SuspiciousAlertType.HighValue => ActivityEventType.SuspiciousHighValuePayment,
            SuspiciousAlertType.MultipleStornos => ActivityEventType.SuspiciousMultipleStornos,
            SuspiciousAlertType.MultipleRefunds => ActivityEventType.SuspiciousMultipleRefunds,
            SuspiciousAlertType.UnusualTime => ActivityEventType.SuspiciousUnusualTime,
            SuspiciousAlertType.SameCardMultiple => ActivityEventType.SuspiciousSameCardMultiple,
            SuspiciousAlertType.RapidTransactions => ActivityEventType.SuspiciousRapidTransactions,
            _ => ActivityEventType.SuspiciousHighValuePayment,
        };

    private static string MapActivitySeverity(SuspiciousAlertSeverity severity) =>
        severity switch
        {
            SuspiciousAlertSeverity.Low => ActivitySeverityNames.Info,
            SuspiciousAlertSeverity.Medium => ActivitySeverityNames.Warning,
            SuspiciousAlertSeverity.High => ActivitySeverityNames.Error,
            SuspiciousAlertSeverity.Critical => ActivitySeverityNames.Critical,
            _ => ActivitySeverityNames.Warning,
        };
}

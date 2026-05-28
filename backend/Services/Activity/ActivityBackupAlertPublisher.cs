using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services.Activity;

/// <summary>Maps backup/restore drill alerts into the tenant activity feed.</summary>
public sealed class ActivityBackupAlertPublisher : IBackupAlertPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityBackupAlertPublisher> _logger;

    public ActivityBackupAlertPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityBackupAlertPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Publish(BackupAlertEvent evt) => _ = PublishInBackgroundAsync(evt);

    private static Dictionary<string, object> BuildMetadata(BackupAlertEvent evt)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (evt.BackupRunId.HasValue)
            metadata["BackupRunId"] = evt.BackupRunId.Value;
        if (evt.RestoreVerificationRunId.HasValue)
            metadata["RestoreVerificationRunId"] = evt.RestoreVerificationRunId.Value;
        if (!string.IsNullOrWhiteSpace(evt.Message))
            metadata["ErrorMessage"] = evt.Message;
        if (evt.Data != null)
        {
            foreach (var pair in evt.Data)
                metadata[pair.Key] = pair.Value;
        }

        return metadata;
    }

    private async Task PublishInBackgroundAsync(BackupAlertEvent evt)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
            var activity = scope.ServiceProvider.GetRequiredService<IActivityEventService>();

            var tenantId = tenantAccessor.TenantId ?? LegacyDefaultTenantIds.Primary;
            var (type, dedup) = MapKind(evt.Kind);
            var metadata = BuildMetadata(evt);

            var request = ActivityEventPublishBuilder.FromMetadata(
                tenantId,
                type,
                metadata,
                dedupKey: dedup);

            request = request with
            {
                Description = string.IsNullOrWhiteSpace(evt.Message) ? request.Description : evt.Message,
                EntityType = evt.BackupRunId.HasValue ? "backup_run" : "restore_verification_run",
                EntityId = (evt.BackupRunId ?? evt.RestoreVerificationRunId)?.ToString(),
            };

            await activity.PublishAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity feed publish failed for backup alert {Kind}", evt.Kind);
        }
    }

    private static (ActivityEventType Type, string? DedupKey) MapKind(BackupAlertKind kind) =>
        kind switch
        {
            BackupAlertKind.RestoreVerificationFailed => (ActivityEventType.RestoreDrillFailed, "restore_drill_failed"),
            BackupAlertKind.RestoreDrillOperationalRisk => (ActivityEventType.RestoreDrillFailed, "restore_drill_risk"),
            _ => (ActivityEventType.BackupFailed, "backup_failed"),
        };
}

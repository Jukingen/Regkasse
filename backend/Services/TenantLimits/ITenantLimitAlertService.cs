namespace KasseAPI_Final.Services.Limits;

/// <summary>Publishes activity-feed alerts when tenant limits approach or are exceeded.</summary>
public interface ITenantLimitAlertService
{
    Task EvaluateAndPublishAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task PublishExceededAsync(
        Guid tenantId,
        LimitExceededException exception,
        CancellationToken cancellationToken = default);
}

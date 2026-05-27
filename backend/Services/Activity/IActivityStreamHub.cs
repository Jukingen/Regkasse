namespace KasseAPI_Final.Services.Activity;

public interface IActivityStreamHub
{
    void Publish(Guid tenantId, object activityPayload);

    IAsyncEnumerable<ActivityStreamMessage> SubscribeAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

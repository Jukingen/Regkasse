using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Outbound TSE integration webhooks (tenant-scoped registrations + delivery log).
/// Diagnostic/integration only — does not alter fiscal signing.
/// </summary>
public interface ITseWebhookService
{
    Task<TseWebhookRegistrationDto> RegisterWebhookAsync(
        RegisterTseWebhookRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseWebhookRegistrationDto>> ListWebhooksAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task DeleteWebhookAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default);

    Task<TseWebhookDeliveryResultDto> TriggerWebhookAsync(
        Guid webhookId,
        TseWebhookEventDto @event,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseWebhookDeliveryLogDto>> GetWebhookEventsAsync(
        Guid webhookId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<TseWebhookDeliveryResultDto> TestWebhookAsync(
        Guid webhookId,
        CancellationToken cancellationToken = default);
}

using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

public sealed class OnlineOrderStatusUpdateResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public OnlineOrder? Order { get; init; }

    public static OnlineOrderStatusUpdateResult Success(OnlineOrder order) =>
        new() { Succeeded = true, Order = order };

    public static OnlineOrderStatusUpdateResult Fail(string code, string error) =>
        new() { Succeeded = false, Code = code, Error = error };
}

public interface IOnlineOrderNotificationService
{
    /// <summary>
    /// Customer confirmation (email + optional push) and staff/POS activity alert.
    /// Failures are logged and never throw into the order flow.
    /// </summary>
    Task SendOrderConfirmationAsync(
        OnlineOrder order,
        string? actorUserId = null,
        CancellationToken ct = default);

    Task NotifyPaidAsync(OnlineOrder order, CancellationToken ct = default);

    Task NotifyStatusChangedAsync(
        OnlineOrder order,
        string previousStatus,
        CancellationToken ct = default);
}

public interface IOnlineOrderStatusService
{
    Task<OnlineOrderStatusUpdateResult> UpdateStatusAsync(
        Guid onlineOrderId,
        string newStatus,
        string? actorUserId = null,
        CancellationToken ct = default);
}

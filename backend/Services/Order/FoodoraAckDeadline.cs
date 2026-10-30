using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Local accept window. Starts at <c>received_at</c>. No Foodora accept call exists for this window.
/// </summary>
public static class FoodoraAckDeadline
{
    public const string ErrorCode = "FOODORA_ACK_DEADLINE";

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public static DateTime DeadlineUtc(DateTime receivedAtUtc) => receivedAtUtc.Add(Window);

    public static bool ShouldFlag(DateTime receivedAtUtc, string? kitchenStatus, DateTime utcNow)
    {
        var pending = string.Equals(
            (kitchenStatus ?? string.Empty).Trim(),
            OnlineOrderStatuses.Pending,
            StringComparison.OrdinalIgnoreCase);
        return pending && utcNow >= DeadlineUtc(receivedAtUtc);
    }
}

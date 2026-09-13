using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Preorder;

/// <summary>Allowed operational status moves. Fiscal cancel must go through existing storno.</summary>
public static class PreorderStatusTransitions
{
    public static bool CanTransition(string? from, string? to, bool isFiscalCancel)
    {
        var current = PreorderStatuses.Normalize(from);
        var next = PreorderStatuses.Normalize(to);
        if (string.Equals(current, next, StringComparison.Ordinal))
            return true;

        if (next == PreorderStatuses.Cancelled)
            return isFiscalCancel && current != PreorderStatuses.Cancelled;

        if (current is PreorderStatuses.Cancelled or PreorderStatuses.Collected)
            return false;

        if (next == PreorderStatuses.Ready)
            return current == PreorderStatuses.Pending;

        if (next == PreorderStatuses.Collected)
            return current is PreorderStatuses.Pending or PreorderStatuses.Ready;

        return false;
    }

    public static OrderStatus ToKitchenStatus(string preorderStatus) =>
        PreorderStatuses.Normalize(preorderStatus) switch
        {
            PreorderStatuses.Ready => OrderStatus.Ready,
            PreorderStatuses.Collected => OrderStatus.Completed,
            PreorderStatuses.Cancelled => OrderStatus.Cancelled,
            _ => OrderStatus.Pending
        };
}

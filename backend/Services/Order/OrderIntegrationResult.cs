using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

/// <summary>Outcome of <see cref="IOrderIntegrationService.PushOrderToPosAsync"/>.</summary>
public sealed class OrderIntegrationResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public OnlineOrder? OnlineOrder { get; init; }
    /// <summary>POS cart string key (<see cref="Cart.CartId"/>).</summary>
    public string? PosCartId { get; init; }
    public bool AlreadyPushed { get; init; }

    public static OrderIntegrationResult Success(OnlineOrder order, string posCartId, bool alreadyPushed = false) =>
        new()
        {
            Succeeded = true,
            OnlineOrder = order,
            PosCartId = posCartId,
            AlreadyPushed = alreadyPushed
        };

    public static OrderIntegrationResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}

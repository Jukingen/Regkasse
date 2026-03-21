using KasseAPI_Final.Constants;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public static class CustomerKindResolver
{
    public static CustomerKind ResolveFromCustomerId(Guid customerId, CustomerKind? requested)
    {
        if (requested.HasValue)
            return requested.Value;
        return customerId == WalkInCustomerConstants.GuestCustomerId
            ? CustomerKind.WalkIn
            : CustomerKind.Registered;
    }
}

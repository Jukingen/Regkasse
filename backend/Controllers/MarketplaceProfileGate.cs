using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Marketplace admin routes are gastronomy and gastronomy-tables only.
/// Calls <see cref="IVerticalProfileGuard.EnforceEndpointAsync"/>.
/// </summary>
internal static class MarketplaceProfileGate
{
    public static async Task<ActionResult?> RejectAsync(IVerticalProfileGuard guard, CancellationToken cancellationToken)
    {
        try
        {
            await guard.EnforceEndpointAsync(VerticalProfileGuard.MarketplaceOrdersFeature, cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (ProfileEndpointDisabledException ex)
        {
            return VerticalProfileGuardResponses.From(ex);
        }
    }
}

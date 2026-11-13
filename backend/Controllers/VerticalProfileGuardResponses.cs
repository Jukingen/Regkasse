using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

internal static class VerticalProfileGuardResponses
{
    public static ActionResult From(Exception exception) => exception switch
    {
        FeatureNotEnabledForProfileException feature => new BadRequestObjectResult(Body(feature.ErrorCode, feature.Feature, feature.Message)),
        ProfileEndpointDisabledException endpoint => new ObjectResult(Body(endpoint.ErrorCode, endpoint.Feature, endpoint.Message))
        {
            StatusCode = StatusCodes.Status403Forbidden,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(exception), exception, "Not a vertical-profile rejection."),
    };

    public static object Body(string code, string? feature, string message) =>
        new
        {
            code,
            feature,
            message,
        };
}

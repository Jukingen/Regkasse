using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Requires the authenticated user's JWT to carry an app_context claim matching the expected value.
/// Used on route groups to enforce POS-only or Admin-only access after login.
/// </summary>
public sealed class AppContextRequirement : IAuthorizationRequirement
{
    public string ExpectedAppContext { get; }

    public AppContextRequirement(string expectedAppContext)
    {
        ExpectedAppContext = expectedAppContext ?? throw new ArgumentNullException(nameof(expectedAppContext));
    }
}

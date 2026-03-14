using Microsoft.AspNetCore.Authorization;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Enforces that the caller's JWT contains an app_context claim matching the given value.
/// Maps to policy "AppContext:{value}" evaluated by <see cref="AppContextAuthorizationHandler"/>.
/// Example: [RequireAppContext("admin")] on admin-only controllers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireAppContextAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "AppContext:";

    public RequireAppContextAttribute(string appContext)
        : base(PolicyPrefix + appContext)
    {
        if (string.IsNullOrEmpty(appContext))
            throw new ArgumentNullException(nameof(appContext));
    }
}

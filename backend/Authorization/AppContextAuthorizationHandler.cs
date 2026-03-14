using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Evaluates <see cref="AppContextRequirement"/>: checks that the JWT app_context claim
/// matches the expected value (e.g. "pos" or "admin").
/// When <see cref="AuthOptions.AllowLegacyLoginWithoutClientApp"/> is true, tokens
/// without app_context are allowed through (legacy grace period).
/// </summary>
public sealed class AppContextAuthorizationHandler : AuthorizationHandler<AppContextRequirement>
{
    private readonly ILogger<AppContextAuthorizationHandler> _logger;
    private readonly AuthOptions _authOptions;

    public AppContextAuthorizationHandler(
        ILogger<AppContextAuthorizationHandler> logger,
        IOptions<AuthOptions> authOptions)
    {
        _logger = logger;
        _authOptions = authOptions.Value;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AppContextRequirement requirement)
    {
        var appContextClaim = context.User.FindFirst(ClientAppPolicy.AppContextClaimType)?.Value;

        if (string.IsNullOrEmpty(appContextClaim))
        {
            if (_authOptions.AllowLegacyLoginWithoutClientApp)
            {
                _logger.LogDebug(
                    "AppContext enforcement skipped: legacy token without app_context claim (expected {Expected})",
                    requirement.ExpectedAppContext);
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "AppContext denied: token has no app_context claim, expected {Expected}",
                requirement.ExpectedAppContext);
            return Task.CompletedTask;
        }

        if (string.Equals(appContextClaim, requirement.ExpectedAppContext, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "AppContext denied: token has app_context={Actual}, expected {Expected}",
                appContextClaim, requirement.ExpectedAppContext);
        }

        return Task.CompletedTask;
    }
}

using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Writes a structured 403 JSON payload and logs with requiredPolicy + missingRequirement + correlationId.
/// For role-based [Authorize(Roles="...")] endpoints, resolves required roles so requiredPolicy is not "Unknown".
/// In Development, optionally adds userRole to the response for easier diagnosis.
/// </summary>
public class ForbiddenResponseAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ILogger<ForbiddenResponseAuthorizationHandler> _logger;
    private readonly IWebHostEnvironment _env;

    public ForbiddenResponseAuthorizationHandler(
        ILogger<ForbiddenResponseAuthorizationHandler> logger,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
            var (requiredPolicyOrRole, requiredRolesList) = GetRequiredPolicyOrRolesFromEndpoint(context);
            var missingRequirement = requiredRolesList != null && requiredRolesList.Count > 0 ? "Role" : "Policy";

            var payload = new ApiError.ForbiddenPayload
            {
                RequiredPolicy = requiredPolicyOrRole ?? "Unknown",
                MissingRequirement = missingRequirement,
                CorrelationId = correlationId,
            };

            _logger.LogWarning(
                "403 Forbidden: correlationId={CorrelationId}, requiredPolicy={RequiredPolicy}, missingRequirement={MissingRequirement}, path={Path}, userId={UserId}",
                correlationId,
                payload.RequiredPolicy,
                payload.MissingRequirement,
                context.Request.Path,
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            // Build response; add requiredRoles and (in Development) userRole for diagnostics
            var responseObj = new Dictionary<string, object?>
            {
                ["code"] = payload.CodeValue,
                ["reason"] = payload.ReasonValue,
                ["requiredPolicy"] = payload.RequiredPolicy,
                ["missingRequirement"] = payload.MissingRequirement,
                ["correlationId"] = payload.CorrelationId,
            };
            if (requiredRolesList != null && requiredRolesList.Count > 0)
                responseObj["requiredRoles"] = requiredRolesList;

            if (_env.IsDevelopment())
            {
                var userRole = context.User.FindFirst("role")?.Value;
                if (!string.IsNullOrEmpty(userRole))
                    responseObj["userRole"] = userRole;
            }

            var json = JsonSerializer.Serialize(responseObj);
            await context.Response.WriteAsync(json);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    /// <summary>
    /// Resolves policy name or role-based description from endpoint metadata.
    /// Role-based [Authorize(Roles="...")] has no Policy set, so we derive "Role:..." and the role list.
    /// </summary>
    private static (string? requiredPolicyOrRole, IReadOnlyList<string>? requiredRoles) GetRequiredPolicyOrRolesFromEndpoint(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
            return (null, null);

        var authorizeAttrs = endpoint.Metadata.OfType<AuthorizeAttribute>().ToList();
        if (authorizeAttrs.Count == 0)
            return (null, null);

        var withPolicy = authorizeAttrs.FirstOrDefault(a => !string.IsNullOrEmpty(a.Policy));
        if (!string.IsNullOrEmpty(withPolicy?.Policy))
            return (withPolicy.Policy, null);

        var roles = authorizeAttrs
            .SelectMany(a => (a.Roles ?? "").Split(',', StringSplitOptions.TrimEntries))
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct()
            .ToList();
        if (roles.Count > 0)
            return ("Role:" + string.Join(",", roles), roles);

        // [Authorize] with no Policy and no Roles – e.g. "authenticated only"
        return ("Authenticated", null);
    }
}

using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Writes a structured 403 JSON payload and logs with requiredPolicy + missingRequirement + correlationId.
/// Keeps JWT auth success and 403 logs correlated by correlationId. No secret leakage in logs.
/// </summary>
public class ForbiddenResponseAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly ILogger<ForbiddenResponseAuthorizationHandler> _logger;

    public ForbiddenResponseAuthorizationHandler(ILogger<ForbiddenResponseAuthorizationHandler> logger)
    {
        _logger = logger;
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
            var requiredPolicy = GetRequiredPolicyFromEndpoint(context);
            var missingRequirement = "Role";

            var payload = new ApiError.ForbiddenPayload
            {
                RequiredPolicy = requiredPolicy ?? "Unknown",
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
            var json = JsonSerializer.Serialize(new
            {
                code = payload.CodeValue,
                reason = payload.ReasonValue,
                requiredPolicy = payload.RequiredPolicy,
                missingRequirement = payload.MissingRequirement,
                correlationId = payload.CorrelationId,
            });
            await context.Response.WriteAsync(json);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static string? GetRequiredPolicyFromEndpoint(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null) return null;

        var authorizeAttrs = endpoint.Metadata.OfType<AuthorizeAttribute>();
        var withPolicy = authorizeAttrs.FirstOrDefault(a => !string.IsNullOrEmpty(a.Policy));
        return withPolicy?.Policy;
    }
}

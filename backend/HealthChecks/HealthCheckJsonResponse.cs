using System.Net;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Deployment;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.HealthChecks;

/// <summary>Shared JSON shape for MapHealthChecks / <see cref="Controllers.HealthController"/>.</summary>
public static class HealthCheckJsonResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static async Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        httpContext.Response.StatusCode = report.Status switch
        {
            HealthStatus.Healthy => (int)HttpStatusCode.OK,
            HealthStatus.Degraded => (int)HttpStatusCode.OK,
            _ => (int)HttpStatusCode.ServiceUnavailable,
        };

        // releaseStage on /health/ready: primarily for debugging and staging verification.
        string? releaseStage = null;
        if (httpContext.Request.Path.StartsWithSegments("/health/ready"))
        {
            var host = httpContext.RequestServices.GetService<IHostEnvironment>();
            var deployment = httpContext.RequestServices.GetService<IOptions<DeploymentOptions>>()?.Value;
            if (host != null)
                releaseStage = ReleaseStageResolver.Resolve(host, deployment);
        }

        var payload = HealthProbeResponseFactory.FromReport(report, releaseStage);
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions))
            .ConfigureAwait(false);
    }

    public static IResult ToMinimalResult(HealthReport report)
    {
        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => StatusCodes.Status200OK,
            HealthStatus.Degraded => StatusCodes.Status200OK,
            _ => StatusCodes.Status503ServiceUnavailable,
        };

        var payload = HealthProbeResponseFactory.FromReport(report);
        return Results.Json(payload, statusCode: statusCode);
    }
}

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>Shared JSON shape for MapHealthChecks / <see cref="Controllers.HealthController"/>.</summary>
public static class HealthCheckJsonResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checkedAtUtc = DateTime.UtcNow,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data,
                }),
        };

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

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checkedAtUtc = DateTime.UtcNow,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data,
                }),
        };

        return Results.Json(payload, statusCode: statusCode);
    }
}

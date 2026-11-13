using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Verifies that the Elmah PostgreSQL error table exists and is reachable.
/// </summary>
public sealed class ElmahHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptions<ElmahOptions> _elmahOptions;

    public ElmahHealthCheck(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IOptions<ElmahOptions> elmahOptions)
    {
        _configuration = configuration;
        _environment = environment;
        _elmahOptions = elmahOptions;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment())
        {
            return HealthCheckResult.Healthy("Elmah is disabled in Development.");
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("DefaultConnection is not configured.");
        }

        var applicationName = string.IsNullOrWhiteSpace(_elmahOptions.Value.ApplicationName)
            ? "Regkasse"
            : _elmahOptions.Value.ApplicationName.Trim();
        var maxLogEntries = _elmahOptions.Value.MaxLogEntries;

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'elmah_error'
                )
                """,
                connection);
            var exists = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (exists is not true)
            {
                return HealthCheckResult.Degraded(
                    "Elmah table elmah_error does not exist yet.",
                    data: new Dictionary<string, object>
                    {
                        ["applicationName"] = applicationName,
                        ["maxLogEntries"] = maxLogEntries,
                    });
            }

            await using var countCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM elmah_error WHERE application = @application",
                connection);
            countCommand.Parameters.AddWithValue("application", applicationName);
            var rowCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

            var data = new Dictionary<string, object>
            {
                ["applicationName"] = applicationName,
                ["rowCount"] = rowCount,
                ["maxLogEntries"] = maxLogEntries,
            };

            if (maxLogEntries > 0 && rowCount > maxLogEntries)
            {
                return HealthCheckResult.Degraded(
                    $"Elmah row count ({rowCount}) exceeds configured max ({maxLogEntries}); retention sweep pending.",
                    data: data);
            }

            return HealthCheckResult.Healthy("Elmah configured and accessible.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Elmah health check failed: {ex.Message}");
        }
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Verifies that the Elmah PostgreSQL error table exists and is reachable.
/// </summary>
public sealed class ElmahHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ElmahHealthCheck(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
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
                return HealthCheckResult.Degraded("Elmah table elmah_error does not exist yet.");
            }

            await using var countCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM elmah_error LIMIT 1",
                connection);
            _ = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy("Elmah configured and accessible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Elmah health check failed: {ex.Message}");
        }
    }
}

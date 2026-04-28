using System;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL for integration tests: prefers REGKASSE_TEST_POSTGRES, else Testcontainers (Docker).
/// When neither works, tests using this fixture should call Skip.IfNot(HasDatabase, SkipReason).
/// </summary>
public sealed class PostgreSqlReplayFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;
    private string? _skipReason;

    /// <summary>True when a connection string is available and migrations succeeded.</summary>
    public bool HasDatabase => _connectionString != null;

    /// <summary>Why the database is unavailable (Docker down, bad env connection, migrate failure).</summary>
    public string SkipReason =>
        _skipReason ?? "PostgreSQL not configured. Start Docker or set REGKASSE_TEST_POSTGRES.";

    public string ConnectionString =>
        _connectionString
        ?? throw new InvalidOperationException("PostgreSQL fixture is not available: " + SkipReason);

    public async Task InitializeAsync()
    {
        var envCs = Environment.GetEnvironmentVariable("REGKASSE_TEST_POSTGRES");
        if (!string.IsNullOrWhiteSpace(envCs))
        {
            try
            {
                await MigrateAsync(envCs.Trim());
                _connectionString = envCs.Trim();
            }
            catch (Exception ex)
            {
                _skipReason = $"REGKASSE_TEST_POSTGRES migrate failed: {ex.Message}";
            }

            return;
        }

        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .Build();
            await _container.StartAsync();
            var cs = _container.GetConnectionString();
            await MigrateAsync(cs);
            _connectionString = cs;
        }
        catch (Exception ex)
        {
            _skipReason =
                $"Docker/Testcontainers unavailable: {ex.Message}. Set REGKASSE_TEST_POSTGRES or start Docker.";
        }
    }

    private static bool ShouldResetIntegrationDatabase(string connectionString)
    {
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString);
            var db = (csb.Database ?? string.Empty).Trim();
            if (db.Equals("regkasse_pg_integration", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(Environment.GetEnvironmentVariable("REGKASSE_TEST_POSTGRES_RESET"), "1", StringComparison.Ordinal))
                return true;
        }
        catch
        {
            // If the connection string cannot be parsed, only run Migrate.
        }

        return false;
    }

    private static async Task AssertAspNetUsersDeactivatedAtPresentAsync(AppDbContext ctx)
    {
        await ctx.Database.OpenConnectionAsync().ConfigureAwait(false);
        try
        {
            await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
            cmd.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_attribute a
                    INNER JOIN pg_class c ON a.attrelid = c.oid
                    INNER JOIN pg_namespace n ON c.relnamespace = n.oid
                    WHERE n.nspname = 'public'
                      AND c.relname = 'AspNetUsers'
                      AND a.attname = 'deactivated_at'
                      AND a.attnum > 0
                      AND NOT a.attisdropped
                );
                """;
            var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (scalar is not true)
            {
                throw new InvalidOperationException(
                    "PostgreSQL integration schema is missing AspNetUsers.deactivated_at after Migrate. " +
                    "Drop the integration database, set REGKASSE_TEST_POSTGRES_RESET=1 for a one-shot reset, or point REGKASSE_TEST_POSTGRES at a fresh database.");
            }
        }
        finally
        {
            await ctx.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static async Task MigrateAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseAppNpgsql(connectionString)
            .Options;
        await using var ctx = new AppDbContext(options);

        if (ShouldResetIntegrationDatabase(connectionString))
            await ctx.Database.EnsureDeletedAsync().ConfigureAwait(false);

        await ctx.Database.MigrateAsync().ConfigureAwait(false);
        await AssertAspNetUsersDeactivatedAtPresentAsync(ctx).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}

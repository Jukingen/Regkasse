using KasseAPI_Final.Data;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            // CI and any explicit connection string opted in. A migrate failure must fail the
            // run. Swallowing it makes Category=PostgreSql report green while every test skips.
            try
            {
                await MigrateAsync(envCs.Trim());
                _connectionString = envCs.Trim();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "REGKASSE_TEST_POSTGRES migrate failed: " + ex.Message,
                    ex);
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
            var message =
                $"Docker/Testcontainers unavailable: {ex.Message}. Set REGKASSE_TEST_POSTGRES or start Docker.";
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(message, ex);
            }

            _skipReason = message;
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
            // information_schema is case-stable across PostgreSQL identifier folding (AspNetUsers vs aspnetusers).
            cmd.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND column_name = 'deactivated_at'
                      AND lower(table_name) = 'aspnetusers'
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
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));

        if (ShouldResetIntegrationDatabase(connectionString))
            await ctx.Database.EnsureDeletedAsync().ConfigureAwait(false);

        await SkipBackdatedProductMigrationsOnEmptyDatabaseAsync(connectionString).ConfigureAwait(false);

        await ctx.Database.MigrateAsync().ConfigureAwait(false);
        await AssertAspNetUsersDeactivatedAtPresentAsync(ctx).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>20250115000000</c> and <c>20250115000001</c> ALTER <c>products</c> but sort before
    /// <c>InitialCreate</c>, which is what creates the table. A fresh database fails there.
    /// Later migrations rename the PascalCase columns <c>InitialCreate</c> emits.
    /// Recording the two ids as applied is a no-op on databases that already ran them.
    /// </summary>
    private static async Task SkipBackdatedProductMigrationsOnEmptyDatabaseAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
            return;

        var maintenance = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using (var admin = new NpgsqlConnection(maintenance.ConnectionString))
        {
            await admin.OpenAsync().ConfigureAwait(false);
            await using var exists = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @name",
                admin);
            exists.Parameters.AddWithValue("name", database);
            if (await exists.ExecuteScalarAsync().ConfigureAwait(false) is null)
            {
                var quoted = database.Replace("\"", "\"\"", StringComparison.Ordinal);
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{quoted}\"", admin);
                await create.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using (var products = new NpgsqlCommand(
            "SELECT to_regclass('public.products') IS NOT NULL",
            conn))
        {
            if (await products.ExecuteScalarAsync().ConfigureAwait(false) is true)
                return;
        }

        await using var seed = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES
                ('20250115000000_UpdateProductTableForRKSV', '10.0.3'),
                ('20250115000001_UpdateProductTableNamingConvention', '10.0.3')
            ON CONFLICT ("MigrationId") DO NOTHING;
            """,
            conn);
        await seed.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}

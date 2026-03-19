using System;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
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

    private static async Task MigrateAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}

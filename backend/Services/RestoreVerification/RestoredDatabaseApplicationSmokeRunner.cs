using System.Data;
using System.Diagnostics;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Geri yüklenen izole PostgreSQL kopyasına karşı salt okunur duman: bağlantı, EF migrasyon tutarlılığı, çekirdek tablolar, basit okuma.
/// </summary>
public sealed class RestoredDatabaseApplicationSmokeRunner : IRestoredDatabaseApplicationSmokeRunner
{
    /// <summary>RKSV çekirdek okuma yolu — tablo adları PostgreSQL <c>public</c> şeması.</summary>
    private static readonly string[] CriticalPublicTables = ["invoices", "receipts", "products"];

    private readonly ILogger<RestoredDatabaseApplicationSmokeRunner> _logger;

    public RestoredDatabaseApplicationSmokeRunner(ILogger<RestoredDatabaseApplicationSmokeRunner> logger)
    {
        _logger = logger;
    }

    public async Task<RestoredDatabaseApplicationSmokeOutcome> RunAsync(string restoredDatabaseConnectionString, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        var checks = new List<RestoredDatabaseApplicationSmokeCheckRow>();
        try
        {
            if (string.IsNullOrWhiteSpace(restoredDatabaseConnectionString))
            {
                sw.Stop();
                return Finish(RestoreDrillApplicationSmokeResultKind.Failed, "empty_connection_string", checks, sw.ElapsedMilliseconds, startedAt);
            }

            if (!IsNpgsqlConnectionString(restoredDatabaseConnectionString))
            {
                sw.Stop();
                return Finish(RestoreDrillApplicationSmokeResultKind.NotSupported, "not_npgsql_connection_string", checks, sw.ElapsedMilliseconds, startedAt);
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(restoredDatabaseConnectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options;

            await using var ctx = new AppDbContext(options);

            var canConnect = await ctx.Database.CanConnectAsync(ct);
            checks.Add(Row("database_can_connect", canConnect, null));
            if (!canConnect)
            {
                sw.Stop();
                return Finish(RestoreDrillApplicationSmokeResultKind.Failed, "cannot_connect_to_restored_database", checks, sw.ElapsedMilliseconds, startedAt);
            }

            var applied = (await ctx.Database.GetAppliedMigrationsAsync(ct)).ToList();
            var pending = (await ctx.Database.GetPendingMigrationsAsync(ct)).ToList();

            checks.Add(Row("ef_applied_migrations_non_empty", applied.Count > 0, $"count={applied.Count}"));
            if (applied.Count == 0)
            {
                sw.Stop();
                return Finish(RestoreDrillApplicationSmokeResultKind.Failed, "no_ef_migrations_applied", checks, sw.ElapsedMilliseconds, startedAt);
            }

            var pendingDetail = pending.Count > 0 ? string.Join(",", pending.Take(8)) : null;
            checks.Add(Row("ef_pending_migrations_empty", pending.Count == 0, pendingDetail == null ? null : $"pending_first={pendingDetail}"));

            if (pending.Count > 0)
            {
                sw.Stop();
                return Finish(
                    RestoreDrillApplicationSmokeResultKind.Inconclusive,
                    "restored_schema_behind_running_code_pending_migrations",
                    checks,
                    sw.ElapsedMilliseconds,
                    startedAt);
            }

            await using var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            foreach (var table in CriticalPublicTables)
            {
                var exists = await TableExistsInPublicSchemaAsync(conn, table, ct);
                checks.Add(Row($"table_exists:{table}", exists, null));
                if (!exists)
                {
                    sw.Stop();
                    return Finish(RestoreDrillApplicationSmokeResultKind.Failed, $"missing_critical_table:{table}", checks, sw.ElapsedMilliseconds, startedAt);
                }
            }

            await using (var cmd = new NpgsqlCommand(@"SELECT 1 FROM ""invoices"" LIMIT 1", conn))
            {
                cmd.CommandTimeout = 60;
                await cmd.ExecuteScalarAsync(ct);
            }

            checks.Add(Row("read_path_invoices_limit_one", true, "ok"));

            sw.Stop();
            return Finish(RestoreDrillApplicationSmokeResultKind.Passed, "restored_db_application_smoke_ok", checks, sw.ElapsedMilliseconds, startedAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restored database application smoke failed");
            sw.Stop();
            checks.Add(Row("runner_exception", false, ex.Message));
            return Finish(RestoreDrillApplicationSmokeResultKind.Failed, ex.Message, checks, sw.ElapsedMilliseconds, startedAt);
        }
    }

    private static bool IsNpgsqlConnectionString(string cs) =>
        cs.Contains("Host=", StringComparison.OrdinalIgnoreCase)
        || cs.Contains("Server=", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> TableExistsInPublicSchemaAsync(NpgsqlConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT EXISTS (
              SELECT 1 FROM information_schema.tables
              WHERE table_schema = 'public' AND table_name = @name
            )
            """,
            conn);
        cmd.Parameters.AddWithValue("name", tableName);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is true;
    }

    private static RestoredDatabaseApplicationSmokeCheckRow Row(string id, bool passed, string? detail) =>
        new() { Id = id, Passed = passed, Detail = detail };

    private static RestoredDatabaseApplicationSmokeOutcome Finish(
        RestoreDrillApplicationSmokeResultKind kind,
        string? detail,
        List<RestoredDatabaseApplicationSmokeCheckRow> checks,
        long durationMs,
        DateTimeOffset startedAtUtc) =>
        new()
        {
            Kind = kind,
            Detail = detail,
            DurationMs = durationMs,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Checks = checks
        };
}

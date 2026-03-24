using System.Data.Common;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace KasseAPI_Final.Data;

/// <summary>
/// Last-mile safety net: Npgsql 6+ rejects <see cref="DateTime"/> parameters with <see cref="DateTimeKind.Local"/>
/// or <see cref="DateTimeKind.Unspecified"/> for many <c>timestamptz</c> paths. EF Core may still surface
/// <see cref="DateTimeKind.Unspecified"/> on the change tracker (especially the InMemory provider).
/// This interceptor aligns parameter values with <see cref="PostgreSqlUtcDateTime.InstantToPersistUtc"/> before commands execute.
/// <para>
/// Skips PostgreSQL <c>date</c> and <c>timestamp without time zone</c> (<see cref="NpgsqlDbType.Date"/>, <see cref="NpgsqlDbType.Timestamp"/>).
/// Calendar anchors such as <see cref="Models.DailyClosing.ClosingDate"/> must already carry the correct UTC instant from
/// <see cref="AppDbContext"/> SaveChanges normalization (or <see cref="PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc"/> at the source);
/// this layer only fixes kind / local-to-UTC for instants.
/// </para>
/// </summary>
public sealed class NpgsqlTimestamptzUtcParameterInterceptor : DbCommandInterceptor
{
    public static NpgsqlTimestamptzUtcParameterInterceptor Instance { get; } = new();

    private NpgsqlTimestamptzUtcParameterInterceptor()
    {
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Normalize(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Normalize(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Normalize(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Normalize(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Normalize(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Normalize(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void Normalize(DbCommand command)
    {
        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter is not NpgsqlParameter npgsql)
                continue;
            if (npgsql.Value is not DateTime dt)
                continue;

            var dbType = npgsql.NpgsqlDbType;
            if (dbType == NpgsqlDbType.Date)
                continue;
            if (dbType == NpgsqlDbType.Timestamp)
                continue;

            var normalized = PostgreSqlUtcDateTime.InstantToPersistUtc(dt);
            if (normalized != dt || normalized.Kind != dt.Kind)
                npgsql.Value = normalized;
        }
    }
}

/// <summary>
/// Registers PostgreSQL + shared interceptors for <see cref="AppDbContext"/>. Prefer this over raw <c>UseNpgsql</c> in app and tests.
/// </summary>
public static class AppDbContextNpgsqlExtensions
{
    public static DbContextOptionsBuilder<AppDbContext> UseAppNpgsql(
        this DbContextOptionsBuilder<AppDbContext> builder,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return builder
            .UseNpgsql(connectionString)
            .AddInterceptors(NpgsqlTimestamptzUtcParameterInterceptor.Instance);
    }
}

using System.Data.Common;
using KasseAPI_Final.Services.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KasseAPI_Final.Data;

/// <summary>
/// Records database query metrics via <see cref="IDbMetricsService"/> for EF Core relational commands.
/// </summary>
public sealed class DbQueryDurationInterceptor : DbCommandInterceptor
{
    private readonly IDbMetricsService _metrics;

    public DbQueryDurationInterceptor(IDbMetricsService metrics)
    {
        _metrics = metrics;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return new ValueTask<DbDataReader>(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return new ValueTask<int>(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return new ValueTask<object?>(result);
    }

    private void Record(DbCommand command, CommandExecutedEventData eventData)
    {
        _metrics.RecordQuery(
            ClassifyCommand(command.CommandText),
            eventData.Duration.TotalMilliseconds);
    }

    internal static string ClassifyCommand(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return "other";

        var span = commandText.AsSpan().TrimStart();
        if (span.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            return "select";
        if (span.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            return "insert";
        if (span.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            return "update";
        if (span.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
            return "delete";

        return "other";
    }
}

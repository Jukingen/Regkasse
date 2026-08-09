using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Logging;

/// <summary>
/// Human-readable single-line console format for Development:
/// <c>[15:30:45] INFO [Auth] message | Tenant: dev | User: admin@admin.com</c>
/// Production should keep <c>FormatterName=json</c> for Promtail/Loki.
/// </summary>
public sealed class ReadableConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "readable";

    private readonly IOptionsMonitor<ReadableConsoleFormatterOptions> _options;

    public ReadableConsoleFormatter(IOptionsMonitor<ReadableConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var options = _options.CurrentValue;
        var timestamp = DateTimeOffset.Now.ToString(options.TimestampFormat ?? "HH:mm:ss");
        var level = FormatLevel(logEntry.LogLevel);
        var category = ShortCategory(logEntry.Category);

        textWriter.Write('[');
        textWriter.Write(timestamp);
        textWriter.Write("] ");
        textWriter.Write(level);
        textWriter.Write(" [");
        textWriter.Write(category);
        textWriter.Write("] ");

        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (!string.IsNullOrEmpty(message))
            textWriter.Write(message);

        if (options.IncludeScopes && scopeProvider != null)
            WriteScopes(textWriter, scopeProvider);

        if (logEntry.Exception != null)
        {
            textWriter.Write(" | ");
            textWriter.Write(logEntry.Exception.GetType().Name);
            textWriter.Write(": ");
            textWriter.Write(logEntry.Exception.Message);
        }

        textWriter.WriteLine();
    }

    private static void WriteScopes(TextWriter textWriter, IExternalScopeProvider scopeProvider)
    {
        scopeProvider.ForEachScope((scope, state) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                foreach (var pair in pairs)
                {
                    if (pair.Value is null)
                        continue;
                    state.Write(" | ");
                    state.Write(pair.Key);
                    state.Write(": ");
                    state.Write(pair.Value);
                }
            }
            else if (scope is not null)
            {
                state.Write(" | ");
                state.Write(scope);
            }
        }, textWriter);
    }

    private static string FormatLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "NONE"
    };

    private static string ShortCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return "App";

        var lastDot = category.LastIndexOf('.');
        var name = lastDot >= 0 && lastDot < category.Length - 1
            ? category[(lastDot + 1)..]
            : category;

        // Drop common suffixes for readability: MetricsMiddleware → Metrics, AuthController → Auth
        if (name.EndsWith("Middleware", StringComparison.Ordinal))
            name = name[..^"Middleware".Length];
        else if (name.EndsWith("Controller", StringComparison.Ordinal))
            name = name[..^"Controller".Length];
        else if (name.EndsWith("Service", StringComparison.Ordinal) && name.Length > "Service".Length)
            name = name[..^"Service".Length];

        return string.IsNullOrEmpty(name) ? "App" : name;
    }
}

/// <summary>Options for <see cref="ReadableConsoleFormatter"/> (Development console).</summary>
public sealed class ReadableConsoleFormatterOptions : ConsoleFormatterOptions
{
    public ReadableConsoleFormatterOptions()
    {
        TimestampFormat = "HH:mm:ss";
        IncludeScopes = true;
    }
}

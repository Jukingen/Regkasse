using System.Collections;
using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CriticalPathLoggingTests
{
    [Fact]
    public void DevelopmentTenantQuery_LogsInformation()
    {
        var logger = new ListLogger<TenantResolutionMiddleware>();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, environment.Object, logger);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tenant=dev");

        var method = typeof(TenantResolutionMiddleware).GetMethod(
            "LogQueryParameterUsage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(middleware, new object[] { context });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("dev", entry.Message, StringComparison.Ordinal);
        Assert.Contains("{Tenant}", entry.Template, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionTenantQuery_StaysWarning()
    {
        var logger = new ListLogger<TenantResolutionMiddleware>();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask, environment.Object, logger);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tenant=other");

        var method = typeof(TenantResolutionMiddleware).GetMethod(
            "LogQueryParameterUsage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method!.Invoke(middleware, new object[] { context });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, string Template)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => Empty.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var template = string.Empty;
            if (state is IEnumerable pairs)
            {
                foreach (var pair in pairs)
                {
                    if (pair is KeyValuePair<string, object> entry && entry.Key == "{OriginalFormat}")
                        template = entry.Value?.ToString() ?? string.Empty;
                }
            }

            Entries.Add((logLevel, formatter(state, exception), template));
        }

        private sealed class Empty : IDisposable
        {
            public static readonly Empty Instance = new();
            public void Dispose()
            {
            }
        }
    }
}

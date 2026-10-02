using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PendingMigrationsGuardTests
{
    [Fact]
    public void Development_WithPending_LogsWarningAndProceeds()
    {
        var logger = new CaptureLogger();
        var pending = new[] { "20261002000000_One", "20261002000001_Two" };

        var exception = Record.Exception(() =>
            StartupMigrationGuard.Enforce(pending, Environment(Environments.Development), logger));

        Assert.Null(exception);
        Assert.Equal(new[] { LogLevel.Warning }, logger.Levels);
        Assert.Contains("2", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains(pending[0], logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains(pending[1], logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Production_WithPending_ThrowsWithFullList()
    {
        var logger = new CaptureLogger();
        var pending = new[] { "20261002000000_One", "20261002000001_Two" };

        var exception = Assert.Throws<PendingMigrationsException>(() =>
            StartupMigrationGuard.Enforce(pending, Environment(Environments.Production), logger));

        Assert.Equal(pending, exception.PendingMigrationIds);
        Assert.Contains(pending[0], exception.Message, StringComparison.Ordinal);
        Assert.Contains(pending[1], exception.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { LogLevel.Critical }, logger.Levels);
        Assert.Contains("2", logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void NoPending_IsNoOp()
    {
        foreach (var name in new[] { Environments.Development, Environments.Production })
        {
            var logger = new CaptureLogger();

            var exception = Record.Exception(() =>
                StartupMigrationGuard.Enforce(Array.Empty<string>(), Environment(name), logger));

            Assert.Null(exception);
            Assert.Empty(logger.Levels);
        }
    }

    private static IHostEnvironment Environment(string name)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.SetupGet(e => e.EnvironmentName).Returns(name);
        return mock.Object;
    }

    private sealed class CaptureLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = new();

        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }
    }
}

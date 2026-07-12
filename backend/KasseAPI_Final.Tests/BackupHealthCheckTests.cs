using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupHealthCheckTests
{
    private static IOptionsMonitor<BackupOptions> MonitorOf(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    [Theory]
    [InlineData(BackupExecutionAdapterKind.Fake)]
    [InlineData(BackupExecutionAdapterKind.ProductionStub)]
    public async Task CheckHealthAsync_WhenNonProductionAdapter_ReturnsUnhealthy(BackupExecutionAdapterKind kind)
    {
        var check = new HealthChecks.BackupHealthCheck(MonitorOf(new BackupOptions { ExecutionAdapterKind = kind }));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains(kind.ToString(), result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPgDumpBinaryMissing_ReturnsUnhealthy()
    {
        var check = new HealthChecks.BackupHealthCheck(MonitorOf(new BackupOptions
        {
            ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
            PgDumpExecutablePath = Path.Combine(Path.GetTempPath(), $"missing-pg-dump-{Guid.NewGuid():N}"),
        }));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("pg_dump not found", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPgDumpBinaryExists_ReturnsHealthy()
    {
        var tempPgDump = Path.Combine(Path.GetTempPath(), $"pg-dump-test-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(tempPgDump, string.Empty);

        try
        {
            var check = new HealthChecks.BackupHealthCheck(MonitorOf(new BackupOptions
            {
                ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump,
                PgDumpExecutablePath = tempPgDump,
            }));

            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            File.Delete(tempPgDump);
        }
    }
}

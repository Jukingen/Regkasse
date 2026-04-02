using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupEffectiveExecutionAdapterResolverTests
{
    [Fact]
    public void Inherit_returns_config_execution_adapter_kind()
    {
        var o = new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.ProductionStub };
        var k = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(
            o,
            AdminBackupRuntimeExecutionMode.InheritFromConfiguration);
        Assert.Equal(BackupExecutionAdapterKind.ProductionStub, k);
    }

    [Fact]
    public void SimulatedFake_forces_fake_even_when_config_pg_dump()
    {
        var o = new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.PgDump };
        var k = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(
            o,
            AdminBackupRuntimeExecutionMode.SimulatedFake);
        Assert.Equal(BackupExecutionAdapterKind.Fake, k);
    }

    [Fact]
    public void PostgreSqlPgDump_forces_pg_dump_even_when_config_fake()
    {
        var o = new BackupOptions { ExecutionAdapterKind = BackupExecutionAdapterKind.Fake };
        var k = BackupEffectiveExecutionAdapterResolver.ResolveEffectiveAdapterKind(
            o,
            AdminBackupRuntimeExecutionMode.PostgreSqlPgDump);
        Assert.Equal(BackupExecutionAdapterKind.PgDump, k);
    }
}

using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupStrategyPolicyTests
{
    [Fact]
    public void Resolve_prefers_explicit_strategy()
    {
        Assert.Equal(
            BackupStrategyKind.System,
            BackupStrategyPolicy.Resolve(Guid.NewGuid(), BackupStrategyKind.System));
    }

    [Fact]
    public void ResolveExcludeTables_tenant_always_excludes_identity()
    {
        var opts = new BackupOptions { LogicalDumpExcludeTables = Array.Empty<string>() };
        var exclude = BackupStrategyPolicy.ResolveExcludeTables(BackupStrategyKind.Tenant, opts);
        Assert.Contains("AspNetUsers", exclude);
        Assert.Contains("AspNetUserTokens", exclude);
    }

    [Fact]
    public void ResolveExcludeTables_system_includes_identity()
    {
        var opts = new BackupOptions
        {
            LogicalDumpExcludeTables =
            [
                "AspNetUsers",
                "AspNetUserClaims",
                "AspNetUserLogins",
                "AspNetUserTokens",
                "some_noise_table"
            ]
        };
        var exclude = BackupStrategyPolicy.ResolveExcludeTables(BackupStrategyKind.System, opts);
        Assert.DoesNotContain("AspNetUsers", exclude);
        Assert.Contains("some_noise_table", exclude);
    }

    [Fact]
    public void DefaultRetentionDays_tenant_30_system_90()
    {
        Assert.Equal(30, BackupStrategyPolicy.DefaultRetentionDays(BackupStrategyKind.Tenant));
        Assert.Equal(90, BackupStrategyPolicy.DefaultRetentionDays(BackupStrategyKind.System));
    }
}

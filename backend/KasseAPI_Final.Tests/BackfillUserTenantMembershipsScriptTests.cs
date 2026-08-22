using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Guards the repo SQL backfill script content (manual DBA run).</summary>
public class BackfillUserTenantMembershipsScriptTests
{
    [SkippableFact]
    public void Backfill_Sql_Exists_And_Targets_Default_Tenant_And_Is_Idempotent()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "backfill-user-tenant-memberships.sql");
        Skip.IfNot(File.Exists(path), "scripts/**/*.sql is gitignored and is not present in CI checkouts.");

        var text = File.ReadAllText(path);
        Assert.Contains("user_tenant_memberships", text, StringComparison.Ordinal);
        Assert.Contains("AspNetUsers", text, StringComparison.Ordinal);
        Assert.Contains(SystemTenantIds.Platform.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT EXISTS", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("is_active = true", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Backfill_EF_Migration_Contains_Idempotent_Insert()
    {
        var repoRoot = FindRepoRoot();
        var migrationsDir = Path.Combine(repoRoot, "backend", "Migrations");
        var files = Directory.GetFiles(migrationsDir, "*BackfillUserTenantMembershipsData.cs");
        Assert.Single(files);
        var text = File.ReadAllText(files[0]);
        Assert.Contains("user_tenant_memberships", text, StringComparison.Ordinal);
        Assert.Contains("AspNetUsers", text, StringComparison.Ordinal);
        Assert.Contains(SystemTenantIds.Platform.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT EXISTS", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var agents = Path.Combine(dir.FullName, "AGENTS.md");
                var backend = Path.Combine(dir.FullName, "backend");
                if (File.Exists(agents) && Directory.Exists(backend))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}

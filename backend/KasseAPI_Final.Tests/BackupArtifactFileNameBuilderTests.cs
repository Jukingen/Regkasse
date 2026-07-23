using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupArtifactFileNameBuilderTests
{
    private static readonly DateTime FixedUtc = new(2026, 7, 22, 14, 30, 22, DateTimeKind.Utc);

    [Theory]
    [InlineData("cafe", BackupStrategyKind.Tenant, "backup_cafe_tenant_20260722_143022.dump")]
    [InlineData("cafe", BackupStrategyKind.System, "backup_cafe_system_20260722_143022.dump")]
    public void BuildLogicalDumpFileName_includes_strategy(string slug, BackupStrategyKind strategy, string expected)
    {
        Assert.Equal(expected, BackupArtifactFileNameBuilder.BuildLogicalDumpFileName(slug, FixedUtc, strategy));
    }

    [Fact]
    public void BuildTenantLogicalPackageFileName_uses_canonical_pattern()
    {
        Assert.Equal(
            "backup_cafe_tenant_20260722_143022.tenant.zip",
            BackupArtifactFileNameBuilder.BuildTenantLogicalPackageFileName("cafe", FixedUtc));
    }

    [Fact]
    public void BuildSystemPackageFileName_includes_slug_and_strategy()
    {
        Assert.Equal(
            "backup_cafe_system_20260722_143022.system.zip",
            BackupArtifactFileNameBuilder.BuildSystemPackageFileName("cafe", FixedUtc));
    }

    [Fact]
    public void BuildManifestFileName_includes_strategy()
    {
        Assert.Equal(
            "backup_dev_system_20260722_143022_manifest.json",
            BackupArtifactFileNameBuilder.BuildManifestFileName("dev", FixedUtc, BackupStrategyKind.System));
    }

    [Fact]
    public void BuildLogicalDumpFileName_sanitizes_slug()
    {
        Assert.Equal(
            "backup_cafe_alpha_tenant_20260722_143022.dump",
            BackupArtifactFileNameBuilder.BuildLogicalDumpFileName("Cafe Alpha", FixedUtc, BackupStrategyKind.Tenant));
    }

    [Fact]
    public void FormatSizeHint_and_InsertSizeHint()
    {
        Assert.Equal("12mb", BackupArtifactFileNameBuilder.FormatSizeHint(12L * 1024 * 1024));
        Assert.Equal(
            "backup_cafe_tenant_20260722_143022_12mb.tenant.zip",
            BackupArtifactFileNameBuilder.InsertSizeHint(
                "backup_cafe_tenant_20260722_143022.tenant.zip",
                12L * 1024 * 1024));
    }
}

public sealed class BackupRunTenantSlugResolverTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_slug_resolver_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task ResolveSlugAsync_manual_all_tenants_returns_all()
    {
        await using var db = CreateDb();
        var run = new BackupRun
        {
            TriggerSource = BackupTriggerSource.Manual,
            IdempotencyKey = "manual-all-tenants-1734567890123",
            AdapterKind = "PgDump"
        };

        var slug = await BackupRunTenantSlugResolver.ResolveSlugAsync(run, db);

        Assert.Equal(BackupRunTenantSlugResolver.AllTenantsSlug, slug);
    }

    [Fact]
    public async Task ResolveSlugAsync_manual_tenant_key_looks_up_slug()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            Status = TenantStatuses.Active
        });
        await db.SaveChangesAsync();

        var run = new BackupRun
        {
            TriggerSource = BackupTriggerSource.Manual,
            IdempotencyKey = $"manual-tenant-{tenantId:D}-1734567890123",
            AdapterKind = "PgDump"
        };

        var slug = await BackupRunTenantSlugResolver.ResolveSlugAsync(run, db);

        Assert.Equal("dev", slug);
    }

    [Fact]
    public async Task ResolveSlugAsync_scheduled_run_returns_scheduled()
    {
        await using var db = CreateDb();
        var run = new BackupRun
        {
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump"
        };

        var slug = await BackupRunTenantSlugResolver.ResolveSlugAsync(run, db);

        Assert.Equal(BackupRunTenantSlugResolver.ScheduledSlug, slug);
    }

    [Fact]
    public async Task ResolveSlugAsync_manual_without_tenant_hint_returns_deployment()
    {
        await using var db = CreateDb();
        var run = new BackupRun
        {
            TriggerSource = BackupTriggerSource.Manual,
            IdempotencyKey = "manual-1734567890123",
            AdapterKind = "PgDump"
        };

        var slug = await BackupRunTenantSlugResolver.ResolveSlugAsync(run, db);

        Assert.Equal(BackupRunTenantSlugResolver.DeploymentSlug, slug);
    }
}

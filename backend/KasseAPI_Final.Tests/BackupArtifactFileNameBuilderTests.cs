using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupArtifactFileNameBuilderTests
{
    private static readonly DateTime FixedUtc = new(2026, 7, 3, 15, 1, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("dev", "backup_dev_20260703_150100.dump")]
    [InlineData("prod", "backup_prod_20260703_150100.dump")]
    [InlineData("testcafe", "backup_testcafe_20260703_150100.dump")]
    public void BuildLogicalDumpFileName_uses_slug_and_timestamp(string slug, string expected)
    {
        Assert.Equal(expected, BackupArtifactFileNameBuilder.BuildLogicalDumpFileName(slug, FixedUtc));
    }

    [Fact]
    public void BuildManifestFileName_appends_manifest_suffix()
    {
        Assert.Equal(
            "backup_dev_20260703_150100_manifest.json",
            BackupArtifactFileNameBuilder.BuildManifestFileName("dev", FixedUtc));
    }

    [Fact]
    public void BuildLogicalDumpFileName_sanitizes_slug()
    {
        Assert.Equal(
            "backup_dev_alpha_20260703_150100.dump",
            BackupArtifactFileNameBuilder.BuildLogicalDumpFileName("Cafe Alpha", FixedUtc));
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
        return new AppDbContext(options);
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

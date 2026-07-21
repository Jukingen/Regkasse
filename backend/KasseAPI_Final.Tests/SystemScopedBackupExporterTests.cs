using System.IO.Compression;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SystemScopedBackupExporterTests
{
    [Fact]
    public async Task ExportAsync_includes_active_tenants_identity_and_nested_tenant_packages()
    {
        var tenantA = Guid.NewGuid();
        var tenantInactive = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(ExportAsync_includes_active_tenants_identity_and_nested_tenant_packages) + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "Active", Slug = "active", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant
            {
                Id = tenantInactive,
                Name = "Off",
                Slug = "off",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Item",
            TenantId = tenantA,
            Price = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@example.com",
            NormalizedEmail = "ADMIN@EXAMPLE.COM",
            FirstName = "Ada",
            LastName = "Admin",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.ActivatedLicenses.Add(new ActivatedLicense
        {
            Id = Guid.NewGuid(),
            LicenseKey = "REGK-TEST",
            ValidUntilUtc = DateTime.UtcNow.AddYears(1),
            ActivatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var zipPath = Path.Combine(Path.GetTempPath(), $"system-backup-{Guid.NewGuid():N}.zip");
        try
        {
            var exporter = new SystemScopedBackupExporter(new TenantScopedBackupExporter());
            var result = await exporter.ExportAsync(db, zipPath);

            Assert.Equal(1, result.Manifest.ActiveTenantCount);
            Assert.Contains(tenantA, result.Manifest.ActiveTenantIds);
            Assert.DoesNotContain(tenantInactive, result.Manifest.ActiveTenantIds);
            Assert.True(result.Manifest.SectionRowCounts["identity/users.json"] >= 1);
            Assert.True(result.Manifest.SectionRowCounts["platform/activated_licenses.json"] >= 1);
            Assert.True(result.ByteSize > 0);

            await using var zip = ZipFile.OpenRead(zipPath);
            Assert.NotNull(zip.GetEntry("manifest.json"));
            Assert.NotNull(zip.GetEntry("identity/users.json"));
            Assert.NotNull(zip.GetEntry("tenants/active.tenant.zip"));
            Assert.Null(zip.GetEntry("tenants/off.tenant.zip"));

            await using var usersStream = zip.GetEntry("identity/users.json")!.Open();
            using var doc = await JsonDocument.ParseAsync(usersStream);
            Assert.True(doc.RootElement.GetArrayLength() >= 1);
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }
}

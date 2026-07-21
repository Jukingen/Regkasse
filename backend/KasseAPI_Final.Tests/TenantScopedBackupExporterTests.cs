using System.IO.Compression;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantScopedBackupExporterTests
{
    [Fact]
    public async Task ExportAsync_writes_only_matching_tenant_rows_and_excludes_other_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(ExportAsync_writes_only_matching_tenant_rows_and_excludes_other_tenant) + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "A", Slug = "a", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = tenantB, Name = "B", Slug = "b", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Products.AddRange(
            new Product { Id = Guid.NewGuid(), Name = "Coffee A", TenantId = tenantA, Price = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), Name = "Coffee B", TenantId = tenantB, Price = 2, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Cust A",
            TenantId = tenantA,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var zipPath = Path.Combine(Path.GetTempPath(), $"tenant-backup-{Guid.NewGuid():N}.zip");
        try
        {
            var exporter = new TenantScopedBackupExporter();
            var result = await exporter.ExportAsync(db, tenantA, "a", zipPath);

            Assert.Equal(tenantA, result.Manifest.TenantId);
            Assert.Equal(1, result.Manifest.TableRowCounts["products.json"]);
            Assert.Equal(1, result.Manifest.TableRowCounts["customers.json"]);
            Assert.True(result.ByteSize > 0);
            Assert.Contains("identity", result.Manifest.ExcludedCategories);

            await using var zip = ZipFile.OpenRead(zipPath);
            Assert.NotNull(zip.GetEntry("manifest.json"));
            Assert.NotNull(zip.GetEntry("products.json"));
            Assert.Null(zip.GetEntry("AspNetUsers.json"));

            await using var productsStream = zip.GetEntry("products.json")!.Open();
            using var doc = await JsonDocument.ParseAsync(productsStream);
            Assert.Equal(1, doc.RootElement.GetArrayLength());
            Assert.Equal("Coffee A", doc.RootElement[0].GetProperty("name").GetString());
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }
}

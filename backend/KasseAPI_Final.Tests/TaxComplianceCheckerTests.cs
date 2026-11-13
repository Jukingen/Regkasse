using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxComplianceCheckerTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_compliance_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task CheckComplianceAsync_DetectsMissingAndInvalidRates()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var validGroupId = Guid.NewGuid();
        var invalidGroupId = Guid.NewGuid();
        db.TaxGroups.AddRange(
            new TaxGroup
            {
                Id = validGroupId,
                TenantId = tenantId,
                Name = "Normalsatz",
                Rate = 20m,
                IsActive = true,
                IsSystem = true,
                GroupType = TaxGroupType.Standard,
                CreatedAt = DateTime.UtcNow,
            },
            new TaxGroup
            {
                Id = invalidGroupId,
                TenantId = tenantId,
                Name = "Custom 7%",
                Rate = 7m,
                IsActive = true,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow,
            });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "c1",
            Name = "Cat",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.Products.AddRange(
            MakeProduct(tenantId, categoryId, validGroupId, "OK", 20m),
            MakeProduct(tenantId, categoryId, invalidGroupId, "BadRate", 7m),
            MakeProduct(tenantId, categoryId, Guid.Empty, "NoGroup", 20m));
        await db.SaveChangesAsync();

        var regulation = new TaxRegulationService(db, NullLogger<TaxRegulationService>.Instance);
        var sut = new TaxComplianceChecker(db, regulation, NullLogger<TaxComplianceChecker>.Instance);

        var report = await sut.CheckComplianceAsync(tenantId);

        Assert.False(report.IsCompliant);
        Assert.Equal(3, report.TotalProducts);
        Assert.True(report.NonCompliantProducts >= 2);
        Assert.Contains(report.Issues, i => i.Code == "MISSING_TAX_GROUP");
        Assert.Contains(report.Issues, i => i.Code == "INVALID_TAX_RATE");
    }

    [Fact]
    public async Task CheckComplianceAsync_AllValid_IsCompliant()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        var groupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = groupId,
            TenantId = tenantId,
            Name = "Ermäßigt",
            Rate = 10m,
            IsActive = true,
            GroupType = TaxGroupType.Reduced,
            CreatedAt = DateTime.UtcNow,
        });
        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "c1",
            Name = "Cat",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Products.Add(MakeProduct(tenantId, categoryId, groupId, "Bread", 10m));
        await db.SaveChangesAsync();

        var regulation = new TaxRegulationService(db, NullLogger<TaxRegulationService>.Instance);
        var sut = new TaxComplianceChecker(db, regulation, NullLogger<TaxComplianceChecker>.Instance);
        var report = await sut.CheckComplianceAsync(tenantId);

        Assert.True(report.IsCompliant);
        Assert.Empty(report.Issues);
        Assert.Equal(1, report.CompliantProducts);
    }

    private static Product MakeProduct(
        Guid tenantId,
        Guid categoryId,
        Guid taxGroupId,
        string name,
        decimal taxRate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = name,
            Category = "Cat",
            Price = 1m,
            TaxRate = taxRate,
            TaxType = TaxTypes.FromRate(taxRate),
            Unit = "pcs",
            Barcode = $"BC-{name}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
}

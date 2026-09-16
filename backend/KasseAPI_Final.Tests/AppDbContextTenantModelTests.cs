using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Model conventions for multi-tenant isolation: TenantId indexes and global query filters on <see cref="ITenantEntity"/>.
/// </summary>
public sealed class AppDbContextTenantModelTests
{
    [Fact]
    public void Every_ITenantEntity_has_TenantId_leading_index_or_pk()
    {
        using var db = CreateContext();
        var missing = new List<string>();

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (clr is null || clr.GetInterface(nameof(ITenantEntity)) is null)
                continue;

            var tenantProp = entityType.FindProperty(nameof(ITenantEntity.TenantId));
            Assert.NotNull(tenantProp);

            if (HasTenantIdLeadingCoverage(entityType, tenantProp!))
                continue;

            missing.Add(clr.Name);
        }

        Assert.True(
            missing.Count == 0,
            "ITenantEntity types missing TenantId-leading index/PK: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_ITenantEntity_has_a_query_filter()
    {
        using var db = CreateContext();
        var missing = new List<string>();

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (clr is null || clr.GetInterface(nameof(ITenantEntity)) is null)
                continue;

            if (entityType.GetQueryFilter() is null)
                missing.Add(clr.Name);
        }

        Assert.True(
            missing.Count == 0,
            "ITenantEntity types missing query filter: " + string.Join(", ", missing));
    }

    [Fact]
    public void Unfiltered_required_navigations_to_filtered_principals_match_documented_set()
    {
        // Expected EF PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning pairs.
        // Dependents are intentionally not ITenantEntity (or use a different isolation path).
        // Customer / LicenseSale mismatches are mitigated with Navigation.IsRequired(false).
        // See docs/ENVIRONMENT_CONFIGURATION.md § Common Warnings.
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "ActivityEventRead.ActivityEvent -> ActivityEvent",
            "CashRegisterTransaction.CashRegister -> CashRegister",
            "OnlineOrderItem.OnlineOrder -> OnlineOrder",
            "PaymentDetails.CashRegister -> CashRegister",
            "RksvSpecialReceiptFinanzOnlineSubmission.(no-nav) -> CashRegister",
            "SplitItem.Product -> Product",
            "SplitItem.SplitSession -> SplitSession",
            "TagesberichtReport.CashRegister -> CashRegister",
        };

        using var db = CreateContext();
        var actual = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            foreach (var fk in entityType.GetForeignKeys())
            {
                if (!fk.IsRequired)
                    continue;
                if (fk.PrincipalEntityType.GetQueryFilter() is null)
                    continue;
                if (entityType.GetQueryFilter() is not null)
                    continue;

                var nav = fk.DependentToPrincipal?.Name ?? "(no-nav)";
                var dependentName = entityType.ClrType?.Name ?? entityType.Name;
                var principalName = fk.PrincipalEntityType.ClrType?.Name ?? fk.PrincipalEntityType.Name;
                actual.Add($"{dependentName}.{nav} -> {principalName}");
            }
        }

        var unexpected = actual.Except(expected).OrderBy(x => x).ToList();
        var missing = expected.Except(actual).OrderBy(x => x).ToList();
        Assert.True(
            unexpected.Count == 0 && missing.Count == 0,
            "Unexpected new filter-interaction pairs (add optional navigation or update the documented set): "
            + string.Join("; ", unexpected)
            + " | Documented pairs that disappeared: "
            + string.Join("; ", missing));
    }

    [Fact]
    public void Tenant_query_filter_is_fail_closed_when_ambient_tenant_is_null()
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var accessor = new TenantTestDoubles.MutableTenantAccessor(null);
        var dbName = $"TenantModel_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using (var seed = new AppDbContext(options, new TenantTestDoubles.MutableTenantAccessor(tenantA)))
        {
            seed.Tenants.Add(new Tenant
            {
                Id = tenantA,
                Name = "A",
                Slug = "tenant-a",
                Status = TenantStatuses.Active,
            });
            seed.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Key = "cat-a",
                Name = "Cat A",
                TenantId = tenantA,
            });
            seed.SaveChanges();
        }

        using var db = new AppDbContext(options, accessor);
        Assert.Empty(db.Categories.ToList());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantModelMeta_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, new TenantTestDoubles.MutableTenantAccessor(Guid.NewGuid()));
    }

    private static bool HasTenantIdLeadingCoverage(IEntityType entityType, IProperty tenantProp)
    {
        var pk = entityType.FindPrimaryKey();
        if (pk is not null
            && pk.Properties.Count == 1
            && pk.Properties[0] == tenantProp)
            return true;

        return entityType.GetIndexes()
            .Any(ix => ix.Properties.Count > 0 && ix.Properties[0] == tenantProp);
    }
}

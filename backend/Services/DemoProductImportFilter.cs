using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Data.CategorySeed;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

internal static class DemoProductImportFilter
{
    internal static readonly Guid DemoProductIdNamespace = Guid.Parse("6b5f4e2a-9c1d-4f3a-8e2b-000000000001");

    internal static Guid ResolveDemoProductId(DemoProduct product)
    {
        if (product.Id != Guid.Empty)
            return product.Id;

        var input = $"{DemoProductIdNamespace:N}:{product.Category}:{product.Name}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static void NormalizeDemoProductIds(DemoData data)
    {
        foreach (var product in data.Products)
            product.Id = ResolveDemoProductId(product);
    }

    internal static List<DemoCategory> SelectCategories(DemoData data, DemoImportRequest request)
    {
        var categoriesToImport = data.Categories.AsEnumerable();

        if (request.SelectedCategories.Count > 0)
        {
            var selected = new HashSet<string>(
                request.SelectedCategories.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                StringComparer.Ordinal);
            categoriesToImport = categoriesToImport.Where(c =>
                selected.Contains(c.Name)
                || selected.Contains(c.Key)
                || (SystemCategories.TryResolve(c.Name, out var byName) && selected.Contains(byName.Key))
                || (SystemCategories.TryResolve(c.Key, out var byKey) && (selected.Contains(byKey.Key) || selected.Contains(byKey.DisplayName))));
        }
        else if (request.ExcludedCategories.Count > 0)
        {
            var excluded = new HashSet<string>(
                request.ExcludedCategories.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                StringComparer.Ordinal);
            categoriesToImport = categoriesToImport.Where(c =>
                !excluded.Contains(c.Name)
                && !excluded.Contains(c.Key)
                && !(SystemCategories.TryResolve(c.Name, out var byName) && excluded.Contains(byName.Key))
                && !(SystemCategories.TryResolve(c.Key, out var byKey) && (excluded.Contains(byKey.Key) || excluded.Contains(byKey.DisplayName))));
        }

        return categoriesToImport.OrderBy(c => c.SortOrder).ToList();
    }

    internal static List<DemoProduct> SelectProducts(
        DemoData data,
        IReadOnlyDictionary<string, DemoCategory> categories,
        DemoImportRequest request)
    {
        var productsToImport = data.Products
            .Where(p => categories.ContainsKey(p.Category))
            .ToList();

        if (request.SelectedProductIds.Count > 0)
        {
            var selectedIds = request.SelectedProductIds
                .Where(id => id != Guid.Empty)
                .ToHashSet();
            productsToImport = productsToImport
                .Where(p => selectedIds.Contains(p.Id))
                .ToList();
        }

        return productsToImport;
    }
}

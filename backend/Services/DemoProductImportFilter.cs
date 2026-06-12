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
            var selected = ExpandCategoryReferences(request.SelectedCategories);
            categoriesToImport = categoriesToImport.Where(c => CategoryMatchesReferenceSet(c, selected));
        }
        else if (request.ExcludedCategories.Count > 0)
        {
            var excluded = ExpandCategoryReferences(request.ExcludedCategories);
            categoriesToImport = categoriesToImport.Where(c => !CategoryMatchesReferenceSet(c, excluded));
        }

        return categoriesToImport.OrderBy(c => c.SortOrder).ToList();
    }

    /// <summary>
    /// Expands wizard/JSON labels (e.g. Alkoholfreie-Getrnke) to canonical keys and display names.
    /// </summary>
    private static HashSet<string> ExpandCategoryReferences(IEnumerable<string> references)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in references.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()))
        {
            expanded.Add(raw);
            if (SystemCategories.TryResolve(raw, out var resolved))
            {
                expanded.Add(resolved.Key);
                expanded.Add(resolved.DisplayName);
            }
        }

        return expanded;
    }

    private static bool CategoryMatchesReferenceSet(DemoCategory category, HashSet<string> references)
    {
        if (references.Contains(category.Name))
            return true;

        if (!string.IsNullOrWhiteSpace(category.Key) && references.Contains(category.Key))
            return true;

        if (SystemCategories.TryResolve(category.Name, out var byName)
            && (references.Contains(byName.Key) || references.Contains(byName.DisplayName)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(category.Key)
            && SystemCategories.TryResolve(category.Key, out var byKey)
            && (references.Contains(byKey.Key) || references.Contains(byKey.DisplayName)))
        {
            return true;
        }

        return false;
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

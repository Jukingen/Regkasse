using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>Resolves localized product display strings for POS/catalog (de, en, tr).</summary>
public static class ProductLocalization
{
    public static void SyncCanonicalFields(Product product)
    {
        product.Name = ResolveName(product, "de");
        product.Description = ResolveDescription(product, "de");
    }

    public static string ResolveName(Product product, string? locale)
    {
        return locale?.Split('-')[0].ToLowerInvariant() switch
        {
            "en" => FirstNonEmpty(product.NameEn, product.NameDe, product.Name),
            "tr" => FirstNonEmpty(product.NameTr, product.NameDe, product.Name),
            _ => FirstNonEmpty(product.NameDe, product.Name),
        };
    }

    public static string? ResolveDescription(Product product, string? locale)
    {
        return locale?.Split('-')[0].ToLowerInvariant() switch
        {
            "en" => FirstNonEmptyOptional(product.DescriptionEn, product.DescriptionDe, product.Description),
            "tr" => FirstNonEmptyOptional(product.DescriptionTr, product.DescriptionDe, product.Description),
            _ => FirstNonEmptyOptional(product.DescriptionDe, product.Description),
        };
    }

    public static void ApplyLocalizedNames(
        Product product,
        string? nameDe,
        string? nameEn,
        string? nameTr,
        string? descriptionDe = null,
        string? descriptionEn = null,
        string? descriptionTr = null)
    {
        if (!string.IsNullOrWhiteSpace(nameDe))
            product.NameDe = nameDe.Trim();
        if (!string.IsNullOrWhiteSpace(nameEn))
            product.NameEn = nameEn.Trim();
        if (!string.IsNullOrWhiteSpace(nameTr))
            product.NameTr = nameTr.Trim();
        if (descriptionDe != null)
            product.DescriptionDe = string.IsNullOrWhiteSpace(descriptionDe) ? null : descriptionDe.Trim();
        if (descriptionEn != null)
            product.DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim();
        if (descriptionTr != null)
            product.DescriptionTr = string.IsNullOrWhiteSpace(descriptionTr) ? null : descriptionTr.Trim();

        SyncCanonicalFields(product);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static string? FirstNonEmptyOptional(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}

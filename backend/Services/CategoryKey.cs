using System.Text.RegularExpressions;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services;

/// <summary>Generates and validates immutable category keys; infers RKSV fiscal category from catalog text.</summary>
public static class CategoryKey
{
    private static readonly Regex KeyRegex = new(@"^[a-z0-9][a-z0-9-]{0,98}[a-z0-9]$|^[a-z0-9]$", RegexOptions.Compiled);

    public static string FromDisplayName(string displayName)
    {
        var key = TenantSlugSuggestions.NormalizeSlug(displayName);
        return string.IsNullOrEmpty(key) ? "category" : key;
    }

    public static bool IsValid(string key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= 100
        && KeyRegex.IsMatch(key);

    public static RksvProductCategory InferFiscalCategory(string name, string? description = null)
    {
        var text = NormalizeForMatch($"{name} {description}");

        if (ContainsAny(text, "tabak", "zigarette", "cigarette", "tobacco"))
            return RksvProductCategory.Tobacco;

        if (ContainsAny(text, "alkohol", "bier", "wein", "spirituose", "schnaps", "cocktail", "prosecco", "champagner", "whisky", "vodka")
            && !ContainsAny(text, "alkoholfrei", "non-alcoholic", "alkoholfreie"))
            return RksvProductCategory.AlcoholicBeverage;

        if (ContainsAny(text, "getränk", "getraenk", "getrnke", "drink", "cola", "fanta", "saft", "wasser", "limo", "kaffee", "tee", "sprudel"))
            return RksvProductCategory.Beverage;

        return RksvProductCategory.Food;
    }

    private static string NormalizeForMatch(string value) =>
        value.Trim().ToLowerInvariant()
            .Replace('ä', 'a')
            .Replace('ö', 'o')
            .Replace('ü', 'u')
            .Replace("ß", "ss", StringComparison.Ordinal);

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

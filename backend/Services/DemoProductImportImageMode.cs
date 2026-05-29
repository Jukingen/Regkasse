namespace KasseAPI_Final.Services;

public enum DemoImportImageMode
{
    None = 0,
    CategoryPlaceholder = 1,
    DefaultAsset = 2,
}

internal static class DemoProductImportImageModeParser
{
    internal static DemoImportImageMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DemoImportImageMode.CategoryPlaceholder;

        if (Enum.TryParse<DemoImportImageMode>(value, ignoreCase: true, out var parsed))
            return parsed;

        return value.Trim().ToLowerInvariant() switch
        {
            "skip" or "none" or "no" => DemoImportImageMode.None,
            "category" or "categoryplaceholder" or "category_placeholder" => DemoImportImageMode.CategoryPlaceholder,
            "default" or "defaultasset" or "default_asset" or "default-food" => DemoImportImageMode.DefaultAsset,
            _ => DemoImportImageMode.CategoryPlaceholder,
        };
    }
}

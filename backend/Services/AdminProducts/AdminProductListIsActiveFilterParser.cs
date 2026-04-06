namespace KasseAPI_Final.Services.AdminProducts;

/// <summary>
/// Parses GET /api/admin/products <c>isActive</c> query (omit/true = active only; false = inactive only; all = both).
/// </summary>
public enum AdminProductListIsActiveFilterMode
{
    All,
    ActiveOnly,
    InactiveOnly
}

public static class AdminProductListIsActiveFilterParser
{
    public static bool TryParse(string? isActive, out AdminProductListIsActiveFilterMode mode, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(isActive))
        {
            mode = AdminProductListIsActiveFilterMode.ActiveOnly;
            return true;
        }

        var s = isActive.Trim().ToLowerInvariant();
        switch (s)
        {
            case "all":
                mode = AdminProductListIsActiveFilterMode.All;
                return true;
            case "true":
            case "1":
                mode = AdminProductListIsActiveFilterMode.ActiveOnly;
                return true;
            case "false":
            case "0":
                mode = AdminProductListIsActiveFilterMode.InactiveOnly;
                return true;
            default:
                mode = default;
                error = "Invalid isActive value; use true, false, or all.";
                return false;
        }
    }
}

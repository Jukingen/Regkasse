using System.Globalization;
using System.Resources;
using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Resources;

/// <summary>Reads embedded <c>PasswordErrorMessages*.resx</c> satellite resources.</summary>
public static class PasswordErrorResources
{
    private static readonly ResourceManager Manager = new(
        "Resources.PasswordErrorMessages",
        typeof(PasswordErrorResources).Assembly);

    public static string? TryGet(string name, string? language, params object[] args)
    {
        var culture = ToCulture(language);
        var message = Manager.GetString(name, culture);
        if (message == null)
            return null;

        return args.Length > 0
            ? string.Format(culture, message, args)
            : message;
    }

    private static CultureInfo ToCulture(string? language) =>
        LanguageMiddleware.NormalizeLanguage(language) switch
        {
            "en" => CultureInfo.GetCultureInfo("en"),
            "tr" => CultureInfo.GetCultureInfo("tr"),
            _ => CultureInfo.GetCultureInfo("de"),
        };
}

using System.Globalization;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Resolves request language from <c>?lang=</c> or <c>Accept-Language</c> and stores it on
/// <see cref="HttpContext.Items"/> for API message localization. Defaults to German (<c>de</c>).
/// </summary>
public sealed class LanguageMiddleware
{
    public const string LanguageItemKey = "Language";
    public const string DefaultLanguage = "de";

    private readonly RequestDelegate _next;

    public LanguageMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var queryLang = context.Request.Query["lang"].FirstOrDefault();
        var acceptLanguage = context.Request.Headers.AcceptLanguage.FirstOrDefault();
        var raw = !string.IsNullOrWhiteSpace(queryLang) ? queryLang : acceptLanguage;
        var language = NormalizeLanguage(raw);

        var cultureInfo = language switch
        {
            "en" => CultureInfo.GetCultureInfo("en-GB"),
            "tr" => CultureInfo.GetCultureInfo("tr-TR"),
            _ => CultureInfo.GetCultureInfo("de-AT"),
        };

        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
        context.Items[LanguageItemKey] = language;

        await _next(context).ConfigureAwait(false);
    }

    public static string NormalizeLanguage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultLanguage;

        var first = raw.Split(',')[0].Trim();
        var code = first.Split('-')[0].Trim().ToLowerInvariant();
        return code switch
        {
            "en" => "en",
            "tr" => "tr",
            "de" => "de",
            _ => DefaultLanguage,
        };
    }
}

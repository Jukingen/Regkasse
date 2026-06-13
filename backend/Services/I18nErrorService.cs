using System.Globalization;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Resources;
using Microsoft.Extensions.Localization;

namespace KasseAPI_Final.Services;

public interface II18nErrorService
{
    string GetMessage(string key, params object[] args);
}

public sealed class I18nErrorService : II18nErrorService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public I18nErrorService(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizer = localizer;
    }

    public string GetMessage(string key, params object[] args)
    {
        ApplyRequestCulture();
        var language = ResolveLanguage();

        var fromResx = ErrorMessageResources.TryGet(key, language, args);
        if (fromResx != null)
            return fromResx;

        var localized = args.Length > 0 ? _localizer[key, args] : _localizer[key];
        if (!localized.ResourceNotFound)
            return localized.Value;

        return key;
    }

    private string ResolveLanguage()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue(LanguageMiddleware.LanguageItemKey, out var stored) == true
            && stored is string lang
            && !string.IsNullOrWhiteSpace(lang))
        {
            return LanguageMiddleware.NormalizeLanguage(lang);
        }

        return LanguageMiddleware.DefaultLanguage;
    }

    private void ApplyRequestCulture()
    {
        var culture = ResolveLanguage() switch
        {
            "en" => CultureInfo.GetCultureInfo("en-GB"),
            "tr" => CultureInfo.GetCultureInfo("tr-TR"),
            _ => CultureInfo.GetCultureInfo("de-AT"),
        };

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}

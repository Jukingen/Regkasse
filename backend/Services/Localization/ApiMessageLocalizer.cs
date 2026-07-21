using KasseAPI_Final.Localization;
using KasseAPI_Final.Middleware;

namespace KasseAPI_Final.Services.Localization;

public sealed class ApiMessageLocalizer : IApiMessageLocalizer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly II18nErrorService _i18nErrors;

    public ApiMessageLocalizer(IHttpContextAccessor httpContextAccessor, II18nErrorService i18nErrors)
    {
        _httpContextAccessor = httpContextAccessor;
        _i18nErrors = i18nErrors;
    }

    public string Get(string key)
    {
        if (ApiMessageKeyResourceMap.TryGetResourceName(key, out var resourceName))
            return _i18nErrors.GetMessage(resourceName);

        return ApiMessageCatalog.Get(key, ResolveLanguage());
    }

    private string ResolveLanguage()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue(LanguageMiddleware.LanguageItemKey, out var stored) == true
            && stored is string lang
            && !string.IsNullOrWhiteSpace(lang))
        {
            return lang;
        }

        return LanguageMiddleware.DefaultLanguage;
    }
}

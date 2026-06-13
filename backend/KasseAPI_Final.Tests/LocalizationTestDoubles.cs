using System.Globalization;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace KasseAPI_Final.Tests;

internal static class LocalizationTestDoubles
{
    private static readonly ServiceProvider LocalizationProvider = BuildLocalizationProvider();

    public static II18nErrorService I18nErrorService(string language = LanguageMiddleware.DefaultLanguage)
    {
        var http = new DefaultHttpContext();
        http.Items[LanguageMiddleware.LanguageItemKey] = language;
        ApplyCulture(language);

        var localizer = LocalizationProvider.GetRequiredService<IStringLocalizer<ErrorMessages>>();
        return new I18nErrorService(new HttpContextAccessor { HttpContext = http }, localizer);
    }

    public static IApiMessageLocalizer ApiMessageLocalizer(string language = LanguageMiddleware.DefaultLanguage)
    {
        var http = new DefaultHttpContext();
        http.Items[LanguageMiddleware.LanguageItemKey] = language;
        return new ApiMessageLocalizer(
            new HttpContextAccessor { HttpContext = http },
            I18nErrorService(language));
    }

    public static PasswordErrorTranslator PasswordErrorTranslator(string language = LanguageMiddleware.DefaultLanguage)
    {
        var http = new DefaultHttpContext();
        http.Items[LanguageMiddleware.LanguageItemKey] = language;
        var localizer = LocalizationProvider.GetRequiredService<IStringLocalizer<PasswordErrorMessages>>();
        return new PasswordErrorTranslator(new HttpContextAccessor { HttpContext = http }, localizer);
    }

    private static void ApplyCulture(string language)
    {
        var culture = language switch
        {
            "en" => CultureInfo.GetCultureInfo("en-GB"),
            "tr" => CultureInfo.GetCultureInfo("tr-TR"),
            _ => CultureInfo.GetCultureInfo("de-AT"),
        };
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static ServiceProvider BuildLocalizationProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services.BuildServiceProvider();
    }
}

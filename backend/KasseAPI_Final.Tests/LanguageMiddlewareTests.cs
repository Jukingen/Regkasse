using KasseAPI_Final.Localization;
using KasseAPI_Final.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LanguageMiddlewareTests
{
    [Theory]
    [InlineData(null, "de")]
    [InlineData("", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("en-US,de;q=0.9", "en")]
    [InlineData("tr-TR", "tr")]
    [InlineData("fr-FR", "de")]
    public void NormalizeLanguage_maps_supported_and_defaults(string? raw, string expected)
    {
        Assert.Equal(expected, LanguageMiddleware.NormalizeLanguage(raw));
    }

    [Fact]
    public async Task InvokeAsync_prefers_query_lang_over_accept_language()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "en-US";
        context.Request.QueryString = new QueryString("?lang=tr");

        string? storedLanguage = null;
        var middleware = new LanguageMiddleware(ctx =>
        {
            storedLanguage = ctx.Items[LanguageMiddleware.LanguageItemKey] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("tr", storedLanguage);
    }

    [Fact]
    public void I18nErrorService_returns_german_for_default_language()
    {
        var service = LocalizationTestDoubles.I18nErrorService("de");
        Assert.Equal("Ungültiges Passwort", service.GetMessage("InvalidPassword"));
    }

    [Fact]
    public void I18nErrorService_returns_turkish_when_requested()
    {
        var service = LocalizationTestDoubles.I18nErrorService("tr");
        Assert.Equal("Geçersiz şifre", service.GetMessage("InvalidPassword"));
    }

    [Fact]
    public void ApiMessageLocalizer_reads_from_resx_for_mapped_keys()
    {
        var localizer = LocalizationTestDoubles.ApiMessageLocalizer("en");
        Assert.Equal("Invalid username or password", localizer.Get(ApiMessageKeys.InvalidLoginCredentials));
        Assert.Equal("Invalid password", localizer.Get(ApiMessageKeys.InvalidPassword));
    }
}

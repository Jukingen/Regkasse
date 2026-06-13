using System.Globalization;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace KasseAPI_Final.Services;

public class PasswordErrorTranslator
{
    private const int RequiredPasswordLength = 8;
    private const int RequiredUniqueChars = 1;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStringLocalizer<PasswordErrorMessages> _localizer;

    public PasswordErrorTranslator(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<PasswordErrorMessages> localizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizer = localizer;
    }

    public string TranslateError(IdentityError error)
    {
        return error.Code switch
        {
            "PasswordTooShort" => GetLocalizedString("PasswordTooShort", RequiredPasswordLength),
            "PasswordRequiresNonAlphanumeric" => GetLocalizedString("PasswordRequiresNonAlphanumeric"),
            "PasswordRequiresDigit" => GetLocalizedString("PasswordRequiresDigit"),
            "PasswordRequiresLower" => GetLocalizedString("PasswordRequiresLower"),
            "PasswordRequiresUpper" => GetLocalizedString("PasswordRequiresUpper"),
            "PasswordRequiresUniqueChars" => GetLocalizedString("PasswordRequiresUniqueChars", RequiredUniqueChars),
            _ => error.Description,
        };
    }

    public string TranslateIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var userFriendlyErrors = new List<string>();

        foreach (var error in errors)
        {
            userFriendlyErrors.Add(TranslateError(error));
        }

        return string.Join(" ", userFriendlyErrors);
    }

    public PasswordValidationResponse GetValidationResponse(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return new PasswordValidationResponse { Success = true };
        }

        var errors = result.Errors.Select(e => e.Code).ToList();
        var userMessage = TranslateIdentityErrors(result.Errors);

        return new PasswordValidationResponse
        {
            Success = false,
            Message = userMessage,
            ErrorCodes = errors,
        };
    }

    public object BuildPasswordValidationBadRequest(IEnumerable<IdentityError> errors)
    {
        var errorList = errors.ToList();
        var validation = GetValidationResponse(IdentityResult.Failed(errorList.ToArray()));

        return new
        {
            message = validation.Message,
            code = "PASSWORD_VALIDATION_FAILED",
            errorCodes = validation.ErrorCodes,
            errors = errorList.Select(e => new { code = e.Code, description = TranslateError(e) }),
        };
    }

    private string GetLocalizedString(string key, params object[] args)
    {
        ApplyRequestCulture();

        var localized = args.Length > 0 ? _localizer[key, args] : _localizer[key];
        if (!localized.ResourceNotFound)
        {
            return localized.Value;
        }

        return PasswordErrorResources.TryGet(key, ResolveLanguage(), args) ?? key;
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
}

public class PasswordValidationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ErrorCodes { get; set; } = new();
}

using System.Globalization;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PasswordErrorTranslatorTests
{
    [Theory]
    [InlineData("de", "PasswordRequiresUpper", "Das Passwort muss mindestens einen Großbuchstaben (A-Z) enthalten.")]
    [InlineData("en", "PasswordRequiresUpper", "Password must contain at least one uppercase letter (A-Z).")]
    [InlineData("tr", "PasswordRequiresNonAlphanumeric", "Şifre en az bir özel karakter içermelidir (ör. ! @ # $ %).")]
    public void TranslateError_Maps_Identity_Codes_To_User_Friendly_Text(string language, string code, string expected)
    {
        var translator = LocalizationTestDoubles.PasswordErrorTranslator(language);
        var text = translator.TranslateError(new IdentityError { Code = code });
        Assert.Equal(expected, text);
    }

    [Fact]
    public void GetValidationResponse_Joins_Multiple_Errors()
    {
        var translator = LocalizationTestDoubles.PasswordErrorTranslator("de");
        var result = IdentityResult.Failed(
            new IdentityError { Code = "PasswordRequiresUpper" },
            new IdentityError { Code = "PasswordRequiresNonAlphanumeric" });

        var validation = translator.GetValidationResponse(result);

        Assert.False(validation.Success);
        Assert.Equal(2, validation.ErrorCodes.Count);
        Assert.Contains("Großbuchstaben", validation.Message);
        Assert.Contains("Sonderzeichen", validation.Message);
    }

    [Fact]
    public void BuildPasswordValidationBadRequest_Includes_Structured_Errors()
    {
        var translator = LocalizationTestDoubles.PasswordErrorTranslator("en");
        var payload = translator.BuildPasswordValidationBadRequest(new[]
        {
            new IdentityError { Code = "PasswordRequiresDigit" },
        });

        dynamic body = payload;
        Assert.Equal("Password must contain at least one digit (0-9).", (string)body.message);
        Assert.Equal("PASSWORD_VALIDATION_FAILED", (string)body.code);
    }
}

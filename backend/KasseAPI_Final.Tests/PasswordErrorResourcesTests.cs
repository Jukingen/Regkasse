using KasseAPI_Final.Resources;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PasswordErrorResourcesTests
{
    [Fact]
    public void ErrorMessageResources_reads_embedded_resx()
    {
        Assert.Equal("Ungültiger Benutzername oder Passwort", ErrorMessageResources.TryGet("InvalidCredentials", "de"));
        Assert.Equal("Invalid username or password", ErrorMessageResources.TryGet("InvalidCredentials", "en"));
        Assert.Equal("Kullanıcı adı veya şifre geçersiz", ErrorMessageResources.TryGet("InvalidCredentials", "tr"));
        Assert.Equal("Ungültiges Passwort", ErrorMessageResources.TryGet("InvalidPassword", "de"));
        Assert.Equal("Invalid password", ErrorMessageResources.TryGet("InvalidPassword", "en"));
    }

    [Fact]
    public void PasswordErrorResources_reads_embedded_resx()
    {
        Assert.Equal(
            "Das Passwort muss mindestens einen Großbuchstaben (A-Z) enthalten.",
            PasswordErrorResources.TryGet("PasswordRequiresUpper", "de"));
        Assert.Equal(
            "Password must contain at least one uppercase letter (A-Z).",
            PasswordErrorResources.TryGet("PasswordRequiresUpper", "en"));
    }
}

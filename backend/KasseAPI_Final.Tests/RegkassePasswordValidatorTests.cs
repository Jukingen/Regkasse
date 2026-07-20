using KasseAPI_Final.Models;
using KasseAPI_Final.Validators;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RegkassePasswordValidatorTests
{
    [Theory]
    [InlineData("Short1!")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_too_short_or_blank(string password)
    {
        var result = await ValidateAsync(password);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordTooShort");
    }

    [Fact]
    public async Task Rejects_missing_complexity_parts()
    {
        var result = await ValidateAsync("alllowercase1!");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresUpper");
    }

    [Fact]
    public async Task Rejects_missing_special_character()
    {
        var result = await ValidateAsync("NoSpecial1");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresNonAlphanumeric");
    }

    [Fact]
    public async Task Accepts_compliant_password()
    {
        var result = await ValidateAsync("ValidPass1!");
        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    private static async Task<IdentityResult> ValidateAsync(string password)
    {
        var options = Options.Create(new IdentityOptions
        {
            Password =
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = true,
                RequiredUniqueChars = 1,
            }
        });
        var validator = new RegkassePasswordValidator<ApplicationUser>(
            new IdentityErrorDescriber(),
            options);
        var manager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!).Object;

        return await validator.ValidateAsync(manager, new ApplicationUser(), password);
    }
}

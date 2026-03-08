using KasseAPI_Final.Controllers;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Auth controller: deactivated user cannot authenticate (login returns 400).
/// </summary>
public class AuthControllerTests
{
    private static UserManager<ApplicationUser> CreateUserManagerForDeactivatedUser(ApplicationUser? userByEmail)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.As<IUserEmailStore<ApplicationUser>>()
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userByEmail);

        var options = Options.Create(new IdentityOptions());
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        return new UserManager<ApplicationUser>(
            store.Object, options, hasher.Object, userValidators, passwordValidators,
            keyNormalizer.Object, errors, services, logger);
    }

    [Fact]
    public async Task Login_WhenUserDeactivated_ReturnsBadRequest()
    {
        var deactivatedUser = new ApplicationUser
        {
            Id = "deact-1",
            UserName = "deact@test.com",
            Email = "deact@test.com",
            IsActive = false,
        };
        var userManager = CreateUserManagerForDeactivatedUser(deactivatedUser);
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c["JwtSettings:SecretKey"]).Returns("test-secret-key-at-least-32-characters-long");
        config.Setup(c => c["JwtSettings:Issuer"]).Returns("Test");
        config.Setup(c => c["JwtSettings:Audience"]).Returns("Test");
        var logger = new Mock<ILogger<AuthController>>().Object;
        var controller = new AuthController(userManager, config.Object, logger);

        var result = await controller.Login(new LoginModel { Email = "deact@test.com", Password = "any" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Login_WhenUserNotFound_ReturnsBadRequest()
    {
        var userManager = CreateUserManagerForDeactivatedUser(null);
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var logger = new Mock<ILogger<AuthController>>().Object;
        var controller = new AuthController(userManager, config.Object, logger);

        var result = await controller.Login(new LoginModel { Email = "nobody@test.com", Password = "any" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }
}

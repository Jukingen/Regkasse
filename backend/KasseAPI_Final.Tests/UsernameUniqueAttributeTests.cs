using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UsernameUniqueAttributeTests
{
    [Fact]
    public void UpdateUsernameRequest_Rejects_Case_Insensitive_Duplicate()
    {
        var (services, _) = BuildValidationServices(existingUserName: "Manager1", existingNormalized: "MANAGER1");
        var request = new UpdateUsernameRequest { NewUsername = "manager1" };

        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(
            request,
            new ValidationContext(request, services, null),
            results,
            validateAllProperties: true);

        Assert.False(ok);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateUsernameRequest.NewUsername)));
    }

    [Fact]
    public void UpdateUsernameRequest_Allows_Same_User_Casing_Change_Only()
    {
        var (services, userId) = BuildValidationServices(existingUserName: "Mustafa", existingNormalized: "MUSTAFA");
        var http = services.GetRequiredService<IHttpContextAccessor>();
        http.HttpContext = new DefaultHttpContext { RequestServices = services };
        http.HttpContext.Request.RouteValues = new RouteValueDictionary { ["id"] = userId };

        var request = new UpdateUsernameRequest { NewUsername = "mustafa" };

        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(
            request,
            new ValidationContext(request, services, null),
            results,
            validateAllProperties: true);

        Assert.True(ok);
        Assert.Empty(results);
    }

    private static (IServiceProvider Services, string ExistingUserId) BuildValidationServices(
        string existingUserName,
        string existingNormalized)
    {
        var userId = Guid.NewGuid().ToString("D");
        var db = CreateDb();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = existingUserName,
            NormalizedUserName = existingNormalized,
            Email = "user@test.com",
            NormalizedEmail = "USER@TEST.COM",
            IsActive = true,
        });
        db.SaveChanges();

        var userManager = CreateUserManager(db);
        var services = new ServiceCollection();
        services.AddSingleton(userManager);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        return (services.BuildServiceProvider(), userId);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"username-unique-attr-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }
}

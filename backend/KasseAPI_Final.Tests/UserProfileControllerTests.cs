using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class UserProfileControllerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"UserProfile_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationUser? existingUser = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        store.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);
        store.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var options = Options.Create(new IdentityOptions());
        return new UserManager<ApplicationUser>(
            store.Object,
            options,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);
    }

    private static IUserUniquenessValidationService CreateUniquenessValidationMock()
    {
        var mock = new Mock<IUserUniquenessValidationService>();
        mock.Setup(x => x.ValidateUniquenessForUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .ReturnsAsync((false, (string?)null));
        return mock.Object;
    }

    private static UserProfileController CreateController(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IAuditLogService auditLogService,
        string actorId = "self-id",
        string actorRole = "Manager")
    {
        var sessionInvalidation = new Mock<IUserSessionInvalidation>();
        sessionInvalidation
            .Setup(x => x.InvalidateSessionsForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var usernameHistory = new Mock<IUserUsernameHistoryService>();
        usernameHistory
            .Setup(x => x.RecordChangeAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var usernameEmail = new Mock<IUsernameChangeEmailService>();
        usernameEmail
            .Setup(x => x.TrySendUsernameChangedAsync(It.IsAny<UsernameChangedEmailRequest>()))
            .ReturnsAsync(true);

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.SetupGet(x => x.TenantId).Returns((Guid?)null);

        var controller = new UserProfileController(
            userManager,
            context,
            CreateUniquenessValidationMock(),
            auditLogService,
            sessionInvalidation.Object,
            usernameHistory.Object,
            usernameEmail.Object,
            tenantAccessor.Object,
            new Mock<ILogger<UserProfileController>>().Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, actorId),
            new(ClaimTypes.Role, actorRole),
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            RequestAborted = CancellationToken.None,
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Fact]
    public async Task GetProfile_WhenUserExists_ReturnsProfileDto()
    {
        var user = new ApplicationUser
        {
            Id = "self-id",
            UserName = "manager1",
            Email = "manager@example.com",
            FirstName = "Max",
            LastName = "Mustermann",
            EmployeeNumber = "E001",
            PhoneNumber = "+43 1 234567",
            Role = "Manager",
            IsActive = true,
        };
        var userManager = CreateUserManager(user);
        using var context = CreateContext();
        var controller = CreateController(userManager, context, new Mock<IAuditLogService>().Object);

        var result = await controller.GetProfile(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<UserProfileDto>(ok.Value);
        Assert.Equal("self-id", dto.Id);
        Assert.Equal("manager1", dto.UserName);
        Assert.Equal("manager@example.com", dto.Email);
        Assert.Equal("Max", dto.FirstName);
        Assert.Equal("Mustermann", dto.LastName);
        Assert.Equal("Manager", dto.Role);
        Assert.Equal("E001", dto.EmployeeNumber);
        Assert.Equal("+43 1 234567", dto.PhoneNumber);
    }

    [Fact]
    public async Task UpdateProfile_WhenValid_UpdatesUserAndReturnsOk()
    {
        var user = new ApplicationUser
        {
            Id = "self-id",
            UserName = "self",
            Email = "self@example.com",
            FirstName = "Old",
            LastName = "Name",
            EmployeeNumber = "E001",
            IsActive = true,
            Role = "Manager",
        };
        var userManager = CreateUserManager(user);
        var auditMock = new Mock<IAuditLogService>();
        using var context = CreateContext();
        var controller = CreateController(userManager, context, auditMock.Object);

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest
            {
                FirstName = "New",
                LastName = "Profile",
                Email = "new@example.com",
                PhoneNumber = "+43 1 234567",
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("Profile", user.LastName);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("+43 1 234567", user.PhoneNumber);
        Assert.Equal("self", user.UserName);
        auditMock.Verify(
            x => x.LogUserLifecycleAsync(
                AuditLogActions.USER_UPDATE,
                "self-id",
                "Manager",
                "self-id",
                null,
                null,
                AuditLogStatus.Success,
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()),
            Times.Once);
    }
}

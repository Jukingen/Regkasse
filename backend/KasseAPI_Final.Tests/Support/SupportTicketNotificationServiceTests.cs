using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Support;

public sealed class SupportTicketNotificationServiceTests
{
    [Fact]
    public async Task NotifyNewTicketAsync_PublishesActivityAndEmailsSuperAdmins()
    {
        var activity = new Mock<IActivityEventPublisher>();
        var email = CreateEmailMock();
        var userManager = CreateUserManager(
            superAdmins: [new ApplicationUser { Email = "ops@regkasse.at", EmailConfirmed = true }]);

        var sut = new SupportTicketNotificationService(
            activity.Object,
            email.Object,
            userManager.Object,
            Mock.Of<ILogger<SupportTicketNotificationService>>());

        var ticket = SampleTicket();
        await sut.NotifyNewTicketAsync(ticket);

        activity.Verify(
            a => a.TryPublishAsync(
                ticket.TenantId,
                ActivityEventType.SupportTicketCreated,
                It.IsAny<object?>(),
                ticket.CreatedByUserId,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        email.Verify(
            e => e.SendAsync(
                It.Is<IReadOnlyList<string>>(to => to.Contains("ops@regkasse.at")),
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<string>(s => s.Contains("New support ticket", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyStaffReplyAsync_EmailsTenantCreator()
    {
        var activity = new Mock<IActivityEventPublisher>();
        var email = CreateEmailMock();
        var userManager = CreateUserManager(
            creator: new ApplicationUser { Id = "user-1", Email = "manager@cafe.at", EmailConfirmed = true });

        var sut = new SupportTicketNotificationService(
            activity.Object,
            email.Object,
            userManager.Object,
            Mock.Of<ILogger<SupportTicketNotificationService>>());

        await sut.NotifyStaffReplyAsync(SampleTicket());

        activity.Verify(
            a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.SupportTicketStaffReplied,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        email.Verify(
            e => e.SendAsync(
                It.Is<IReadOnlyList<string>>(to => to.Contains("manager@cafe.at")),
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<string>(s => s.Contains("Support reply", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyClosedAsync_PublishesClosedEvent()
    {
        var activity = new Mock<IActivityEventPublisher>();
        var email = CreateEmailMock();
        var userManager = CreateUserManager(
            creator: new ApplicationUser { Id = "user-1", Email = "manager@cafe.at", EmailConfirmed = true });

        var sut = new SupportTicketNotificationService(
            activity.Object,
            email.Object,
            userManager.Object,
            Mock.Of<ILogger<SupportTicketNotificationService>>());

        await sut.NotifyClosedAsync(SampleTicket());

        activity.Verify(
            a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.SupportTicketClosed,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IDataDeletionNotificationSender> CreateEmailMock()
    {
        var email = new Mock<IDataDeletionNotificationSender>();
        email
            .Setup(e => e.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return email;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(
        ApplicationUser? creator = null,
        IList<ApplicationUser>? superAdmins = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(creator);
        mgr.Setup(m => m.GetUsersInRoleAsync(Roles.SuperAdmin))
            .ReturnsAsync(superAdmins ?? Array.Empty<ApplicationUser>());
        return mgr;
    }

    private static SupportTicket SampleTicket() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        TicketNumber = "SUP-2026-0001",
        Title = "Invoice PDF missing",
        Message = "Cannot download PDF.",
        Category = SupportTicketCategories.Billing,
        Priority = SupportTicketPriorities.High,
        Status = SupportTicketStatuses.Open,
        CreatedByUserId = "user-1",
    };
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseFailoverNotificationServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static TseDevice Primary(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        SerialNumber = "PRI-SERIAL-001",
        DeviceId = "primary-1",
        DeviceType = "Soft",
        VendorId = "auto",
        ProductId = "soft",
        TenantId = TenantId,
        CashRegisterId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        HealthStatus = TseHealthStatus.Offline,
        HealthMessage = "Not connected",
        HealthScore = 0,
        CertificateStatus = "VALID",
        MemoryStatus = "OK",
        FinanzOnlineUsername = string.Empty,
        IsActive = true,
        IsPrimary = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static TseDevice Backup(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        SerialNumber = "BKP-SERIAL-001",
        DeviceId = "backup-1",
        DeviceType = "Soft",
        VendorId = "auto",
        ProductId = "soft",
        TenantId = TenantId,
        HealthStatus = TseHealthStatus.Healthy,
        HealthScore = 95,
        CertificateStatus = "VALID",
        MemoryStatus = "OK",
        FinanzOnlineUsername = string.Empty,
        IsActive = true,
        IsBackup = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static (
        TseFailoverNotificationService Svc,
        Mock<IActivityEventPublisher> Activity,
        Mock<IDataDeletionNotificationSender> Email) CreateService(
        IReadOnlyList<ApplicationUser>? superAdmins = null,
        string? failoverAlertEmails = null)
    {
        var activity = new Mock<IActivityEventPublisher>();
        activity
            .Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var email = new Mock<IDataDeletionNotificationSender>();
        email
            .Setup(e => e.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager
            .Setup(m => m.GetUsersInRoleAsync(Roles.SuperAdmin))
            .ReturnsAsync(superAdmins?.ToList() ?? new List<ApplicationUser>());

        var opts = Options.Create(new TseOptions { FailoverAlertEmails = failoverAlertEmails }).ToMonitor();

        var svc = new TseFailoverNotificationService(
            activity.Object,
            email.Object,
            userManager.Object,
            opts,
            NullLogger<TseFailoverNotificationService>.Instance);

        return (svc, activity, email);
    }

    [Fact]
    public async Task NotifyFailoverStarted_PublishesActivity_AndEmailsSuperAdmin()
    {
        var (svc, activity, email) = CreateService(
            superAdmins:
            [
                new ApplicationUser
                {
                    Id = "sa-1",
                    UserName = "super",
                    Email = "super@example.com",
                    EmailConfirmed = true,
                    Role = Roles.SuperAdmin,
                },
            ]);

        var primary = Primary();
        var backup = Backup();
        await svc.NotifyFailoverStartedAsync(primary, backup);

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.TseFailoverStarted,
                It.IsAny<object?>(),
                TseFailoverService.SystemActorUserId,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        email.Verify(
            e => e.SendAsync(
                It.Is<IReadOnlyList<string>>(to => to.Contains("super@example.com")),
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<string>(s => s.Contains("TSE Failover Started", StringComparison.Ordinal)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyNoBackupAvailable_IncludesConfiguredFailoverAlertEmails()
    {
        var (svc, activity, email) = CreateService(
            superAdmins: Array.Empty<ApplicationUser>(),
            failoverAlertEmails: "ops@regkasse.at; other@example.com");

        await svc.NotifyNoBackupAvailableAsync(Primary(), "Primary offline");

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.TseFailoverNoBackup,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        email.Verify(
            e => e.SendAsync(
                It.Is<IReadOnlyList<string>>(to =>
                    to.Contains("ops@regkasse.at") && to.Contains("other@example.com")),
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<string>(s => s.Contains("No TSE Backup", StringComparison.Ordinal)),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyFailoverReverted_PublishesActivity_WithoutEmail()
    {
        var (svc, activity, email) = CreateService(
            superAdmins:
            [
                new ApplicationUser
                {
                    Id = "sa-1",
                    Email = "super@example.com",
                    EmailConfirmed = true,
                    Role = Roles.SuperAdmin,
                },
            ]);

        await svc.NotifyFailoverRevertedAsync(Primary());

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.TseFailoverReverted,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        email.Verify(
            e => e.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyFailoverCompleted_PublishesActivatedEvent()
    {
        var (svc, activity, _) = CreateService(failoverAlertEmails: "alert@example.com");

        await svc.NotifyFailoverCompletedAsync(Primary(), Backup(), TseFailoverTypes.Automatic);

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.TseFailoverActivated,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

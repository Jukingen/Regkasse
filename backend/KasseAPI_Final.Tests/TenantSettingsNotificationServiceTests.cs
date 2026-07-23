using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.TenantSettings;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantSettingsNotificationServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantSettingsNotify_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(null));
    }

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    [Fact]
    public async Task Approved_PublishesActivity_AndEmailsManagers()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Notify Tenant",
            Slug = "notify-tenant",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "mgr-1",
            UserName = "manager1",
            Email = "manager@example.com",
            Role = Authorization.Roles.Manager,
            EmailConfirmed = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = "mgr-1",
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IReadOnlyList<string>? emailedTo = null;
        var email = new Mock<IDataDeletionNotificationSender>();
        email.Setup(e => e.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, IReadOnlyList<string>, string, string, CancellationToken>(
                (to, _, _, _, _) => emailedTo = to)
            .Returns(Task.CompletedTask);

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var svc = new TenantSettingsNotificationService(
            db,
            activity.Object,
            email.Object,
            userManager.Object,
            NullLogger<TenantSettingsNotificationService>.Instance);

        var changeId = Guid.NewGuid();
        await svc.NotifySettingsChangeAsync(
            TenantId,
            changeId,
            ActivityEventType.TenantSettingsChangeApproved,
            "currency",
            "EUR",
            "USD",
            "super-2",
            "Approved after review");

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.TenantSettingsChangeApproved,
                It.IsAny<object?>(),
                "super-2",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(emailedTo);
        Assert.Contains("manager@example.com", emailedTo!);
    }

    [Fact]
    public async Task Rejected_PublishesActivity_WithoutEmail()
    {
        await using var db = CreateDb();

        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var email = new Mock<IDataDeletionNotificationSender>();
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var svc = new TenantSettingsNotificationService(
            db,
            activity.Object,
            email.Object,
            userManager.Object,
            NullLogger<TenantSettingsNotificationService>.Instance);

        await svc.NotifySettingsChangeAsync(
            TenantId,
            Guid.NewGuid(),
            ActivityEventType.TenantSettingsChangeRejected,
            "timezone",
            "Europe/Vienna",
            "Europe/Berlin",
            "super-2",
            "Not needed");

        activity.Verify(
            a => a.TryPublishAsync(
                TenantId,
                ActivityEventType.TenantSettingsChangeRejected,
                It.IsAny<object?>(),
                "super-2",
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
}

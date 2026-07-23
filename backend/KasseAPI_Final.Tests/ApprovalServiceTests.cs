using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.CriticalActions;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ApprovalServiceTests
{
    [Fact]
    public async Task RequestApproval_PersistsPending_AndNotifies()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo GmbH",
            Slug = "demo",
            Status = TenantStatuses.Active,
        });
        await db.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        var email = new Mock<IDataDeletionNotificationSender>();
        email.Setup(e => e.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync("requester-1"))
            .ReturnsAsync(new ApplicationUser { Id = "requester-1", Email = "req@test.com", UserName = "req" });
        userManager.Setup(m => m.GetUsersInRoleAsync(Roles.SuperAdmin))
            .ReturnsAsync(
            [
                new ApplicationUser { Id = "sa-1", Email = "sa@test.com", EmailConfirmed = true },
            ]);

        var svc = CreateService(db, activity.Object, email.Object, userManager.Object);
        var result = await svc.RequestApprovalAsync(
            "requester-1",
            new CreateApprovalRequestDto
            {
                ActionType = CriticalActionType.TenantDeletion,
                TenantId = tenantId,
                PathHint = "/api/admin/tenants/x/permanent",
                Reason = "Cleanup",
                Payload = "{\"confirm\":true}",
            });

        Assert.True(result.Ok);
        Assert.NotNull(result.Dto);
        Assert.Equal(ApprovalRequestStatuses.Pending, result.Dto!.Status);

        var row = await db.ApprovalRequests.SingleAsync();
        Assert.Equal(CriticalActionType.TenantDeletion.ToString(), row.ActionType);
        Assert.Equal(tenantId, row.TenantId);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.CriticalActionApprovalRequested,
                It.IsAny<object?>(),
                "requester-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        email.Verify(
            e => e.SendAsync(
                It.Is<IReadOnlyList<string>>(to => to.Contains("sa@test.com")),
                It.IsAny<IReadOnlyList<string>>(),
                It.Is<string>(s => s.Contains("TenantDeletion")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Approve_IssuesClaimableToken()
    {
        await using var db = CreateDb();
        var requestId = Guid.NewGuid();
        db.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = requestId,
            RequestedBy = "requester-1",
            ActionType = CriticalActionType.DeleteAllProducts.ToString(),
            Status = ApprovalRequestStatuses.Pending,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            PathHint = "/api/admin/products/deactivate-all",
        });
        await db.SaveChangesAsync();

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync("sa-1"))
            .ReturnsAsync(new ApplicationUser { Id = "sa-1", Email = "sa@test.com" });
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([Roles.SuperAdmin]);
        userManager.Setup(m => m.FindByIdAsync("requester-1"))
            .ReturnsAsync(new ApplicationUser { Id = "requester-1", Email = "req@test.com" });

        var svc = CreateService(db, userManager: userManager.Object);
        var approved = await svc.ApproveAsync(requestId, "sa-1", "ok");
        Assert.True(approved.Ok);
        Assert.False(string.IsNullOrWhiteSpace(approved.ApprovalToken));

        var claimed = await svc.ClaimTokenAsync(requestId, "requester-1");
        Assert.True(claimed.Ok);
        Assert.Equal(approved.ApprovalToken, claimed.ApprovalToken);

        var secondClaim = await svc.ClaimTokenAsync(requestId, "requester-1");
        Assert.False(secondClaim.Ok);
    }

    [Fact]
    public async Task ListHistory_And_Report_IncludeResolvedRequests_WithDecisionTime()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "History GmbH",
            Slug = "history",
            Status = TenantStatuses.Active,
        });
        var requestedAt = DateTime.UtcNow.AddHours(-2);
        var approvedAt = requestedAt.AddMinutes(45);
        db.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestedBy = "requester-1",
            ApprovedBy = "sa-1",
            ActionType = CriticalActionType.TenantDeletion.ToString(),
            Status = ApprovalRequestStatuses.Approved,
            RequestedAt = requestedAt,
            ApprovedAt = approvedAt,
            ExpiresAt = requestedAt.AddHours(24),
            Reason = "Done",
        });
        db.ApprovalRequests.Add(new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestedBy = "requester-2",
            ActionType = CriticalActionType.DeleteAllProducts.ToString(),
            Status = ApprovalRequestStatuses.Rejected,
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            ApprovedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(10),
            ApprovedBy = "sa-1",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        });
        await db.SaveChangesAsync();

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync("requester-1"))
            .ReturnsAsync(new ApplicationUser { Id = "requester-1", Email = "req@test.com" });
        userManager.Setup(m => m.FindByIdAsync("requester-2"))
            .ReturnsAsync(new ApplicationUser { Id = "requester-2", Email = "req2@test.com" });
        userManager.Setup(m => m.FindByIdAsync("sa-1"))
            .ReturnsAsync(new ApplicationUser { Id = "sa-1", Email = "sa@test.com" });

        var svc = CreateService(db, userManager: userManager.Object);
        var history = await svc.ListHistoryAsync(new ApprovalHistoryQuery { TenantId = tenantId, Limit = 50 });
        Assert.Equal(2, history.Count);
        var approved = Assert.Single(history, h => h.Status == ApprovalRequestStatuses.Approved);
        Assert.Equal(45, approved.TimeToDecisionMinutes);
        Assert.Equal("sa@test.com", approved.ApprovedByEmail);

        var report = await svc.GetHistoryReportAsync(tenantId: tenantId);
        Assert.Equal(2, report.TotalRequests);
        Assert.Equal(1, report.ApprovedCount);
        Assert.Equal(1, report.RejectedCount);
        Assert.NotNull(report.AverageTimeToApprovalMinutes);
        Assert.Contains(report.ByActionType, a => a.ActionType == CriticalActionType.TenantDeletion.ToString());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ApprovalService_{Guid.NewGuid():N}")
            .Options;
        var tenantAccessor = Mock.Of<ICurrentTenantAccessor>();
        return new AppDbContext(options, tenantAccessor);
    }

    private static ApprovalService CreateService(
        AppDbContext db,
        IActivityEventPublisher? activity = null,
        IDataDeletionNotificationSender? email = null,
        UserManager<ApplicationUser>? userManager = null)
    {
        if (userManager is null)
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            userManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!).Object;
        }

        var options = Options.Create(new CriticalActionOptions
        {
            ApprovalTokenTtlMinutes = 5,
            SuperAdminMaySelfApprove = true,
        });
        var monitor = Mock.Of<IOptionsMonitor<CriticalActionOptions>>(m => m.CurrentValue == options.Value);

        return new ApprovalService(
            db,
            userManager,
            activity ?? Mock.Of<IActivityEventPublisher>(),
            email ?? Mock.Of<IDataDeletionNotificationSender>(),
            new MemoryCache(new MemoryCacheOptions()),
            monitor,
            NullLogger<ApprovalService>.Instance);
    }
}

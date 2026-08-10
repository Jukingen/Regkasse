using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Communication;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class BulkEmailServiceTests
{
    private static (BulkEmailService Sut, AppDbContext Db, Mock<IEmailService> Email) CreateSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
        var email = new Mock<IEmailService>();
        email.Setup(e => e.TrySendHtmlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var limiter = new BulkEmailRateLimiter(new MemoryCache(new MemoryCacheOptions()));
        var sut = new BulkEmailService(db, email.Object, limiter, NullLogger<BulkEmailService>.Instance);
        return (sut, db, email);
    }

    [Fact]
    public async Task SendBulkAsync_sends_to_manager_emails_and_logs()
    {
        var (sut, db, email) = CreateSut(nameof(SendBulkAsync_sends_to_manager_emails_and_logs));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T1",
            Slug = "t1",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("D"),
            UserName = "mgr@t1.test",
            Email = "mgr@t1.test",
            NormalizedEmail = "MGR@T1.TEST",
            Role = Roles.Manager,
            IsActive = true,
            FirstName = "M",
            LastName = "G",
        };
        db.Users.Add(user);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await sut.SendBulkAsync(new BulkEmailRequest
        {
            TenantIds = [tenantId],
            Subject = "Hello",
            Body = "<p>Hi</p>",
        });

        Assert.Equal(1, result.TotalAttempted);
        Assert.Equal(1, result.TotalSent);
        Assert.Equal(0, result.TotalFailed);
        Assert.Equal(1, await db.CommunicationLogs.CountAsync());
        email.Verify(e => e.TrySendHtmlAsync("mgr@t1.test", "Hello", "<p>Hi</p>", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendBulkAsync_records_failed_when_smtp_fails()
    {
        var (sut, db, email) = CreateSut(nameof(SendBulkAsync_records_failed_when_smtp_fails));
        email.Setup(e => e.TrySendHtmlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T2",
            Slug = "t2",
            Email = "fallback@t2.test",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await sut.SendBulkAsync(new BulkEmailRequest
        {
            TenantIds = [tenantId],
            Subject = "X",
            Body = "Y",
        });

        Assert.Equal(1, result.TotalFailed);
        Assert.Contains("fallback@t2.test", result.FailedEmails);
        Assert.Equal(CommunicationLogStatuses.Failed, (await db.CommunicationLogs.SingleAsync()).Status);
    }

    [Fact]
    public void RateLimiter_blocks_over_100_per_minute()
    {
        var limiter = new BulkEmailRateLimiter(new MemoryCache(new MemoryCacheOptions()));
        Assert.Null(limiter.TryAcquireOrError(100));
        Assert.NotNull(limiter.TryAcquireOrError(1));
    }
}

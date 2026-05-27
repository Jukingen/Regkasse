using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserActivityReportServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UserActivityReport_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserActivityReportService CreateService(AppDbContext db) =>
        new(db, NullLogger<UserActivityReportService>.Instance);

    [Fact]
    public async Task BuildReportAsync_AggregatesLoginSessionsAndActions()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Cafe Demo", Slug = "cafe", Status = TenantStatuses.Active });
        var user = new ApplicationUser
        {
            Id = "u-report",
            UserName = "cashier1",
            Email = "c@demo.at",
            FirstName = "Max",
            LastName = "Muster",
            Role = "Cashier",
            LoginCount = 5,
            LastLoginAt = DateTime.UtcNow.AddDays(-1),
        };
        db.Users.Add(user);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenantId,
            IsActive = true,
        });

        var now = DateTime.UtcNow;
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                UserRole = "Cashier",
                Action = AuditLogActions.USER_LOGIN,
                EntityType = "User",
                Status = AuditLogStatus.Success,
                Timestamp = now.AddDays(-1),
                IpAddress = "10.0.0.1",
                SessionId = "s1",
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                UserRole = "Cashier",
                Action = AuditLogActions.USER_LOGIN,
                EntityType = "User",
                Status = AuditLogStatus.Failed,
                Timestamp = now.AddDays(-2),
                SessionId = "s2",
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                UserRole = "Cashier",
                Action = "PaymentCreated",
                EntityType = "Payment",
                Status = AuditLogStatus.Success,
                Timestamp = now.AddHours(-3),
                SessionId = "s3",
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.BuildReportAsync(
            new UserActivityReportQuery
            {
                UserId = user.Id,
                FromDate = now.AddDays(-30),
                ToDate = now.AddDays(1),
                DefaultRangeDays = 30,
            },
            actorIsSuperAdmin: true,
            ambientTenantId: null);

        Assert.NotNull(report);
        Assert.Equal(4, report!.TotalActions);
        Assert.Equal(1, report.FailedLoginAttempts);
        Assert.Equal(1, report.ActionsPerformed.PaymentsProcessed);
        Assert.True(report.DailyActivity.Count >= 1);
    }

    [Fact]
    public async Task BuildReportAsync_TenantAdminCannotAccessUserOutsideTenant()
    {
        await using var db = CreateContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "A", Slug = "a", Status = TenantStatuses.Active },
            new Tenant { Id = tenantB, Name = "B", Slug = "b", Status = TenantStatuses.Active });
        var user = new ApplicationUser
        {
            Id = "u-b-only",
            UserName = "buser",
            Email = "b@x.at",
            FirstName = "B",
            LastName = "User",
            Role = "Cashier",
        };
        db.Users.Add(user);
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = user.Id,
            TenantId = tenantB,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.BuildReportAsync(
            new UserActivityReportQuery { UserId = user.Id },
            actorIsSuperAdmin: false,
            ambientTenantId: tenantA);

        Assert.Null(report);
    }

    [Fact]
    public async Task ExportAsync_ReturnsCsv()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", Status = TenantStatuses.Active });
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u@t.at",
            FirstName = "U",
            LastName = "One",
            Role = "Cashier",
        };
        db.Users.Add(user);
        db.UserTenantMemberships.Add(new UserTenantMembership { UserId = user.Id, TenantId = tenantId, IsActive = true });
        var now = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            UserRole = "Cashier",
            Action = "PaymentCreated",
            EntityType = "Payment",
            Status = AuditLogStatus.Success,
            Timestamp = now,
            SessionId = "s",
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (bytes, contentType, _) = await service.ExportAsync(
            new UserActivityReportQuery { UserId = user.Id, FromDate = now.AddDays(-1), ToDate = now.AddDays(1) },
            "csv",
            true,
            null);

        Assert.Equal("text/csv", contentType);
        Assert.Contains("TimestampUtc", System.Text.Encoding.UTF8.GetString(bytes));
    }
}

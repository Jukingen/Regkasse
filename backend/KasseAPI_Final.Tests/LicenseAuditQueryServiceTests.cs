using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseAuditQueryServiceTests
{
    [Fact]
    public async Task ListAsync_FiltersByTenantAndClampsPageSize()
    {
        await using var db = CreateDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantA,
                Name = "Alpha",
                Slug = "alpha",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = tenantB,
                Name = "Beta",
                Slug = "beta",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        db.BillingAuditLogs.AddRange(
            new BillingAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                UserId = Guid.Empty,
                Action = BillingAuditEventTypes.LicenseActivated,
                Details = """{"invoiceNumber":"RE1"}""",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-2),
            },
            new BillingAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB,
                UserId = Guid.Empty,
                Action = BillingAuditEventTypes.SaleCreated,
                Details = """{"invoiceNumber":"RE2"}""",
                TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            SessionId = "s1",
            UserId = "user-1",
            UserRole = "SuperAdmin",
            Action = AuditLogActions.LICENSE_RENEWED,
            ActionType = AuditEventType.LicenseRenewed,
            Description = "License renewed.",
            Timestamp = DateTime.UtcNow,
            Status = AuditLogStatus.Success,
        });
        await db.SaveChangesAsync();

        var sut = new LicenseAuditQueryService(
            db,
            Options.Create(new LicenseOptions { GracePeriodDays = 7 }));

        var page = await sut.ListAsync(new LicenseAuditLogQuery(Page: 1, PageSize: 500, TenantId: tenantA));

        Assert.Equal(100, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, i => Assert.Equal(tenantA, i.TenantId));
        Assert.Contains(page.Items, i => i.Action == "LICENSE_ACTIVATED");
        Assert.Contains(page.Items, i => i.Action == "LICENSE_RENEWED");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicAudit_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }
}

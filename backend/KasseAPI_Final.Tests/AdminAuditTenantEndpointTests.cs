using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AdminAuditTenantEndpointTests
{
    private static async Task<(AdminAuditController Ctrl, AppDbContext Db, Guid TenantId)> CreateAsync(string dbName)
    {
        var tenantId = Guid.NewGuid();
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options, tenantAccessor);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Audit Co",
            Slug = "audit-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var otherTenant = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = otherTenant,
            Name = "Other",
            Slug = "other-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var now = DateTime.UtcNow;
        db.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = "s1",
                UserId = "user-a",
                UserRole = Roles.Manager,
                Action = "USER_CREATED",
                EntityType = "User",
                Status = AuditLogStatus.Success,
                Timestamp = now.AddMinutes(-10),
                Description = "Created user A",
                CreatedAt = now,
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = "s2",
                UserId = "user-b",
                UserRole = Roles.Cashier,
                Action = "PAYMENT_CONFIRM",
                EntityType = "Payment",
                Status = AuditLogStatus.Success,
                Timestamp = now.AddMinutes(-5),
                Description = "Payment ok",
                CreatedAt = now,
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenant,
                SessionId = "s3",
                UserId = "user-x",
                UserRole = Roles.Manager,
                Action = "USER_CREATED",
                EntityType = "User",
                Status = AuditLogStatus.Success,
                Timestamp = now,
                Description = "Other tenant",
                CreatedAt = now,
            });
        await db.SaveChangesAsync();

        var resolver = new Mock<IActorDisplayNameResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<IList<string>>()))
            .ReturnsAsync(new Dictionary<string, string> { ["user-a"] = "Alice", ["user-b"] = "Bob" });

        var ctrl = new AdminAuditController(
            Mock.Of<IAuditExportService>(),
            Mock.Of<IAuditExportJobManager>(),
            Mock.Of<IAuditReportScheduler>(),
            tenantAccessor,
            new FileNamingService(tenantAccessor),
            db,
            Options.Create(new AuditRetentionOptions()),
            NullLogger<AdminAuditController>.Instance,
            Mock.Of<IDownloadSecurityService>(),
            resolver.Object);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, Roles.SuperAdmin), new Claim(ClaimTypes.NameIdentifier, "sa")],
                    "Test")),
            },
        };

        return (ctrl, db, tenantId);
    }

    [Fact]
    public async Task GetTenantAuditLogs_filters_by_tenant_and_paginates()
    {
        var (ctrl, _, tenantId) = await CreateAsync(nameof(GetTenantAuditLogs_filters_by_tenant_and_paginates));

        var result = await ctrl.GetTenantAuditLogs(tenantId, page: 1, pageSize: 1);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TenantAuditLogsResponse>(ok.Value);
        Assert.Equal(2, body.TotalCount);
        Assert.Single(body.Items);
        Assert.Equal(tenantId, body.TenantId);
        Assert.DoesNotContain(body.Items, i => i.Details == "Other tenant");
    }

    [Fact]
    public async Task GetTenantAuditLogs_filters_by_action_and_user()
    {
        var (ctrl, _, tenantId) = await CreateAsync(nameof(GetTenantAuditLogs_filters_by_action_and_user));

        var result = await ctrl.GetTenantAuditLogs(tenantId, action: "USER_CREATED", userId: "user-a");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<TenantAuditLogsResponse>(ok.Value);
        Assert.Equal(1, body.TotalCount);
        Assert.Equal("USER_CREATED", body.Items[0].Action);
        Assert.Equal("Alice", body.Items[0].UserDisplayName);
    }

    [Fact]
    public async Task GetTenantAuditLogs_unknown_tenant_returns_404()
    {
        var (ctrl, _, _) = await CreateAsync(nameof(GetTenantAuditLogs_unknown_tenant_returns_404));
        var result = await ctrl.GetTenantAuditLogs(Guid.NewGuid());
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}

using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTseFailoverControllerTests
{
    [Fact]
    public async Task GetStatus_ReturnsActiveFailoverPairing()
    {
        await using var db = CreateDb();
        var (primaryId, backupId) = await SeedPairAsync(db);

        var controller = CreateController(db);
        var result = await controller.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TseFailoverStatusDto>(ok.Value);
        Assert.Equal(1, dto.ActiveFailoverCount);
        Assert.Contains(dto.ActiveFailovers, f => f.PrimaryDeviceId == primaryId && f.BackupDeviceId == backupId);
    }

    [Fact]
    public async Task GetHistory_ReturnsNewestFirst()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "H",
            Slug = "h",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        db.TseFailoverLogs.Add(new TseFailoverLog
        {
            Id = older,
            TenantId = tenantId,
            PrimaryDeviceId = Guid.NewGuid(),
            FailoverType = TseFailoverTypes.Automatic,
            TriggerReason = TseFailoverTriggerReasons.HealthCheckFailed,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            IsSuccessful = true,
        });
        db.TseFailoverLogs.Add(new TseFailoverLog
        {
            Id = newer,
            TenantId = tenantId,
            PrimaryDeviceId = Guid.NewGuid(),
            FailoverType = TseFailoverTypes.Manual,
            TriggerReason = TseFailoverTriggerReasons.ManualOverride,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            IsSuccessful = false,
            ErrorMessage = "x",
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetHistory(10, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TseFailoverHistoryItemDto>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Equal(newer, items[0].Id);
        Assert.Equal(older, items[1].Id);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin_tse_failover_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static AdminTseFailoverController CreateController(AppDbContext db)
    {
        var failover = new Mock<ITseFailoverService>();
        var controller = new AdminTseFailoverController(
            db,
            failover.Object,
            Mock.Of<ITseHealthTrendService>(),
            Mock.Of<ITsePerformanceService>(),
            Options.Create(new TseOptions { AutoFailoverEnabled = true }).ToMonitor());

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "sa-1"), new Claim(ClaimTypes.Role, Roles.SuperAdmin)],
            authenticationType: "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
        return controller;
    }

    private static async Task<(Guid PrimaryId, Guid BackupId)> SeedPairAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Failover Tenant",
            Slug = "failover-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var primary = new TseDevice
        {
            SerialNumber = $"P-{Guid.NewGuid():N}"[..16],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            KassenId = Guid.NewGuid(),
            IsPrimary = true,
            IsBackup = false,
            IsActive = true,
            IsFailoverActive = false,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(primary);
        await db.SaveChangesAsync();

        var backup = new TseDevice
        {
            SerialNumber = $"B-{Guid.NewGuid():N}"[..16],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            KassenId = primary.KassenId,
            PrimaryDeviceId = primary.Id,
            IsPrimary = false,
            IsBackup = true,
            IsActive = true,
            IsFailoverActive = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(backup);
        await db.SaveChangesAsync();
        return (primary.Id, backup.Id);
    }
}

using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.GracePeriods;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class GracePeriodServiceTests
{
    [Fact]
    public async Task Schedule_Then_Cancel_WithinWindow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo",
            Status = TenantStatuses.Active,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, TimeSpan.FromMinutes(30));
        var scheduled = await svc.ScheduleAsync(
            tenantId,
            "user-1",
            new ScheduleGracePeriodRequest
            {
                ActionKind = GracePeriodActionKinds.Schlussbeleg,
                EntityType = "CashRegister",
                EntityId = Guid.NewGuid().ToString("D"),
                Reason = "test",
            });

        Assert.True(scheduled.Success);
        Assert.NotNull(scheduled.Pending);
        Assert.True(scheduled.Pending!.CanCancel);

        var cancelled = await svc.CancelAsync(tenantId, scheduled.Pending.Id, "user-1", "changed mind");
        Assert.True(cancelled.Success);
        Assert.Equal(GracePeriodStatuses.Cancelled, cancelled.Pending!.Status);
    }

    [Fact]
    public void IsWithinUndoWindow_RespectsDuration()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDb(tenantId);
        var svc = CreateService(db, TimeSpan.FromMinutes(15), GracePeriodActionKinds.PriceUpdate);

        Assert.True(svc.IsWithinUndoWindow(GracePeriodActionKinds.PriceUpdate, DateTime.UtcNow.AddMinutes(-5)));
        Assert.False(svc.IsWithinUndoWindow(GracePeriodActionKinds.PriceUpdate, DateTime.UtcNow.AddMinutes(-20)));
    }

    [Fact]
    public void GetConfig_ReturnsRules()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDb(tenantId);
        var svc = CreateService(db, TimeSpan.FromMinutes(30));
        var config = svc.GetConfig();
        Assert.Contains(config.Rules, r => r.ActionKind == GracePeriodActionKinds.Schlussbeleg);
        Assert.Equal(1800, config.Rules.First(r => r.ActionKind == GracePeriodActionKinds.Schlussbeleg).DurationSeconds);
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"grace-{Guid.NewGuid():N}")
            .Options;
        var tenantAccessor = Mock.Of<ICurrentTenantAccessor>(a => a.TenantId == tenantId);
        return new AppDbContext(options, tenantAccessor);
    }

    private static GracePeriodService CreateService(
        AppDbContext db,
        TimeSpan schlussDuration,
        string? focusKind = null)
    {
        var opts = new GracePeriodsOptions
        {
            Enabled = true,
            Schlussbeleg = new GracePeriodRuleOptions
            {
                Duration = schlussDuration,
                RequiresApproval = true,
            },
            PriceUpdate = new GracePeriodRuleOptions
            {
                Duration = focusKind == GracePeriodActionKinds.PriceUpdate
                    ? schlussDuration
                    : TimeSpan.FromMinutes(15),
                RequiresApproval = false,
            },
        };
        var monitor = Mock.Of<IOptionsMonitor<GracePeriodsOptions>>(m => m.CurrentValue == opts);
        var scopeFactory = Mock.Of<IServiceScopeFactory>();
        return new GracePeriodService(
            db,
            monitor,
            scopeFactory,
            NullLogger<GracePeriodService>.Instance);
    }
}

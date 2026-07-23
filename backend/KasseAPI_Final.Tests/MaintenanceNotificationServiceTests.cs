using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Maintenance;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MaintenanceNotificationServiceTests
{
    private static MaintenanceNotificationService CreateService(out AppDbContext db)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Maintenance_{Guid.NewGuid():N}")
            .Options;
        db = new AppDbContext(options, new FixedTenantAccessor(null));
        return new MaintenanceNotificationService(db);
    }

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static CreateMaintenanceNotificationRequestDto BaseCreate(
        Action<CreateMaintenanceNotificationRequestDto>? configure = null)
    {
        var now = DateTime.UtcNow;
        var dto = new CreateMaintenanceNotificationRequestDto
        {
            Title = "Planned downtime",
            Message = "API and POS will be unavailable for upgrades.",
            ScheduledStartAt = now.AddHours(48),
            ScheduledEndAt = now.AddHours(50),
            Priority = 4,
            AffectedSystems = "All",
            PublishImmediately = true,
        };
        configure?.Invoke(dto);
        return dto;
    }

    [Fact]
    public async Task Create_PublishImmediately_IsVisibleToClients()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync("admin-1", BaseCreate());

        Assert.Equal(MaintenanceNotificationStatuses.Published, created.Status);
        Assert.NotNull(created.PublishedAt);

        var active = await service.GetActiveForUserAsync("cashier-1", MaintenanceAffectedSystems.Pos);
        Assert.Single(active);
        Assert.Equal(created.Id, active[0].Id);
        Assert.False(active[0].EffectiveForceDisplay);
        Assert.True(active[0].CanDismiss);
    }

    [Fact]
    public async Task GetActive_FiltersByAffectedSystems()
    {
        var service = CreateService(out _);
        await service.CreateAsync("admin-1", BaseCreate(c =>
        {
            c.AffectedSystems = "FA";
            c.Title = "FA only";
        }));

        var forPos = await service.GetActiveForUserAsync("u1", MaintenanceAffectedSystems.Pos);
        var forFa = await service.GetActiveForUserAsync("u1", MaintenanceAffectedSystems.Fa);

        Assert.Empty(forPos);
        Assert.Single(forFa);
    }

    [Fact]
    public async Task Acknowledge_Dismiss_HidesUntilForceWindow()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync("admin-1", BaseCreate());

        await service.AcknowledgeAsync(
            created.Id,
            "user-1",
            new AcknowledgeMaintenanceNotificationRequestDto { Dismiss = true, MarkRead = true });

        var active = await service.GetActiveForUserAsync("user-1", MaintenanceAffectedSystems.Fa);
        Assert.Empty(active);
    }

    [Fact]
    public async Task ForceDisplay_Within24Hours_CannotDismiss()
    {
        var service = CreateService(out _);
        var now = DateTime.UtcNow;
        var created = await service.CreateAsync("admin-1", BaseCreate(c =>
        {
            c.ScheduledStartAt = now.AddHours(6);
            c.ScheduledEndAt = now.AddHours(8);
            c.IsForceDisplay = false;
            c.IsMandatory = false;
            c.ForceDisplayFrom = now.AddHours(-1);
        }));

        Assert.True(created.EffectiveForceDisplay);
        Assert.False(created.CanDismiss);

        var afterAck = await service.AcknowledgeAsync(
            created.Id,
            "user-1",
            new AcknowledgeMaintenanceNotificationRequestDto { Dismiss = true, MarkRead = true });

        Assert.NotNull(afterAck);
        Assert.False(afterAck!.IsDismissedByCurrentUser);
        Assert.True(afterAck.IsReadByCurrentUser);

        var active = await service.GetActiveForUserAsync("user-1", MaintenanceAffectedSystems.Pos);
        Assert.Single(active);
        Assert.True(active[0].EffectiveForceDisplay);
    }

    [Fact]
    public async Task Mandatory_AlwaysForceDisplay()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync("admin-1", BaseCreate(c =>
        {
            c.IsMandatory = true;
            c.ScheduledStartAt = DateTime.UtcNow.AddDays(5);
            c.ScheduledEndAt = DateTime.UtcNow.AddDays(5).AddHours(2);
        }));

        Assert.True(created.IsForceDisplay);
        Assert.True(created.EffectiveForceDisplay);
        Assert.False(created.CanDismiss);
    }

    [Fact]
    public void ComputeEffectiveForceDisplay_UsesDefault24hLead()
    {
        var start = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var entity = new MaintenanceNotification
        {
            ScheduledStartAt = start,
            ScheduledEndAt = start.AddHours(2),
            IsForceDisplay = false,
            IsMandatory = false,
            ForceDisplayFrom = null,
        };

        Assert.False(
            MaintenanceNotificationService.ComputeEffectiveForceDisplay(
                entity,
                start.AddHours(-25)));
        Assert.True(
            MaintenanceNotificationService.ComputeEffectiveForceDisplay(
                entity,
                start.AddHours(-23)));
    }
}

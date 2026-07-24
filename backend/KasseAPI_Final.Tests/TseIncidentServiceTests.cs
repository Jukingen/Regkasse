using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseIncidentServiceTests
{
    [Fact]
    public async Task CreateAndResolve_UpdatesStatusAndPublishes()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateService(db, activity.Object);
        var created = await svc.CreateIncidentAsync(new CreateTseIncidentRequestDto
        {
            TenantId = tenantId,
            Title = "TSE offline",
            Description = "Primary device stopped responding to health probes.",
            Severity = TseIncidentSeverities.High,
        }, actorUserId: "sa-1");

        Assert.Equal(TseIncidentStatuses.Open, created.Status);
        Assert.NotEmpty(created.Logs);

        var resolved = await svc.UpdateIncidentStatusAsync(
            created.Id,
            TseIncidentStatuses.Resolved,
            resolution: "Failover activated and primary repaired.",
            actorUserId: "sa-1");

        Assert.Equal(TseIncidentStatuses.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Contains(resolved.Logs, l => l.EventType == TseIncidentLogEventTypes.StatusChanged);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseIncidentCreated,
                It.IsAny<object?>(),
                "sa-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseIncidentResolved,
                It.IsAny<object?>(),
                "sa-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateIncidentReport_IncludesTimeline()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());

        var created = await svc.CreateIncidentAsync(new CreateTseIncidentRequestDto
        {
            TenantId = tenantId,
            Title = "Certificate expiring",
            Description = "Certificate expires within warning window.",
            Severity = TseIncidentSeverities.Medium,
        });

        await svc.AddIncidentActionAsync(created.Id, new AddTseIncidentActionRequestDto
        {
            ActionType = TseIncidentActionTypes.RenewCertificate,
            Description = "Schedule renewal",
            IsCompleted = false,
        });

        var report = await svc.GenerateIncidentReportAsync(created.Id);
        Assert.Equal(created.Id, report.IncidentId);
        Assert.True(report.LogCount >= 1);
        Assert.Equal(1, report.ActionCount);
        Assert.False(string.IsNullOrWhiteSpace(report.Summary));
        Assert.NotEmpty(report.Timeline);
    }

    [Fact]
    public async Task GetDashboard_CountsByStatus()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());

        var open = await svc.CreateIncidentAsync(new CreateTseIncidentRequestDto
        {
            TenantId = tenantId,
            Title = "Open incident",
            Description = "Still open for dashboard counters.",
            Severity = TseIncidentSeverities.Critical,
        });
        await svc.CreateIncidentAsync(new CreateTseIncidentRequestDto
        {
            TenantId = tenantId,
            Title = "Second open",
            Description = "Another open incident for counts.",
            Severity = TseIncidentSeverities.Low,
        });
        await svc.UpdateIncidentStatusAsync(
            open.Id,
            TseIncidentStatuses.Investigating,
            note: "Looking into it");

        var dashboard = await svc.GetDashboardAsync(tenantId);
        Assert.Equal(1, dashboard.OpenCount);
        Assert.Equal(1, dashboard.InvestigatingCount);
        Assert.Equal(1, dashboard.CriticalOpenCount);
        Assert.Equal(2, dashboard.Incidents.Count);
    }

    private static TseIncidentService CreateService(AppDbContext db, IActivityEventPublisher activity) =>
        new(db, activity, NullLogger<TseIncidentService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_incident_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = id,
            Name = "Incident Cafe",
            Slug = "incident-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }
}

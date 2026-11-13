using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.DemoRequests;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminDemoRequestsControllerTests
{
    [Fact]
    public async Task List_AsSuperAdmin_ReturnsRows()
    {
        await using var db = CreateDb();
        db.DemoRequests.Add(new DemoRequest
        {
            Name = "Ada Lovelace",
            Email = "ada@example.at",
            Company = "Café Beispiel",
            Industry = VerticalProfileIds.Retail,
            Status = DemoRequestStatuses.New,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, Roles.SuperAdmin);
        var result = await controller.List(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<DemoRequestListDto>(ok.Value);
        Assert.Equal(1, list.Total);
        Assert.Equal("Ada Lovelace", Assert.Single(list.Items).Name);
    }

    [Fact]
    public async Task List_AsNonSuperAdmin_Returns403()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, Roles.Manager);

        var result = await controller.List(cancellationToken: CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static AdminDemoRequestsController CreateController(AppDbContext db, string role)
    {
        var service = new DemoRequestService(db, new DemoRequestRateLimiter(), new NoopActivity());
        var controller = new AdminDemoRequestsController(service);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)],
            authenticationType: "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
        return controller;
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private sealed class NoopActivity : IActivityEventPublisher
    {
        public Task TryPublishAsync(
            Guid tenantId,
            ActivityEventType type,
            object? metadata = null,
            string? actorUserId = null,
            string? dedupKey = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryPublishAsync(ActivityEventPublishRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

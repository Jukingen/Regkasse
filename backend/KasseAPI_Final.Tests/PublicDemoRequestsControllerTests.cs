using System.Net;
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

public sealed class PublicDemoRequestsControllerTests
{
    [Fact]
    public async Task Create_ValidSubmit_Returns201AndWritesRow()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, out var activity);

        var result = await controller.Create(ValidBody(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var body = Assert.IsType<DemoRequestCreatedDto>(created.Value);
        Assert.Equal(DemoRequestStatuses.New, body.Status);

        var row = await db.DemoRequests.SingleAsync();
        Assert.Equal(body.Id, row.Id);
        Assert.Equal("Ada Lovelace", row.Name);
        Assert.Equal("ada@example.at", row.Email);
        Assert.Equal(VerticalProfileIds.Gastronomy, row.Industry);
        Assert.Equal("Bitte um eine Vorführung.", row.Message);
        Assert.NotNull(activity.Last);
        Assert.Equal(ActivityEventType.DemoRequestReceived, activity.Last!.Type);
    }

    [Fact]
    public async Task Create_InvalidEmail_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, out _);
        var body = ValidBody();
        body.Email = "not-an-email";

        var result = await controller.Create(body, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<DemoRequestErrorDto>(bad.Value);
        Assert.Equal(DemoRequestService.ValidationCode, error.Code);
        Assert.Empty(db.DemoRequests);
    }

    [Fact]
    public async Task Create_UnknownIndustry_Returns400()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, out _);
        var body = ValidBody();
        body.Industry = "not-a-profile";

        var result = await controller.Create(body, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.DemoRequests);
    }

    [Fact]
    public async Task Create_FourthRequestFromSameIpWithinAnHour_Returns429()
    {
        await using var db = CreateDb();
        var limiter = new DemoRequestRateLimiter();
        var controller = CreateController(db, out _, limiter);

        for (var i = 0; i < DemoRequestRateLimiter.MaxRequestsPerIpPerHour; i++)
        {
            var ok = await controller.Create(ValidBody(), CancellationToken.None);
            Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(ok).StatusCode);
        }

        var blocked = await controller.Create(ValidBody(), CancellationToken.None);
        var result = Assert.IsType<ObjectResult>(blocked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        var error = Assert.IsType<DemoRequestErrorDto>(result.Value);
        Assert.Equal(DemoRequestRateLimiter.RateLimitedCode, error.Code);
        Assert.Equal(3, await db.DemoRequests.CountAsync());
    }

    [Fact]
    public async Task Create_WritesDemoRequestReceivedWithoutTheMessage()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, out var activity);

        await controller.Create(ValidBody(), CancellationToken.None);

        var request = activity.Last;
        Assert.NotNull(request);
        Assert.Equal((int)ActivityEventType.DemoRequestReceived, 265);
        Assert.Equal(ActivityEventType.DemoRequestReceived, request!.Type);
        Assert.Equal(SystemTenantIds.Platform, request.TenantId);
        Assert.NotNull(request.Metadata);
        Assert.True(request.Metadata!.ContainsKey("requestId"));
        Assert.Equal("ada@example.at", request.Metadata["email"]);
        Assert.Equal(VerticalProfileIds.Gastronomy, request.Metadata["industry"]);
        Assert.DoesNotContain(request.Metadata.Keys, key => key.Equals("message", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            request.Metadata.Values.Select(value => value?.ToString() ?? string.Empty),
            value => value.Contains("Vorführung", StringComparison.Ordinal));
    }

    private static CreateDemoRequestDto ValidBody() => new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.at",
        Company = "Café Beispiel",
        Industry = VerticalProfileIds.Gastronomy,
        Message = "Bitte um eine Vorführung.",
    };

    private static PublicDemoRequestsController CreateController(
        AppDbContext db,
        out CapturingActivity activity,
        DemoRequestRateLimiter? limiter = null)
    {
        activity = new CapturingActivity();
        var service = new DemoRequestService(db, limiter ?? new DemoRequestRateLimiter(), activity);
        var controller = new PublicDemoRequestsController(service);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private sealed class CapturingActivity : IActivityEventPublisher
    {
        public ActivityEventPublishRequest? Last { get; private set; }

        public Task TryPublishAsync(
            Guid tenantId,
            ActivityEventType type,
            object? metadata = null,
            string? actorUserId = null,
            string? dedupKey = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryPublishAsync(ActivityEventPublishRequest request, CancellationToken cancellationToken = default)
        {
            Last = request;
            return Task.CompletedTask;
        }
    }
}

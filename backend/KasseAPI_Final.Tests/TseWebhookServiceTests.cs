using System.Net;
using System.Net.Http;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseWebhookServiceTests
{
    [Fact]
    public async Task RegisterWebhookAsync_PersistsActiveRegistration()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, new StubHandler(HttpStatusCode.OK));

        var reg = await svc.RegisterWebhookAsync(new RegisterTseWebhookRequestDto
        {
            TenantId = tenantId,
            Url = "https://hooks.example.com/tse",
            Events = new List<string> { TseWebhookEventTypes.DeviceHealthChanged, "failoveroccurred" },
            Secret = "sekrit",
        }, "admin");

        Assert.Equal(TseWebhookStatuses.Active, reg.Status);
        Assert.True(reg.HasSecret);
        Assert.Contains(TseWebhookEventTypes.DeviceHealthChanged, reg.Events);
        Assert.Contains(TseWebhookEventTypes.FailoverOccurred, reg.Events);
        Assert.Equal(1, await db.TseWebhooks.CountAsync());
    }

    [Fact]
    public async Task RegisterWebhookAsync_RejectsHttpNonLocal()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, new StubHandler(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RegisterWebhookAsync(new RegisterTseWebhookRequestDto
            {
                TenantId = tenantId,
                Url = "http://hooks.example.com/tse",
                Events = new List<string> { TseWebhookEventTypes.Test },
            }));
    }

    [Fact]
    public async Task TriggerWebhookAsync_PostsAndLogsDelivery()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var handler = new StubHandler(HttpStatusCode.OK, "{\"ok\":true}");
        var svc = CreateService(db, handler);

        var reg = await svc.RegisterWebhookAsync(new RegisterTseWebhookRequestDto
        {
            TenantId = tenantId,
            Url = "https://hooks.example.com/tse",
            Events = new List<string> { TseWebhookEventTypes.CertificateExpiry },
            Secret = "s3cret",
        });

        var result = await svc.TriggerWebhookAsync(reg.Id, new TseWebhookEventDto
        {
            EventType = TseWebhookEventTypes.CertificateExpiry,
            OccurredAt = DateTime.UtcNow,
            Payload = new { daysUntilExpiry = 7 },
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatus);
        Assert.Equal(1, await db.TseWebhookDeliveries.CountAsync());
        Assert.True(handler.LastRequestHadSecret);
        Assert.Contains("CertificateExpiry", handler.LastBody ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestWebhookAsync_DeliversTestEvent()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, new StubHandler(HttpStatusCode.Accepted));

        var reg = await svc.RegisterWebhookAsync(new RegisterTseWebhookRequestDto
        {
            TenantId = tenantId,
            Url = "http://localhost:9999/hook",
            Events = new List<string> { TseWebhookEventTypes.DeviceHealthChanged },
        });

        var result = await svc.TestWebhookAsync(reg.Id);
        Assert.True(result.Success);
        Assert.Equal(1, await db.TseWebhookDeliveries.CountAsync(d => d.EventType == TseWebhookEventTypes.Test));
    }

    [Fact]
    public async Task DeleteWebhookAsync_RemovesRow()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, new StubHandler(HttpStatusCode.OK));
        var reg = await svc.RegisterWebhookAsync(new RegisterTseWebhookRequestDto
        {
            TenantId = tenantId,
            Url = "https://hooks.example.com/tse",
            Events = new List<string> { TseWebhookEventTypes.Test },
        });

        await svc.DeleteWebhookAsync(reg.Id);
        Assert.Equal(0, await db.TseWebhooks.CountAsync());
    }

    private static TseWebhookService CreateService(AppDbContext db, HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(TseWebhookService.HttpClientName))
            .Returns(() => new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });
        return new TseWebhookService(db, factory.Object, NullLogger<TseWebhookService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_wh_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Webhook Cafe",
            Slug = "webhook-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body = "ok")
        {
            _status = status;
            _body = body;
        }

        public string? LastBody { get; private set; }
        public bool LastRequestHadSecret { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LastRequestHadSecret = request.Headers.Contains(TseWebhookService.SecretHeaderName);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }
}

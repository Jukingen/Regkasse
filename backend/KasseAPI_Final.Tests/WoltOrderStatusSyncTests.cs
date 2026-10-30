using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class WoltOrderStatusSyncTests
{
    [Fact]
    public async Task Kitchen_steps_call_accept_ready_and_delivered_only_for_self_delivery()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, selfDelivery: true);

        var accepted = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted);
        var preparing = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Preparing);
        var ready = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Ready);
        var completed = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Completed);

        Assert.True(accepted.Succeeded);
        Assert.True(preparing.Succeeded);
        Assert.True(ready.Succeeded);
        Assert.True(completed.Succeeded);
        Assert.Equal(
            new[] { "PUT /orders/ord-1/accept", "PUT /orders/ord-1/ready", "PUT /orders/ord-1/delivered" },
            calls);
        Assert.Equal(OnlineOrderStatuses.Completed, (await harness.ReloadOrderAsync()).OrderStatus);
    }

    [Fact]
    public async Task Courier_delivery_completes_locally_without_a_delivered_put()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, selfDelivery: false);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Preparing);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Ready);
        calls.Clear();

        var completed = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Completed);

        Assert.True(completed.Succeeded);
        Assert.Empty(calls);
        Assert.Equal(OnlineOrderStatuses.Completed, (await harness.ReloadOrderAsync()).OrderStatus);
    }

    [Fact]
    public async Task Missing_self_delivery_is_resolved_with_one_order_get()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, selfDelivery: null, getBody: "{\"status\":\"READY\",\"delivery\":{\"self_delivery\":true}}");
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Preparing);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Ready);
        calls.Clear();

        var completed = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Completed);

        Assert.True(completed.Succeeded);
        Assert.Equal(new[] { "GET /v2/orders/ord-1", "PUT /orders/ord-1/delivered" }, calls);
        Assert.True((await harness.ReloadIntegrationAsync()).WoltSelfDelivery);
    }

    [Fact]
    public async Task Missing_self_delivery_field_completes_locally()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, selfDelivery: null, getBody: "{\"status\":\"READY\",\"delivery\":{}}");
        await AdvanceToReadyAsync(harness);
        calls.Clear();

        var completed = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Completed);

        Assert.True(completed.Succeeded);
        Assert.Equal(new[] { "GET /v2/orders/ord-1" }, calls);
        Assert.False((await harness.ReloadIntegrationAsync()).WoltSelfDelivery);
    }

    [Fact]
    public async Task Already_in_target_state_counts_as_success()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, selfDelivery: true, putStatus: HttpStatusCode.Conflict, getBody: "{\"status\":\"PRODUCTION\"}");

        var accepted = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted);

        Assert.True(accepted.Succeeded);
        Assert.Equal(new[] { "PUT /orders/ord-1/accept", "GET /v2/orders/ord-1" }, calls);
        Assert.Equal(OnlineOrderStatuses.Accepted, (await harness.ReloadOrderAsync()).OrderStatus);
        Assert.Null((await harness.ReloadIntegrationAsync()).LastError);
    }

    [Fact]
    public async Task Conflicting_409_and_422_do_not_advance_or_schedule_a_retry()
    {
        var calls = new List<string>();
        var conflict = await Harness.CreateAsync(calls, selfDelivery: true, putStatus: HttpStatusCode.Conflict, getBody: "{\"status\":\"REJECTED\"}");
        var before = await conflict.ReloadIntegrationAsync();

        var rejected = await conflict.Status.UpdateStatusAsync(conflict.OrderId, OnlineOrderStatuses.Accepted);
        var stored = await conflict.ReloadIntegrationAsync();

        Assert.False(rejected.Succeeded);
        Assert.Equal(WoltOrderStatusSync.RejectedCode, rejected.Code);
        Assert.Equal(OnlineOrderStatuses.Pending, (await conflict.ReloadOrderAsync()).OrderStatus);
        Assert.Contains("409", stored.LastError, StringComparison.Ordinal);
        Assert.Equal(before.Status, stored.Status);
        Assert.Equal(before.AttemptCount, stored.AttemptCount);
        Assert.Equal(before.NextAttemptAt, stored.NextAttemptAt);

        var unprocessable = await Harness.CreateAsync(calls, selfDelivery: true, putStatus: HttpStatusCode.UnprocessableEntity);
        var failed = await unprocessable.Status.UpdateStatusAsync(unprocessable.OrderId, OnlineOrderStatuses.Accepted);
        Assert.False(failed.Succeeded);
        Assert.Equal(OnlineOrderStatuses.Pending, (await unprocessable.ReloadOrderAsync()).OrderStatus);
        Assert.Contains("422", (await unprocessable.ReloadIntegrationAsync()).LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_gastronomy_wolt_transition_is_forbidden_and_does_not_call_wolt()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, selfDelivery: true, gastronomy: false);

        var accepted = await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted);

        Assert.False(accepted.Succeeded);
        Assert.Equal(ProfileEndpointDisabledException.Code, accepted.Code);
        Assert.Empty(calls);
        Assert.Equal(OnlineOrderStatuses.Pending, (await harness.ReloadOrderAsync()).OrderStatus);
    }

    private static async Task AdvanceToReadyAsync(Harness harness)
    {
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted)).Succeeded);
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Preparing)).Succeeded);
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Ready)).Succeeded);
    }

    private sealed class Harness
    {
        public required Guid OrderId { get; init; }
        public required OnlineOrderStatusService Status { get; init; }
        public required IDbContextFactory<AppDbContext> Factory { get; init; }

        public static async Task<Harness> CreateAsync(
            List<string> calls,
            bool? selfDelivery,
            bool gastronomy = true,
            HttpStatusCode putStatus = HttpStatusCode.NoContent,
            string getBody = "{\"status\":\"PRODUCTION\"}")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var tenant = new TenantTestDoubles.MutableTenantAccessor(Guid.NewGuid());
            var db = new AppDbContext(options, tenant);
            var factory = TenantTestDoubles.DbContextFactoryForTests(options, tenant);
            var orderId = Guid.NewGuid();
            db.OnlineOrders.Add(new OnlineOrder
            {
                Id = orderId,
                TenantId = tenant.TenantId!.Value,
                OrderNumber = "ORD-1",
                CustomerName = "Max Mustermann",
                CustomerPhone = "+431234567",
                OrderStatus = OnlineOrderStatuses.Pending,
                PaymentMethod = OnlineOrderPaymentMethods.Online,
                PaymentStatus = OnlineOrderPaymentStatuses.Pending,
                Source = OnlineOrderSources.Marketplace,
                CreatedAt = DateTime.UtcNow,
            });
            db.OnlineOrderIntegrations.Add(new OnlineOrderIntegration
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId!.Value,
                Provider = "wolt",
                ExternalOrderId = "ord-1",
                ExternalLocationId = "venue-1",
                OnlineOrderId = orderId,
                Status = OnlineOrderIntegrationStatuses.CartLinked,
                WoltSelfDelivery = selfDelivery,
                AttemptCount = 1,
                ReceivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            await db.SaveChangesAsync();

            var handler = new StubHandler((request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                calls.Add(request.Method + " " + path);
                if (request.Method == HttpMethod.Get)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(getBody, Encoding.UTF8, "application/json"),
                    });
                }

                return Task.FromResult(new HttpResponseMessage(putStatus));
            });
            var http = new Mock<IHttpClientFactory>();
            http.Setup(item => item.CreateClient(WoltOAuthClient.HttpClientName)).Returns(new HttpClient(handler));
            var clientOptions = Options.Create(new OnlineOrderIntegrationOptions
            {
                Wolt = new WoltIntegrationOptions { OrderBaseUrl = "https://wolt.test" },
            });
            var oauth = new WoltOAuthClient(http.Object, clientOptions, TimeProvider.System, NullLogger<WoltOAuthClient>.Instance);
            var tokens = new Mock<IWoltAccessTokenSource>();
            tokens.Setup(item => item.GetAccessTokenAsync(tenant.TenantId!.Value, "venue-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync("access-token");
            var profiles = new Mock<IVerticalProfileGuard>();
            profiles.Setup(item => item.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, It.IsAny<CancellationToken>()))
                .ReturnsAsync(gastronomy);
            var sync = new WoltOrderStatusSync(oauth, tokens.Object, profiles.Object);
            var notifications = new Mock<IOnlineOrderNotificationService>();
            notifications.Setup(item => item.NotifyStatusChangedAsync(It.IsAny<OnlineOrder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var status = new OnlineOrderStatusService(
                factory,
                tenant,
                notifications.Object,
                Mock.Of<IOnlineOrderLoyaltyService>(),
                TimeProvider.System,
                sync);
            return new Harness { OrderId = orderId, Status = status, Factory = factory };
        }

        public async Task<OnlineOrder> ReloadOrderAsync()
        {
            await using var db = await Factory.CreateDbContextAsync();
            return await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
        }

        public async Task<OnlineOrderIntegration> ReloadIntegrationAsync()
        {
            await using var db = await Factory.CreateDbContextAsync();
            return await db.OnlineOrderIntegrations.IgnoreQueryFilters().SingleAsync(r => r.OnlineOrderId == OrderId);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }
}

using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FoodoraOrderStatusSyncTests
{
    [Fact]
    public async Task Logistics_ready_puts_ready_for_pickup_and_completed_does_not_put()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, FoodoraOrderMapper.LogisticsDelivery);

        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted)).Succeeded);
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Preparing)).Succeeded);
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Ready)).Succeeded);
        Assert.Equal(new[] { "PUT /v2/chains/chain-1/orders/ord-1 READY_FOR_PICKUP" }, calls);

        calls.Clear();
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Completed)).Succeeded);
        Assert.Empty(calls);
        Assert.Equal(OnlineOrderStatuses.Completed, (await harness.ReloadOrderAsync()).OrderStatus);
    }

    [Fact]
    public async Task Vendor_ready_puts_dispatched_and_completed_does_not_put()
    {
        var calls = new List<string>();
        var harness = await Harness.CreateAsync(calls, FoodoraOrderMapper.VendorDelivery);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Accepted);
        await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Preparing);
        calls.Clear();

        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Ready)).Succeeded);
        Assert.Equal(new[] { "PUT /v2/chains/chain-1/orders/ord-1 DISPATCHED" }, calls);

        calls.Clear();
        Assert.True((await harness.Status.UpdateStatusAsync(harness.OrderId, OnlineOrderStatuses.Completed)).Succeeded);
        Assert.Empty(calls);
    }

    [Fact]
    public void Logistics_and_vendor_ready_actions_follow_the_transport_type()
    {
        Assert.Null(FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.LogisticsDelivery, "pending", "accepted"));
        Assert.Null(FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.LogisticsDelivery, "accepted", "preparing"));
        Assert.Equal("READY_FOR_PICKUP", FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.LogisticsDelivery, "preparing", "ready"));
        Assert.Null(FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.LogisticsDelivery, "ready", "completed"));
        Assert.Equal("DISPATCHED", FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.VendorDelivery, "preparing", "ready"));
        Assert.Null(FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.VendorDelivery, "ready", "completed"));
        Assert.Equal("CANCELLED", FoodoraOrderStatusSync.ActionFor(FoodoraOrderMapper.LogisticsDelivery, "pending", "cancelled"));
    }

    private sealed class Harness
    {
        public required Guid OrderId { get; init; }
        public required OnlineOrderStatusService Status { get; init; }
        public required IDbContextFactory<AppDbContext> Factory { get; init; }

        public static async Task<Harness> CreateAsync(List<string> calls, string transport)
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
                Provider = "foodora",
                ExternalOrderId = "ord-1",
                ExternalLocationId = "vendor-1",
                OnlineOrderId = orderId,
                Status = OnlineOrderIntegrationStatuses.CartLinked,
                FoodoraTransportType = transport,
                AttemptCount = 1,
                ReceivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            await db.SaveChangesAsync();

            var handler = new StubHandler(async (request, ct) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
                var status = body.Contains("READY_FOR_PICKUP", StringComparison.Ordinal)
                    ? "READY_FOR_PICKUP"
                    : body.Contains("DISPATCHED", StringComparison.Ordinal)
                        ? "DISPATCHED"
                        : body.Contains("CANCELLED", StringComparison.Ordinal)
                            ? "CANCELLED"
                            : "";
                calls.Add(request.Method + " " + path + " " + status);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var http = new Mock<IHttpClientFactory>();
            http.Setup(item => item.CreateClient(FoodoraAuthClient.HttpClientName)).Returns(new HttpClient(handler));
            var client = new FoodoraAuthClient(
                http.Object,
                Options.Create(new OnlineOrderIntegrationOptions
                {
                    Foodora = new FoodoraIntegrationOptions { ApiBaseUrl = "https://foodora.test" },
                }),
                TimeProvider.System,
                NullLogger<FoodoraAuthClient>.Instance);
            var tokens = new Mock<IFoodoraAccessTokenSource>();
            tokens.Setup(item => item.GetAccessAsync(tenant.TenantId!.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FoodoraAccess { AccessToken = "access-token", ChainId = "chain-1" });
            var profiles = new Mock<IVerticalProfileGuard>();
            profiles.Setup(item => item.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var sync = new FoodoraOrderStatusSync(client, tokens.Object, profiles.Object);
            var notifications = new Mock<IOnlineOrderNotificationService>();
            notifications.Setup(item => item.NotifyStatusChangedAsync(It.IsAny<OnlineOrder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var status = new OnlineOrderStatusService(
                factory,
                tenant,
                notifications.Object,
                Mock.Of<IOnlineOrderLoyaltyService>(),
                TimeProvider.System,
                wolt: null,
                foodora: sync);
            return new Harness { OrderId = orderId, Status = status, Factory = factory };
        }

        public async Task<OnlineOrder> ReloadOrderAsync()
        {
            await using var db = await Factory.CreateDbContextAsync();
            return await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
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

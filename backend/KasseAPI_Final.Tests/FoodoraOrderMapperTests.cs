using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FoodoraOrderMapperTests
{
    [Fact]
    public void Sku_is_the_catalog_key_and_platform_order_id_is_not_the_uniqueness_key()
    {
        var mapped = FoodoraOrderMapper.Map(Sample(status: "RECEIVED"));

        Assert.True(mapped.Parsed);
        Assert.True(mapped.CountryAccepted);
        Assert.Equal("11111111-1111-4111-8111-111111111111", mapped.OrderId);
        Assert.Equal("order-78107", mapped.PlatformOrderId);
        Assert.Equal(OnlineOrderStatuses.Pending, mapped.KitchenStatus);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        Assert.Equal("11111111-1111-4111-8111-111111111111", body.ExternalOrderId);
        Assert.NotEqual("order-78107", body.ExternalOrderId);
        Assert.Contains("order-78107", body.Notes, StringComparison.Ordinal);
        Assert.Contains("42", body.Notes, StringComparison.Ordinal);
        var item = Assert.Single(body.Items!);
        Assert.Equal("plu-pizza", item.ExternalProductId);
        Assert.NotEqual("line-1", item.ExternalProductId);
        Assert.Equal(10.00m, item.UnitPrice);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(12.50m, body.ChannelTotal);
        Assert.Equal(2.00m, body.DeliveryFee);
        Assert.Equal(0.50m, body.ServiceFee);
        Assert.Equal("vendor-1", body.ExternalLocationId);
    }

    [Theory]
    [InlineData("RECEIVED", "pending")]
    [InlineData("READY_FOR_PICKUP", "ready")]
    [InlineData("DISPATCHED", "completed")]
    [InlineData("CANCELLED", "cancelled")]
    [InlineData("CANCELED", "cancelled")]
    public void Maps_foodora_statuses_onto_the_kitchen_status(string foodoraStatus, string kitchenStatus)
    {
        Assert.Equal(kitchenStatus, FoodoraOrderMapper.MapKitchenStatus(foodoraStatus));
        var mapped = FoodoraOrderMapper.Map(Sample(status: foodoraStatus));
        Assert.Equal(kitchenStatus, mapped.KitchenStatus);
    }

    [Fact]
    public void Country_other_than_at_is_rejected_and_keeps_the_channel_total()
    {
        var mapped = FoodoraOrderMapper.Map(Sample(status: "RECEIVED", country: "de"));

        Assert.True(mapped.Parsed);
        Assert.False(mapped.CountryAccepted);
        Assert.Equal(FoodoraOrderMapper.CountryError, mapped.Error);
        var body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(mapped.NormalizedJson!, JsonOptions())!;
        Assert.Equal(12.50m, body.ChannelTotal);
    }

    [Fact]
    public void Pending_order_is_flagged_at_received_at_plus_five_minutes()
    {
        var received = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(received.AddMinutes(5), FoodoraAckDeadline.DeadlineUtc(received));
        Assert.False(FoodoraAckDeadline.ShouldFlag(received, OnlineOrderStatuses.Pending, received.AddMinutes(5).AddSeconds(-1)));
        Assert.True(FoodoraAckDeadline.ShouldFlag(received, OnlineOrderStatuses.Pending, received.AddMinutes(5)));
        Assert.False(FoodoraAckDeadline.ShouldFlag(received, OnlineOrderStatuses.Accepted, received.AddHours(1)));
        Assert.Equal("FOODORA_ACK_DEADLINE", FoodoraAckDeadline.ErrorCode);
    }

    [Fact]
    public async Task Scanner_sets_last_error_when_the_order_is_still_pending_after_five_minutes()
    {
        var received = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = new TenantTestDoubles.MutableTenantAccessor(Guid.NewGuid());
        await using (var db = new AppDbContext(options, tenant))
        {
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
                CreatedAt = received,
            });
            db.OnlineOrderIntegrations.Add(new OnlineOrderIntegration
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId!.Value,
                Provider = "foodora",
                ExternalOrderId = "11111111-1111-4111-8111-111111111111",
                ExternalLocationId = "vendor-1",
                OnlineOrderId = orderId,
                Status = OnlineOrderIntegrationStatuses.CartLinked,
                ReceivedAt = received,
                CreatedAt = received,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider { Now = new DateTimeOffset(received.AddMinutes(5), TimeSpan.Zero) };
        var scanner = new FoodoraAckDeadlineScanner(
            TenantTestDoubles.DbContextFactoryForTests(options, tenant),
            clock);
        await scanner.MarkExpiredAsync();

        await using var check = new AppDbContext(options, tenant);
        var row = await check.OnlineOrderIntegrations.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(FoodoraAckDeadline.ErrorCode, row.LastError);
    }

    private static string Sample(string status, string country = "at") =>
        $$"""
        {
          "order_id": "11111111-1111-4111-8111-111111111111",
          "external_order_id": "order-78107",
          "order_code": "42",
          "status": "{{status}}",
          "transport_type": "LOGISTICS_DELIVERY",
          "client": { "id": "vendor-1", "chain_id": "chain-1", "country_code": "{{country}}" },
          "customer": { "name": "Max Mustermann", "phone": "+431234567" },
          "payment": { "order_total": 12.5, "delivery_fee": 2.0, "service_fee": 0.5 },
          "items": [
            { "_id": "line-1", "sku": "plu-pizza", "name": "Pizza", "quantity": 1, "unit_price": 10.0 }
          ]
        }
        """;

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Hosted;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderIntegrationTests
{
    [Fact]
    public void Signature_accepts_hmac_sha256_of_the_raw_body()
    {
        const string secret = "local-test-secret";
        const string body = "{\"externalOrderId\":\"ord-1\"}";
        var hex = OnlineOrderIntegrationSignature.SignHex(body, secret);

        Assert.True(OnlineOrderIntegrationSignature.IsValid(body, secret, hex));
        Assert.True(OnlineOrderIntegrationSignature.IsValid(body, secret, "sha256=" + hex));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, secret, hex + "00"));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, null, hex));
        Assert.False(OnlineOrderIntegrationConfirmed);
    }

    [Fact]
    public async Task Enabling_a_location_in_production_is_rejected_while_the_placeholder_is_unconfirmed()
    {
        var harness = await Harness.CreateAsync("Production");

        var result = await harness.Locations.UpsertAsync(new UpsertOnlineOrderIntegrationLocationRequest
        {
            Provider = "deliverect",
            ExternalLocationId = "loc-1",
            Enabled = true,
        });

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(OnlineOrderIntegrationSignatureStatus.UnconfirmedCode, result.Code);
        Assert.False(OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed);
    }

    [Fact]
    public async Task Unknown_or_disabled_location_returns_404_and_writes_nothing()
    {
        var harness = await Harness.CreateAsync();
        var audit = harness.Audit();
        var webhooks = harness.Webhooks(audit.Object, new NoopActivity());
        var unknownBody = harness.Body("ord-missing", locationId: "no-such-location");

        var unknown = await webhooks.AcceptAsync("deliverect", unknownBody, harness.Sign(unknownBody), CancellationToken.None);
        var disabledBody = harness.Body("ord-off");
        harness.Location.Enabled = false;
        await harness.Db.SaveChangesAsync();
        var disabled = await webhooks.AcceptAsync("deliverect", disabledBody, harness.Sign(disabledBody), CancellationToken.None);

        Assert.Equal(404, unknown.StatusCode);
        Assert.Equal(404, disabled.StatusCode);
        audit.Verify(
            a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Never);
        Assert.Empty(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
        Assert.Empty(harness.Db.OnlineOrders.IgnoreQueryFilters());
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Invalid_signature_returns_400_and_writes_no_row()
    {
        var harness = await Harness.CreateAsync();
        var body = harness.Body("ord-bad-sign");

        var result = await harness.Webhooks(harness.Audit().Object, new NoopActivity())
            .AcceptAsync("deliverect", body, "not-a-signature", CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Duplicate_external_order_id_does_not_create_a_second_online_order()
    {
        var harness = await Harness.CreateAsync();
        var webhooks = harness.Webhooks(harness.Audit().Object, new NoopActivity());
        var body = harness.Body("ord-dup");
        var signature = harness.Sign(body);

        var first = await webhooks.AcceptAsync("deliverect", body, signature, CancellationToken.None);
        var second = await webhooks.AcceptAsync("deliverect", body, signature, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, harness.Db.OnlineOrders.IgnoreQueryFilters().Count());
        Assert.Equal(1, harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task Audit_failure_does_not_open_a_cart()
    {
        var harness = await Harness.CreateAsync();
        var audit = harness.Audit(throwOnWrite: true);
        var webhooks = harness.Webhooks(audit.Object, new NoopActivity());

        await webhooks.AcceptAsync("deliverect", harness.Body("ord-audit"), harness.Sign(harness.Body("ord-audit")), CancellationToken.None);

        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
        Assert.DoesNotContain(
            harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters(),
            row => row.Status == OnlineOrderIntegrationStatuses.CartLinked);
    }

    [Fact]
    public async Task Claiming_user_without_approve_permission_still_owns_the_cart()
    {
        var harness = await Harness.CreateAsync(role: "Cashier");
        var webhooks = harness.Webhooks(harness.Audit().Object, new NoopActivity());
        var body = harness.Body("ord-owner");

        var result = await webhooks.AcceptAsync("deliverect", body, harness.Sign(body), CancellationToken.None);

        Assert.True(result.Succeeded);
        var cart = Assert.Single(harness.Db.Carts.IgnoreQueryFilters());
        Assert.Equal(harness.User.Id, cart.UserId);
        Assert.Empty(harness.Db.PaymentDetails.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Total_mismatch_marks_the_row_failed_and_does_not_open_a_cart()
    {
        var harness = await Harness.CreateAsync();
        var body = harness.Body("ord-mismatch", channelTotal: 99.99m);
        await harness.Webhooks(harness.Audit().Object, new NoopActivity())
            .AcceptAsync("deliverect", body, harness.Sign(body), CancellationToken.None);

        var row = Assert.Single(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
        Assert.Equal(OnlineOrderIntegrationStatuses.Failed, row.Status);
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
        Assert.Empty(harness.Db.OnlineOrders.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Fee_without_a_product_id_marks_the_row_failed()
    {
        var harness = await Harness.CreateAsync();
        harness.Location.DeliveryFeeProductId = null;
        await harness.Db.SaveChangesAsync();
        var body = harness.Body("ord-fee", deliveryFee: 1.50m, channelTotal: 11.50m);

        await harness.Webhooks(harness.Audit().Object, new NoopActivity())
            .AcceptAsync("deliverect", body, harness.Sign(body), CancellationToken.None);

        var row = Assert.Single(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
        Assert.Equal(OnlineOrderIntegrationStatuses.Failed, row.Status);
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Matching_fee_lines_open_one_cart_and_never_call_payment()
    {
        var harness = await Harness.CreateAsync();
        var body = harness.Body("ord-fees", deliveryFee: 1.50m, serviceFee: 0.50m, channelTotal: 12.00m);

        await harness.Webhooks(harness.Audit().Object, new NoopActivity())
            .AcceptAsync("deliverect", body, harness.Sign(body), CancellationToken.None);

        var order = Assert.Single(harness.Db.OnlineOrders.IgnoreQueryFilters().Include(o => o.Items));
        Assert.Equal(12.00m, order.Total);
        Assert.Equal(3, order.Items.Count);
        Assert.Equal(OnlineOrderSources.Marketplace, order.Source);
        Assert.Single(harness.Db.Carts.IgnoreQueryFilters());
        Assert.Empty(harness.Db.PaymentDetails.IgnoreQueryFilters());
        Assert.DoesNotContain(
            typeof(OnlineOrderIntegrationProcessor).GetConstructors().SelectMany(c => c.GetParameters()),
            p => p.ParameterType.Name.Contains("PaymentService", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(OnlineOrderIntegrationRetryHostedService).GetConstructors().SelectMany(c => c.GetParameters()),
            p => p.ParameterType.Name.Contains("PaymentService", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unmapped_external_product_marks_the_row_failed_and_does_not_open_a_cart()
    {
        var harness = await Harness.CreateAsync();
        var body = harness.Body("ord-unmapped", externalProductId: "missing-plu");

        await harness.Webhooks(harness.Audit().Object, new NoopActivity())
            .AcceptAsync("deliverect", body, harness.Sign(body), CancellationToken.None);

        var row = Assert.Single(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
        Assert.Equal(OnlineOrderIntegrationStatuses.Failed, row.Status);
        Assert.Contains("not mapped", row.LastError, StringComparison.Ordinal);
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
        Assert.Empty(harness.Db.OnlineOrders.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Mapped_external_product_uses_the_catalog_product_on_the_order_line()
    {
        var harness = await Harness.CreateAsync();
        var body = harness.Body("ord-mapped");

        await harness.Webhooks(harness.Audit().Object, new NoopActivity())
            .AcceptAsync("deliverect", body, harness.Sign(body), CancellationToken.None);

        var order = Assert.Single(harness.Db.OnlineOrders.IgnoreQueryFilters().Include(o => o.Items));
        var line = Assert.Single(order.Items);
        Assert.Equal(harness.Food.Id, line.ProductId);
        Assert.Empty(harness.Db.PaymentDetails.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Product_mapping_upsert_lists_the_ambient_tenant_only()
    {
        var harness = await Harness.CreateAsync();

        var saved = await harness.ProductMappings.UpsertAsync(new UpsertOnlineOrderProductMappingRequest
        {
            Provider = "Deliverect",
            ExternalProductId = " plu-extra ",
            ProductId = harness.Food.Id,
        });
        var list = await harness.ProductMappings.ListAsync("deliverect");

        Assert.True(saved.Succeeded);
        Assert.True(list.Succeeded);
        Assert.Contains(list.Body!.Items, item => item.ExternalProductId == "plu-extra" && item.ProductId == harness.Food.Id);
        Assert.All(list.Body.Items, item => Assert.Equal("deliverect", item.Provider));
    }

    [Fact]
    public async Task Product_mapping_rejects_a_product_outside_the_tenant()
    {
        var harness = await Harness.CreateAsync();
        var other = Harness.Product(Guid.NewGuid(), "Other", 1m);
        harness.Db.Products.Add(other);
        await harness.Db.SaveChangesAsync();

        harness.Food.IsActive = false;
        await harness.Db.SaveChangesAsync();
        var inactive = await harness.ProductMappings.UpsertAsync(new UpsertOnlineOrderProductMappingRequest
        {
            Provider = "deliverect",
            ExternalProductId = "plu-off",
            ProductId = harness.Food.Id,
        });
        var foreign = await harness.ProductMappings.UpsertAsync(new UpsertOnlineOrderProductMappingRequest
        {
            Provider = "deliverect",
            ExternalProductId = "plu-foreign",
            ProductId = other.Id,
        });
        harness.Tenant.TenantId = Guid.NewGuid();
        var missingTenant = await harness.ProductMappings.ListAsync("deliverect");

        Assert.Equal(400, inactive.StatusCode);
        Assert.Equal("PRODUCTS_INVALID", inactive.Code);
        Assert.Equal(400, foreign.StatusCode);
        Assert.Equal(404, missingTenant.StatusCode);
        Assert.Equal("TENANT_NOT_FOUND", missingTenant.Code);
    }

    [Fact]
    public void Retry_delays_are_one_two_four_eight_and_sixteen_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), OnlineOrderIntegrationProcessor.DelayAfterFailedAttempt(1));
        Assert.Equal(TimeSpan.FromMinutes(2), OnlineOrderIntegrationProcessor.DelayAfterFailedAttempt(2));
        Assert.Equal(TimeSpan.FromMinutes(4), OnlineOrderIntegrationProcessor.DelayAfterFailedAttempt(3));
        Assert.Equal(TimeSpan.FromMinutes(8), OnlineOrderIntegrationProcessor.DelayAfterFailedAttempt(4));
        Assert.Equal(TimeSpan.FromMinutes(16), OnlineOrderIntegrationProcessor.DelayAfterFailedAttempt(5));
        Assert.Equal(TimeSpan.Zero, OnlineOrderIntegrationProcessor.DelayAfterFailedAttempt(6));
    }

    private static bool OnlineOrderIntegrationConfirmed =>
        OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed;

    private sealed class Harness
    {
        public required AppDbContext Db { get; init; }
        public required TenantTestDoubles.MutableTenantAccessor Tenant { get; init; }
        public required IDbContextFactory<AppDbContext> Factory { get; init; }
        public required ApplicationUser User { get; init; }
        public required OnlineOrderIntegrationLocation Location { get; init; }
        public required Product Food { get; init; }
        public required Product DeliveryProduct { get; init; }
        public required Product ServiceProduct { get; init; }
        public required string EnvironmentName { get; init; }

        public static async Task<Harness> CreateAsync(string environment = "Development", string role = "Cashier")
        {
            var database = Guid.NewGuid().ToString("N");
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database).Options;
            var tenant = new TenantTestDoubles.MutableTenantAccessor(null);
            var db = new AppDbContext(options, tenant);
            var factory = TenantTestDoubles.DbContextFactoryForTests(options, tenant);
            var tenantId = Guid.NewGuid();
            tenant.TenantId = tenantId;
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = "cashier",
                Email = "cashier@example.test",
                FirstName = "Ada",
                LastName = "Kasse",
                IsActive = true,
                Role = role,
            };
            var food = Product(tenantId, "Pizza", 10m);
            var delivery = Product(tenantId, "Liefergebühr", 0m);
            var service = Product(tenantId, "Servicegebühr", 0m);
            var location = new OnlineOrderIntegrationLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = "deliverect",
                ExternalLocationId = "loc-1",
                ClaimingUserId = user.Id,
                Enabled = true,
                DeliveryFeeProductId = delivery.Id,
                ServiceFeeProductId = service.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Demo",
                Slug = "demo",
                IsActive = true,
            });
            db.Users.Add(user);
            db.Products.AddRange(food, delivery, service);
            db.OnlineOrderIntegrationLocations.Add(location);
            db.OnlineOrderProductMappings.Add(new OnlineOrderProductMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = "deliverect",
                ExternalProductId = "plu-pizza",
                ProductId = food.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return new Harness
            {
                Db = db,
                Tenant = tenant,
                Factory = factory,
                User = user,
                Location = location,
                Food = food,
                DeliveryProduct = delivery,
                ServiceProduct = service,
                EnvironmentName = environment,
            };
        }

        public string Body(
            string externalOrderId,
            decimal channelTotal = 10m,
            decimal deliveryFee = 0m,
            decimal serviceFee = 0m,
            string locationId = "loc-1",
            string externalProductId = "plu-pizza")
        {
            var payload = new
            {
                externalOrderId,
                externalLocationId = locationId,
                externalChannel = "lieferando",
                channelTotal,
                customerName = "Max Mustermann",
                customerPhone = "+431234567",
                orderType = "delivery",
                deliveryFee,
                serviceFee,
                items = new[]
                {
                    new { externalProductId, quantity = 1, unitPrice = 10m, name = "Pizza" },
                },
            };
            return JsonSerializer.Serialize(payload);
        }

        public string Sign(string body)
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes("local-test-secret"), Encoding.UTF8.GetBytes(body));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public Mock<IAuditLogService> Audit(bool throwOnWrite = false)
        {
            var audit = new Mock<IAuditLogService>();
            var setup = audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()));
            if (throwOnWrite)
                setup.ThrowsAsync(new InvalidOperationException("audit unavailable"));
            else
                setup.ReturnsAsync(new AuditLog());
            return audit;
        }

        public OnlineOrderIntegrationWebhookService Webhooks(IAuditLogService audit, IActivityEventPublisher activity)
        {
            var notifications = new Mock<IOnlineOrderNotificationService>();
            notifications
                .Setup(n => n.SendOrderConfirmationAsync(It.IsAny<OnlineOrder>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var processor = new OnlineOrderIntegrationProcessor(
                Factory,
                Tenant,
                new OrderIntegrationService(
                    Factory,
                    Tenant,
                    notifications.Object,
                    TimeProvider.System,
                    NullLogger<OrderIntegrationService>.Instance),
                audit,
                activity,
                TenantTestDoubles.HostEnvironmentReturning(EnvironmentName),
                TimeProvider.System,
                NullLogger<OnlineOrderIntegrationProcessor>.Instance);
            return new OnlineOrderIntegrationWebhookService(
                Factory,
                Tenant,
                Options.Create(new OnlineOrderIntegrationOptions
                {
                    WebhookSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["deliverect:loc-1"] = "local-test-secret",
                    },
                }),
                processor,
                TimeProvider.System,
                NullLogger<OnlineOrderIntegrationWebhookService>.Instance);
        }

        public OnlineOrderIntegrationLocationService Locations =>
            new(
                Factory,
                Tenant,
                TenantTestDoubles.HostEnvironmentReturning(EnvironmentName),
                TimeProvider.System);

        public OnlineOrderIntegrationProductMappingService ProductMappings =>
            new(Factory, Tenant, TimeProvider.System);

        public static Product Product(Guid tenantId, string name, decimal price) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Price = price,
            Category = "Speisen",
            TaxType = 2,
            IsActive = true,
        };
    }

    private sealed class NoopActivity : IActivityEventPublisher
    {
        public Task TryPublishAsync(
            Guid tenantId,
            ActivityEventType eventType,
            object? metadata = null,
            string? actorUserId = null,
            string? dedupKey = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryPublishAsync(
            ActivityEventPublishRequest request,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

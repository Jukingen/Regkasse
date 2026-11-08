using System.Text;
using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LieferandoOrderNotificationTests
{
    [Fact]
    public async Task Missing_authorization_is_rejected_before_the_body_is_read()
    {
        var (_, options, tenant, protector) = EmptyDatabase();
        var service = Service(TenantTestDoubles.DbContextFactoryForTests(options, tenant), protector);
        var body = new ThrowingStream();

        var result = await service.AcceptAsync("  ", null, body, CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("SIGNATURE_INVALID", result.Code);
        Assert.False(body.WasRead);
    }

    [Fact]
    public async Task Wrong_authorization_writes_nothing()
    {
        var db = await SeedAsync(hmacSecret: null);
        var body = new ThrowingStream();

        var result = await db.Service.AcceptAsync("wrong-key", null, body, CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("SIGNATURE_INVALID", result.Code);
        Assert.False(body.WasRead);
        Assert.Empty(db.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Valid_authorization_returns_the_online_order_number()
    {
        var db = await SeedAsync(hmacSecret: null);
        var raw = OrderJson();

        var result = await db.Service.AcceptAsync("jet-key", null, Stream(raw), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("ORD-001", result.OrderNumber);
        var order = await db.Db.OnlineOrders.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("ORD-001", order.OrderNumber);
        Assert.Equal(18.00m, order.Total);
        Assert.Equal("Anna Berger", order.CustomerName);
    }

    [Fact]
    public async Task Hmac_secret_rejects_a_bad_hash_and_accepts_the_envelope()
    {
        var db = await SeedAsync(hmacSecret: "hook-secret");
        var raw = OrderJson();
        var bad = await db.Service.AcceptAsync("jet-key", "00", Stream(raw), CancellationToken.None);
        Assert.Equal(400, bad.StatusCode);
        Assert.Empty(db.Db.OnlineOrderIntegrations.IgnoreQueryFilters());

        var hex = OnlineOrderIntegrationSignature.SignHex(raw, "hook-secret");
        var good = await db.Service.AcceptAsync(
            "Bearer jet-key",
            "HMAC-SHA256 t=1,signature=" + hex,
            Stream(raw),
            CancellationToken.None);

        Assert.Equal("ORD-001", good.OrderNumber);
    }

    [Fact]
    public async Task Masked_customer_is_stored_as_failed_and_is_not_acknowledged()
    {
        var db = await SeedAsync(hmacSecret: null);
        var raw = OrderJson(name: "****", phone: "****");

        var result = await db.Service.AcceptAsync("jet-key", null, Stream(raw), CancellationToken.None);

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(LieferandoOrderMapper.RejectedCode, result.Code);
        Assert.Equal(LieferandoOrderMapper.MaskedCustomerError, result.Error);
        var row = await db.Db.OnlineOrderIntegrations.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(OnlineOrderIntegrationStatuses.Failed, row.Status);
        Assert.Equal(LieferandoOrderMapper.MaskedCustomerError, row.LastError);
        Assert.Empty(db.Db.OnlineOrders.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Controller_keeps_the_OrderId_property_name()
    {
        var lieferando = new StubNotifications();
        var controller = new OnlineOrderIntegrationWebhookController(
            Mock.Of<IOnlineOrderIntegrationWebhookService>(),
            Mock.Of<IWoltOrderNotificationService>(),
            Mock.Of<IFoodoraOrderNotificationService>(),
            lieferando)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Accept("lieferando", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"OrderId\":\"ORD-009\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_location_stays_disabled_while_the_signature_is_unconfirmed()
    {
        var (db, options, tenant, _) = EmptyDatabase();
        var tenantId = tenant.TenantId!.Value;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Demo", Slug = "demo", IsActive = true });
        await db.SaveChangesAsync();
        var locations = new OnlineOrderIntegrationLocationService(
            TenantTestDoubles.DbContextFactoryForTests(options, tenant),
            tenant,
            TenantTestDoubles.HostEnvironmentReturning("Production"),
            TimeProvider.System);

        var result = await locations.UpsertAsync(new UpsertOnlineOrderIntegrationLocationRequest
        {
            Provider = "lieferando",
            ExternalLocationId = "AKZ12",
            Enabled = true,
        });

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(OnlineOrderIntegrationSignatureStatus.UnconfirmedCode, result.Code);
    }

    [Fact]
    public async Task Tenant_credentials_keep_the_authorization_key_as_ciphertext_and_allow_an_empty_hmac()
    {
        var (db, options, tenant, protector) = EmptyDatabase();
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, tenant);
        var providers = new OnlineOrderIntegrationProviderService(factory, tenant, protector, TimeProvider.System);
        await providers.UpsertAsync(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "lieferando",
            Enabled = true,
            ClientId = "should-not-stick",
            ClientSecret = "should-not-stick",
        });
        var credentials = new LieferandoTenantCredentialService(factory, tenant, protector, TimeProvider.System);

        var saved = await credentials.UpsertAsync(new UpsertLieferandoTenantCredentialRequest
        {
            ClientId = "jet-key",
            ClientSecret = "",
            ChainId = "brand-1",
            WebhookToken = "flyt-key",
        });
        var status = await credentials.GetAsync();

        Assert.True(saved.Succeeded);
        Assert.Equal(OnlineOrderIntegrationProviderStatuses.Configured, status.Body!.CredentialStatus);
        var provider = await db.OnlineOrderIntegrationProviders.SingleAsync();
        Assert.Equal(OnlineOrderIntegrationCredentialOwners.Tenant, provider.CredentialOwner);
        Assert.Equal(string.Empty, provider.ClientId);
        var stored = await db.OnlineOrderIntegrationTenantCredentials.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("brand-1", stored.ChainId);
        Assert.Equal(string.Empty, stored.ClientSecret);
        Assert.DoesNotContain("jet-key", stored.ClientId, StringComparison.Ordinal);
        Assert.DoesNotContain("flyt-key", stored.WebhookToken, StringComparison.Ordinal);
        Assert.DoesNotContain("jet-key", JsonSerializer.Serialize(status.Body), StringComparison.Ordinal);
    }

    private static LieferandoOrderNotificationService Service(
        IDbContextFactory<AppDbContext> factory,
        OnlineOrderIntegrationTokenProtector protector) =>
        new(
            factory,
            new TenantTestDoubles.MutableTenantAccessor(Guid.NewGuid()),
            protector,
            Mock.Of<IOnlineOrderIntegrationWebhookService>(),
            Profile(),
            TimeProvider.System,
            NullLogger<LieferandoOrderNotificationService>.Instance);

    private static async Task<Seed> SeedAsync(string? hmacSecret)
    {
        var (db, options, tenant, protector) = EmptyDatabase();
        var tenantId = tenant.TenantId!.Value;
        var food = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Cheeseburger",
            Price = 18m,
            Category = "Speisen",
            TaxType = 2,
            IsActive = true,
        };
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Demo", Slug = "demo", IsActive = true });
        db.Products.Add(food);
        db.OnlineOrderIntegrationProviders.Add(new OnlineOrderIntegrationProvider
        {
            Id = Guid.NewGuid(),
            Provider = "lieferando",
            Enabled = true,
            ClientId = string.Empty,
            ClientSecret = string.Empty,
            CredentialOwner = OnlineOrderIntegrationCredentialOwners.Tenant,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.OnlineOrderIntegrationTenantCredentials.Add(new OnlineOrderIntegrationTenantCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = "lieferando",
            ClientId = protector.Protect("jet-key"),
            ClientSecret = string.IsNullOrEmpty(hmacSecret) ? string.Empty : protector.Protect(hmacSecret),
            ChainId = "brand-1",
            WebhookToken = protector.Protect("flyt-key"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.OnlineOrderIntegrationLocations.Add(new OnlineOrderIntegrationLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = "lieferando",
            ExternalLocationId = "AKZ12",
            Enabled = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.OnlineOrderProductMappings.Add(new OnlineOrderProductMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = "lieferando",
            ExternalProductId = "M2",
            ProductId = food.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var factory = TenantTestDoubles.DbContextFactoryForTests(options, tenant);
        var processor = new OnlineOrderIntegrationProcessor(
            factory,
            tenant,
            Mock.Of<IOrderIntegrationService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<IActivityEventPublisher>(),
            TenantTestDoubles.HostEnvironmentReturning("Development"),
            TimeProvider.System,
            NullLogger<OnlineOrderIntegrationProcessor>.Instance);
        var webhooks = new OnlineOrderIntegrationWebhookService(
            factory,
            tenant,
            Microsoft.Extensions.Options.Options.Create(new KasseAPI_Final.Configuration.OnlineOrderIntegrationOptions()),
            processor,
            Profile(),
            TimeProvider.System,
            NullLogger<OnlineOrderIntegrationWebhookService>.Instance);
        var service = new LieferandoOrderNotificationService(
            factory,
            tenant,
            protector,
            webhooks,
            Profile(),
            TimeProvider.System,
            NullLogger<LieferandoOrderNotificationService>.Instance);
        return new Seed(db, service);
    }

    private static IVerticalProfileGuard Profile()
    {
        var guard = new Mock<IVerticalProfileGuard>();
        guard.Setup(item => item.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return guard.Object;
    }

    private static (AppDbContext Db, DbContextOptions<AppDbContext> Options, TenantTestDoubles.MutableTenantAccessor Tenant, OnlineOrderIntegrationTokenProtector Protector) EmptyDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = new TenantTestDoubles.MutableTenantAccessor(Guid.NewGuid());
        var db = new AppDbContext(options, tenant);
        return (db, options, tenant, new OnlineOrderIntegrationTokenProtector(new EphemeralDataProtectionProvider()));
    }

    private static string OrderJson(string name = "Anna", string phone = "+431234567") =>
        $$"""
        {
          "type": "delivery-by-delivery-partner",
          "posLocationId": "AKZ12",
          "id": "38bbeb45-f520-4438-a44f-0fcdbb29e166",
          "items": [
            {
              "name": "Cheeseburger",
              "plu": "M2",
              "children": [ { "name": "Extra Sauce", "plu": "R3", "price": 100 } ],
              "price": 1700
            }
          ],
          "third_party_order_reference": "22721763",
          "payment_method": "CARD",
          "payment": { "adjustments": [], "final": { "inc_tax": 1800, "tax": 0 } },
          "delivery": {
            "first_name": "{{name}}",
            "last_name": "Berger",
            "phone_number": "{{phone}}",
            "line_one": "Hauptstrasse 1",
            "city": "Wien",
            "postcode": "1010"
          }
        }
        """;

    private static MemoryStream Stream(string raw) => new(Encoding.UTF8.GetBytes(raw));

    private sealed record Seed(AppDbContext Db, LieferandoOrderNotificationService Service);

    private sealed class StubNotifications : ILieferandoOrderNotificationService
    {
        public Task<OnlineOrderIntegrationCommandResult> AcceptAsync(
            string? authorizationHeader,
            string? hashHeader,
            Stream body,
            CancellationToken ct = default) =>
            Task.FromResult(OnlineOrderIntegrationCommandResult.WithOrder(StatusCodes.Status200OK, Guid.NewGuid(), "ORD-009"));
    }

    private sealed class ThrowingStream : Stream
    {
        public bool WasRead { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            WasRead = true;
            throw new InvalidOperationException("Body was read.");
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Hosted;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task Wolt_enable_stays_blocked_in_production_until_the_test_hook_and_deliverect_stays_blocked()
    {
        var harness = await Harness.CreateAsync("Production");
        try
        {
            var blocked = await harness.Locations.UpsertAsync(new UpsertOnlineOrderIntegrationLocationRequest
            {
                Provider = "wolt",
                ExternalLocationId = "venue-1",
                Enabled = true,
            });

            Assert.False(blocked.Succeeded);
            Assert.Equal(409, blocked.StatusCode);
            Assert.Equal(OnlineOrderIntegrationSignatureStatus.UnconfirmedCode, blocked.Code);
            Assert.False(OnlineOrderIntegrationSignatureStatus.WoltConfirmed);
            Assert.False(OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed);
            Assert.DoesNotContain(
                harness.Db.OnlineOrderIntegrationLocations.IgnoreQueryFilters(),
                location => location.Provider == "wolt");

            OnlineOrderIntegrationSignatureStatus.WoltConfirmedForTests = true;
            var opened = await harness.Locations.UpsertAsync(new UpsertOnlineOrderIntegrationLocationRequest
            {
                Provider = "wolt",
                ExternalLocationId = "venue-1",
                Enabled = true,
            });
            var deliverect = await harness.Locations.UpsertAsync(new UpsertOnlineOrderIntegrationLocationRequest
            {
                Provider = "deliverect",
                ExternalLocationId = "loc-1",
                Enabled = true,
            });

            Assert.True(opened.Succeeded);
            Assert.Equal(200, opened.StatusCode);
            Assert.False(deliverect.Succeeded);
            Assert.Equal(409, deliverect.StatusCode);
            Assert.Equal(OnlineOrderIntegrationSignatureStatus.UnconfirmedCode, deliverect.Code);
        }
        finally
        {
            OnlineOrderIntegrationSignatureStatus.WoltConfirmedForTests = false;
        }
    }

    [Fact]
    public async Task Unconfirmed_wolt_row_is_skipped_in_production()
    {
        var harness = await Harness.CreateAsync("Production");
        var row = new OnlineOrderIntegration
        {
            Id = Guid.NewGuid(),
            TenantId = harness.Tenant.TenantId!.Value,
            Provider = "wolt",
            ExternalOrderId = "ord-wolt-skip",
            ExternalLocationId = "venue-1",
            Status = OnlineOrderIntegrationStatuses.Received,
            RawPayload = "{}",
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0,
            IsActive = true,
        };
        harness.Db.OnlineOrderIntegrations.Add(row);
        await harness.Db.SaveChangesAsync();

        await harness.Processor(harness.Audit().Object).ProcessAsync(row.Id);

        harness.Db.ChangeTracker.Clear();
        var stored = await harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(OnlineOrderIntegrationStatuses.Received, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
        Assert.False(OnlineOrderIntegrationSignatureStatus.WoltConfirmed);
    }

    [Fact]
    public async Task Retry_worker_skips_unconfirmed_wolt_rows_and_still_selects_wolt_when_the_hook_is_set()
    {
        var harness = await Harness.CreateAsync("Production");
        var wolt = IntegrationRow(harness, "wolt", "ord-wolt-retry");
        var deliverect = IntegrationRow(harness, "deliverect", "ord-deliverect-retry");
        harness.Db.OnlineOrderIntegrations.AddRange(wolt, deliverect);
        await harness.Db.SaveChangesAsync();
        var worker = harness.RetryWorker(harness.Processor(harness.Audit().Object));

        await worker.PollAsync(CancellationToken.None);
        harness.Db.ChangeTracker.Clear();
        var afterClosed = await harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Assert.All(afterClosed, row => Assert.Equal(OnlineOrderIntegrationStatuses.Received, row.Status));
        Assert.All(afterClosed, row => Assert.Null(row.NextAttemptAt));

        try
        {
            OnlineOrderIntegrationSignatureStatus.WoltConfirmedForTests = true;
            var due = await worker.ListDueIdsAsync(CancellationToken.None);
            Assert.Equal(new[] { wolt.Id }, due);
        }
        finally
        {
            OnlineOrderIntegrationSignatureStatus.WoltConfirmedForTests = false;
        }
    }

    [Fact]
    public async Task Wolt_token_exchange_and_refresh_do_not_reuse_or_log_the_refresh_token()
    {
        const string oldRefresh = "refresh-VISIBLE-secret";
        const string newRefresh = "refresh-rotated-secret";
        var seen = new List<string>();
        var logger = new ListLogger<WoltOAuthClient>();
        var handler = new StubHandler(async (request, ct) =>
        {
            var form = await request.Content!.ReadAsStringAsync(ct);
            var fields = ParseForm(form);
            if (fields.TryGetValue("refresh_token", out var refresh))
                seen.Add(refresh);
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal)
                && fields.GetValueOrDefault("grant_type") == "authorization_code")
            {
                Assert.False(fields.ContainsKey("refresh_token"));
                Assert.Equal("auth-code-1", fields["code"]);
                return JsonResponse(HttpStatusCode.OK, TokenJson(FakeJwt("venue-9"), "refresh-from-code", 3600));
            }

            if (fields.GetValueOrDefault("grant_type") == "refresh_token" && refresh == oldRefresh)
                return JsonResponse(HttpStatusCode.OK, TokenJson(FakeJwt("venue-9"), newRefresh, 0));
            if (refresh == newRefresh)
                return JsonResponse(HttpStatusCode.OK, TokenJson(FakeJwt("venue-9"), "refresh-third", 0));
            return JsonResponse(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}");
        });

        var options = Options.Create(new OnlineOrderIntegrationOptions
        {
            Wolt = new WoltIntegrationOptions
            {
                ClientId = "wolt-client",
                ClientSecret = "wolt-secret",
                RedirectUri = "https://admin.example/callback",
                TokenUrl = "https://wolt.test/oauth2/token",
                OrderBaseUrl = "https://wolt.test",
            },
        });
        var client = new WoltOAuthClient(HttpFactory(handler), options, TimeProvider.System, logger);
        var exchanged = await client.ExchangeAuthorizationCodeAsync("auth-code-1");
        Assert.True(exchanged.Succeeded);
        Assert.Equal("venue-9", exchanged.VenueId);
        Assert.DoesNotContain(logger.Lines, line => line.Contains("wolt-secret", StringComparison.Ordinal));

        logger.Lines.Clear();
        var rejected = await client.RefreshAsync(oldRefresh + "-missing");
        Assert.False(rejected.Succeeded);
        Assert.DoesNotContain(logger.Lines, line => line.Contains(oldRefresh, StringComparison.Ordinal));

        var harness = await Harness.CreateAsync();
        var protector = new OnlineOrderIntegrationTokenProtector(new EphemeralDataProtectionProvider());
        harness.Db.OnlineOrderIntegrationOAuthTokens.Add(new OnlineOrderIntegrationOAuthToken
        {
            Id = Guid.NewGuid(),
            TenantId = harness.Tenant.TenantId!.Value,
            Provider = "wolt",
            ExternalLocationId = "venue-9",
            AccessToken = protector.Protect("access-old"),
            RefreshToken = protector.Protect(oldRefresh),
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await harness.Db.SaveChangesAsync();
        var source = new WoltAccessTokenSource(
            harness.Factory,
            protector,
            client,
            new OnlineOrderIntegrationProviderService(harness.Factory, harness.Tenant, protector, TimeProvider.System),
            TimeProvider.System,
            NullLogger<WoltAccessTokenSource>.Instance);

        var first = await source.GetAccessTokenAsync(harness.Tenant.TenantId!.Value, "venue-9");
        var second = await source.GetAccessTokenAsync(harness.Tenant.TenantId!.Value, "venue-9");
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        Assert.Equal(new[] { oldRefresh, newRefresh }, seen.Where(value => value is oldRefresh or newRefresh).ToArray());
        harness.Db.ChangeTracker.Clear();
        var stored = await harness.Db.OnlineOrderIntegrationOAuthTokens.IgnoreQueryFilters().SingleAsync();
        Assert.DoesNotContain(oldRefresh, stored.RefreshToken, StringComparison.Ordinal);
        Assert.DoesNotContain(newRefresh, stored.RefreshToken, StringComparison.Ordinal);
        Assert.DoesNotContain("access-old", stored.AccessToken, StringComparison.Ordinal);
    }

    [Fact]
    public void Wolt_signature_accepts_hex_and_rejects_a_wrong_header_empty_secret_and_bad_hex()
    {
        const string secret = "example-hmac-sha256-wolt";
        const string body = "{\"type\":\"order.notification\"}";
        var hex = OnlineOrderIntegrationSignature.SignHex(body, secret);

        Assert.Equal("WOLT-SIGNATURE", OnlineOrderIntegrationSignatureStatus.WoltHeaderName);
        Assert.True(OnlineOrderIntegrationSignature.IsValid(body, secret, hex));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, secret, "sha256=" + hex + "ff"));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, "", hex));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, null, hex));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, secret, "not-hex"));
        Assert.False(OnlineOrderIntegrationSignature.IsValid(body, secret, ""));
    }

    [Fact]
    public async Task Wolt_webhook_rejects_a_bad_signature_before_location_lookup_and_writes_nothing()
    {
        var harness = await Harness.CreateAsync();
        var calls = 0;
        var wolt = harness.WoltNotifications(_ =>
        {
            calls++;
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        var body = Notification("missing-venue", "CREATED");

        var invalidJson = await wolt.AcceptAsync("{", wolt.Sign("{"), CancellationToken.None);
        var wrongSignature = await wolt.AcceptAsync(body, "00", CancellationToken.None);

        Assert.Equal(400, invalidJson.StatusCode);
        Assert.Equal(400, wrongSignature.StatusCode);
        Assert.Equal("SIGNATURE_INVALID", wrongSignature.Code);
        Assert.NotEqual(404, wrongSignature.StatusCode);
        Assert.Equal(0, calls);
        Assert.Empty(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Wolt_unknown_or_disabled_venue_returns_404_and_a_failed_fetch_writes_nothing()
    {
        var harness = await Harness.CreateAsync();
        var calls = 0;
        var wolt = harness.WoltNotifications(_ =>
        {
            calls++;
            return JsonResponse(HttpStatusCode.InternalServerError, "{}");
        });

        var unknownBody = Notification("missing-venue", "CREATED");
        var unknown = await wolt.AcceptAsync(unknownBody, wolt.Sign(unknownBody), CancellationToken.None);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Equal(0, calls);

        harness.AddWoltLocation(enabled: false);
        await harness.Db.SaveChangesAsync();
        var disabledBody = Notification("venue-1", "CREATED");
        var disabled = await wolt.AcceptAsync(disabledBody, wolt.Sign(disabledBody), CancellationToken.None);
        Assert.Equal(404, disabled.StatusCode);
        Assert.Equal(0, calls);

        harness.LocationForWolt!.Enabled = true;
        harness.AddWoltToken();
        await harness.Db.SaveChangesAsync();
        var fetchBody = Notification("venue-1", "CREATED");
        var fetch = await wolt.AcceptAsync(fetchBody, wolt.Sign(fetchBody), CancellationToken.None);
        Assert.Equal(502, fetch.StatusCode);
        Assert.Equal(1, calls);
        Assert.Empty(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Wolt_created_and_first_production_open_a_cart_and_later_production_does_not_fetch_again()
    {
        var harness = await Harness.CreateAsync();
        harness.AddWoltLocation(enabled: true);
        harness.AddWoltMapping();
        harness.AddWoltToken();
        await harness.Db.SaveChangesAsync();
        var gets = 0;
        var wolt = harness.WoltNotifications(orderId =>
        {
            gets++;
            return JsonResponse(HttpStatusCode.OK, OrderJson(orderId, 1000, "EUR", "plu-pizza", "+431234567"));
        });

        var createdBody = Notification("venue-1", "CREATED", "ord-created");
        var created = await wolt.AcceptAsync(createdBody, wolt.Sign(createdBody), CancellationToken.None);
        var productionBody = Notification("venue-1", "PRODUCTION", "ord-production");
        var production = await wolt.AcceptAsync(productionBody, wolt.Sign(productionBody), CancellationToken.None);
        var repeat = await wolt.AcceptAsync(productionBody, wolt.Sign(productionBody), CancellationToken.None);

        Assert.True(created.Succeeded);
        Assert.True(production.Succeeded);
        Assert.Equal(production.Id, repeat.Id);
        Assert.Equal(2, gets);
        Assert.Equal(2, harness.Db.Carts.IgnoreQueryFilters().Count());
        Assert.Empty(harness.Db.PaymentDetails.IgnoreQueryFilters());
        Assert.Equal(2, harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters().Count(row => row.Status == OnlineOrderIntegrationStatuses.CartLinked));
    }

    [Fact]
    public async Task Wolt_currency_total_product_and_phone_failures_do_not_open_a_cart()
    {
        var harness = await Harness.CreateAsync();
        harness.AddWoltLocation(enabled: true);
        harness.AddWoltMapping();
        harness.AddWoltToken();
        await harness.Db.SaveChangesAsync();
        var wolt = harness.WoltNotifications(orderId =>
        {
            var json = orderId switch
            {
                "ord-usd" => OrderJson(orderId, 1000, "USD", "plu-pizza", "+431234567"),
                "ord-mismatch" => OrderJson(orderId, 9999, "EUR", "plu-pizza", "+431234567"),
                "ord-pos" => OrderJson(orderId, 1000, "EUR", "", "+431234567"),
                _ => OrderJson(orderId, 1000, "EUR", "plu-pizza", ""),
            };
            return JsonResponse(HttpStatusCode.OK, json);
        });

        await wolt.AcceptAsync(Signed(wolt, "ord-usd"), wolt.Sign(Signed(wolt, "ord-usd")), CancellationToken.None);
        await wolt.AcceptAsync(Signed(wolt, "ord-mismatch"), wolt.Sign(Signed(wolt, "ord-mismatch")), CancellationToken.None);
        await wolt.AcceptAsync(Signed(wolt, "ord-pos"), wolt.Sign(Signed(wolt, "ord-pos")), CancellationToken.None);
        await wolt.AcceptAsync(Signed(wolt, "ord-phone"), wolt.Sign(Signed(wolt, "ord-phone")), CancellationToken.None);

        var rows = harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters().ToList();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal(OnlineOrderIntegrationStatuses.Failed, row.Status));
        Assert.Contains(rows, row => row.ExternalOrderId == "ord-usd" && row.LastError == WoltOrderMapper.CurrencyError);
        Assert.Contains(rows, row => row.ExternalOrderId == "ord-mismatch" && row.LastError != null && row.LastError.Contains("does not equal", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.ExternalOrderId == "ord-pos" && row.LastError == "One or more external products are not mapped.");
        Assert.Contains(rows, row => row.ExternalOrderId == "ord-phone" && row.LastError == "Customer name or phone is invalid.");
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
        Assert.Empty(harness.Db.OnlineOrders.IgnoreQueryFilters());
        Assert.DoesNotContain(rows, row => (row.RawPayload ?? string.Empty).Contains("+430000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Provider_configuration_hides_secrets_and_blocks_connect_until_enabled()
    {
        var harness = await Harness.CreateAsync();
        harness.SetVerticalProfile(VerticalProfileIds.Gastronomy);
        var providers = harness.ProviderService();
        var guard = harness.ProfileGuard();
        var admin = new AdminOnlineOrderIntegrationProvidersController(providers, guard);
        var tenant = new TenantOnlineOrderIntegrationProvidersController(providers, guard);

        var missing = await admin.Get("wolt", CancellationToken.None);
        var missingBody = Assert.IsType<OkObjectResult>(missing).Value as OnlineOrderIntegrationProviderAdminDto;
        Assert.NotNull(missingBody);
        Assert.Equal(OnlineOrderIntegrationProviderStatuses.Missing, missingBody!.CredentialStatus);
        Assert.False(missingBody.Enabled);

        var saved = await admin.Upsert(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "wolt",
            Enabled = true,
            ClientId = "partner-client",
            ClientSecret = "partner-secret",
        }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(saved);

        var configured = await admin.Get("wolt", CancellationToken.None);
        var configuredBody = Assert.IsType<OkObjectResult>(configured).Value as OnlineOrderIntegrationProviderAdminDto;
        Assert.NotNull(configuredBody);
        Assert.True(configuredBody!.Enabled);
        Assert.Equal(OnlineOrderIntegrationProviderStatuses.Configured, configuredBody.CredentialStatus);
        Assert.DoesNotContain("partner-secret", System.Text.Json.JsonSerializer.Serialize(configuredBody), StringComparison.Ordinal);

        var stored = Assert.Single(harness.Db.OnlineOrderIntegrationProviders);
        Assert.DoesNotContain("partner-secret", stored.ClientSecret, StringComparison.Ordinal);
        Assert.DoesNotContain("partner-client", stored.ClientId, StringComparison.Ordinal);

        var tenantView = await tenant.Get("wolt", CancellationToken.None);
        var tenantBody = Assert.IsType<OkObjectResult>(tenantView).Value as OnlineOrderIntegrationProviderTenantDto;
        Assert.NotNull(tenantBody);
        Assert.True(tenantBody!.Enabled);
        Assert.False(tenantBody.Connected);
        Assert.DoesNotContain("partner-secret", System.Text.Json.JsonSerializer.Serialize(tenantBody), StringComparison.Ordinal);

        harness.SetVerticalProfile(VerticalProfileIds.HairSalon);
        AssertProfileForbidden(await admin.Get("wolt", CancellationToken.None));
        AssertProfileForbidden(await tenant.Get("wolt", CancellationToken.None));
        AssertProfileForbidden(await admin.Upsert(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "wolt",
            Enabled = false,
            ClientId = "other",
            ClientSecret = "other-secret",
        }, CancellationToken.None));

        var connect = new WoltIntegrationConnectService(
            harness.Factory,
            harness.Tenant,
            Options.Create(new OnlineOrderIntegrationOptions
            {
                Wolt = new WoltIntegrationOptions
                {
                    RedirectUri = "https://admin.example/callback",
                    AuthorizationEndpoint = "https://wolt.test/oauth/authorize",
                    TokenUrl = "https://wolt.test/oauth2/token",
                },
            }),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            new Mock<IWoltOAuthClient>().Object,
            providers,
            harness.TokenProtector,
            TimeProvider.System,
            NullLogger<WoltIntegrationConnectService>.Instance);
        harness.SetVerticalProfile(VerticalProfileIds.Gastronomy);
        var open = await connect.BeginAsync("user-1", CancellationToken.None);
        Assert.True(open.Succeeded);
        Assert.Contains("client_id=partner-client", open.AuthorizationUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("partner-secret", open.AuthorizationUrl, StringComparison.Ordinal);

        await providers.UpsertAsync(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "wolt",
            Enabled = false,
        }, CancellationToken.None);
        var blocked = await connect.BeginAsync("user-1", CancellationToken.None);
        Assert.False(blocked.Succeeded);
        Assert.Equal(409, blocked.StatusCode);
        Assert.Equal(OnlineOrderIntegrationProviderStatuses.DisabledCode, blocked.Code);
    }

    [Fact]
    public async Task Tenant_admin_and_super_admin_complete_wolt_callback_for_the_ambient_tenant()
    {
        var connectPermission = Assert.Single(typeof(AdminWoltIntegrationController)
            .GetMethod(nameof(AdminWoltIntegrationController.Connect))!
            .GetCustomAttributes<HasPermissionAttribute>());
        var callbackPermission = Assert.Single(typeof(AdminWoltIntegrationController)
            .GetMethod(nameof(AdminWoltIntegrationController.Callback))!
            .GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal(AppPermissions.DigitalOrdersManage, connectPermission.Permission);
        Assert.Equal(AppPermissions.DigitalOrdersManage, callbackPermission.Permission);
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.DigitalOrdersManage));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.DigitalOrdersManage));

        var harness = await Harness.CreateAsync();
        harness.SetVerticalProfile(VerticalProfileIds.Gastronomy);
        var tenantId = harness.Tenant.TenantId!.Value;
        var venues = new Queue<string>(new[] { "venue-tenant", "venue-super" });
        var connect = await WoltConnectAsync(harness, venues);
        var controller = WoltController(harness, connect);

        controller.HttpContext.User = Actor("tenant-admin");
        var started = await controller.Connect(CancellationToken.None);
        var startedBody = Assert.IsType<OkObjectResult>(started).Value;
        var authorizationUrl = startedBody!.GetType().GetProperty("authorizationUrl")!.GetValue(startedBody) as string;
        Assert.Contains("client_id=partner-client", authorizationUrl, StringComparison.Ordinal);
        var completed = await controller.Callback("code-tenant", StateOf(authorizationUrl!), CancellationToken.None);
        Assert.IsType<OkObjectResult>(completed);

        var superTenantId = Guid.NewGuid();
        harness.Tenant.TenantId = superTenantId;
        harness.SetVerticalProfile(VerticalProfileIds.Gastronomy);
        controller.HttpContext.User = Actor("super-admin");
        var superStarted = await controller.Connect(CancellationToken.None);
        var superBody = Assert.IsType<OkObjectResult>(superStarted).Value;
        var superUrl = superBody!.GetType().GetProperty("authorizationUrl")!.GetValue(superBody) as string;
        var superCompleted = await controller.Callback("code-super", StateOf(superUrl!), CancellationToken.None);
        Assert.IsType<OkObjectResult>(superCompleted);

        var tokens = await harness.Db.OnlineOrderIntegrationOAuthTokens.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Contains(tokens, token => token.TenantId == tenantId && token.ExternalLocationId == "venue-tenant");
        Assert.Contains(tokens, token => token.TenantId == superTenantId && token.ExternalLocationId == "venue-super");
        Assert.All(tokens, token => Assert.NotEqual(Guid.Empty, token.TenantId));

        harness.Tenant.TenantId = tenantId;
        harness.SetVerticalProfile(VerticalProfileIds.HairSalon);
        controller.HttpContext.User = Actor("tenant-admin");
        AssertProfileForbidden(await controller.Connect(CancellationToken.None));
        AssertProfileForbidden(await controller.Callback("code-salon", "state-salon", CancellationToken.None));
        Assert.Equal(2, await harness.Db.OnlineOrderIntegrationOAuthTokens.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Gastronomy_marketplace_admin_endpoints_succeed_and_other_verticals_are_forbidden()
    {
        var harness = await Harness.CreateAsync();
        harness.SetVerticalProfile(VerticalProfileIds.Gastronomy);
        var guard = harness.ProfileGuard();
        var locations = new AdminOnlineOrderIntegrationLocationsController(harness.Locations, guard);
        var mappings = new AdminOnlineOrderIntegrationProductMappingsController(harness.ProductMappings, guard);
        var connect = new Mock<IWoltIntegrationConnectService>();
        connect.Setup(service => service.BeginAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WoltConnectStartResult.Ok("https://wolt.test/authorize"));
        connect.Setup(service => service.CompleteAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, Guid.NewGuid()));
        var wolt = new AdminWoltIntegrationController(connect.Object, guard)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var location = await locations.Upsert(new UpsertOnlineOrderIntegrationLocationRequest
        {
            Provider = "deliverect",
            ExternalLocationId = "loc-gastro",
            Enabled = true,
        }, CancellationToken.None);
        var mapping = await mappings.Upsert(new UpsertOnlineOrderProductMappingRequest
        {
            Provider = "deliverect",
            ExternalProductId = "plu-gastro",
            ProductId = harness.Food.Id,
        }, CancellationToken.None);
        var started = await wolt.Connect(CancellationToken.None);

        Assert.IsType<OkObjectResult>(location);
        Assert.IsType<OkObjectResult>(mapping);
        Assert.IsType<OkObjectResult>(started);

        harness.SetVerticalProfile(VerticalProfileIds.HairSalon);
        var deniedLocation = await locations.Upsert(new UpsertOnlineOrderIntegrationLocationRequest
        {
            Provider = "deliverect",
            ExternalLocationId = "loc-salon",
            Enabled = true,
        }, CancellationToken.None);
        var deniedMapping = await mappings.Upsert(new UpsertOnlineOrderProductMappingRequest
        {
            Provider = "deliverect",
            ExternalProductId = "plu-salon",
            ProductId = harness.Food.Id,
        }, CancellationToken.None);
        var deniedConnect = await wolt.Connect(CancellationToken.None);
        var deniedCallback = await wolt.Callback("code", "state", CancellationToken.None);

        AssertProfileForbidden(deniedLocation);
        AssertProfileForbidden(deniedMapping);
        AssertProfileForbidden(deniedConnect);
        AssertProfileForbidden(deniedCallback);
        Assert.DoesNotContain(
            harness.Db.OnlineOrderIntegrationLocations.IgnoreQueryFilters(),
            row => row.ExternalLocationId == "loc-salon");
        Assert.DoesNotContain(
            harness.Db.OnlineOrderProductMappings.IgnoreQueryFilters(),
            row => row.ExternalProductId == "plu-salon");
        connect.Verify(
            service => service.BeginAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Non_gastronomy_webhook_with_a_valid_signature_returns_404_and_writes_nothing()
    {
        var harness = await Harness.CreateAsync();
        harness.SetVerticalProfile(VerticalProfileIds.HairSalon);
        harness.AddWoltLocation(enabled: true);
        await harness.Db.SaveChangesAsync();
        var calls = 0;
        var wolt = harness.WoltNotifications(_ =>
        {
            calls++;
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        var body = Notification("venue-1", "CREATED", "ord-salon");
        var result = await wolt.AcceptAsync(body, wolt.Sign(body), CancellationToken.None);

        var deliverect = harness.Webhooks(harness.Audit().Object, new NoopActivity());
        var deliverectBody = harness.Body("ord-salon-deliverect");
        var invalid = await deliverect.AcceptAsync("deliverect", deliverectBody, "00", CancellationToken.None);
        var valid = await deliverect.AcceptAsync("deliverect", deliverectBody, harness.Sign(deliverectBody), CancellationToken.None);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("LOCATION_NOT_FOUND", result.Code);
        Assert.Equal(0, calls);
        Assert.Equal(400, invalid.StatusCode);
        Assert.Equal(404, valid.StatusCode);
        Assert.Empty(harness.Db.OnlineOrderIntegrations.IgnoreQueryFilters());
        Assert.Empty(harness.Db.Carts.IgnoreQueryFilters());
    }

    private static async Task<WoltIntegrationConnectService> WoltConnectAsync(Harness harness, Queue<string> venues)
    {
        var saved = await harness.ProviderService().UpsertAsync(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "wolt",
            Enabled = true,
            ClientId = "partner-client",
            ClientSecret = "partner-secret",
        }, CancellationToken.None);
        if (!saved.Succeeded)
            throw new InvalidOperationException(saved.Error);
        var handler = new StubHandler((_, _) =>
        {
            var venue = venues.Dequeue();
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, TokenJson(FakeJwt(venue), "refresh-" + venue, 3600)));
        });
        var options = Options.Create(new OnlineOrderIntegrationOptions
        {
            Wolt = new WoltIntegrationOptions
            {
                RedirectUri = "https://admin.example/callback",
                AuthorizationEndpoint = "https://wolt.test/oauth/authorize",
                TokenUrl = "https://wolt.test/oauth2/token",
            },
        });
        return new WoltIntegrationConnectService(
            harness.Factory,
            harness.Tenant,
            options,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            new WoltOAuthClient(HttpFactory(handler), options, TimeProvider.System, NullLogger<WoltOAuthClient>.Instance),
            harness.ProviderService(),
            harness.TokenProtector,
            TimeProvider.System,
            NullLogger<WoltIntegrationConnectService>.Instance);
    }

    private static AdminWoltIntegrationController WoltController(Harness harness, IWoltIntegrationConnectService connect)
    {
        var http = new DefaultHttpContext();
        return new AdminWoltIntegrationController(connect, harness.ProfileGuard())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static ClaimsPrincipal Actor(string userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"));

    private static string StateOf(string authorizationUrl)
    {
        var query = new Uri(authorizationUrl).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "state")
                return Uri.UnescapeDataString(parts[1]);
        }

        throw new InvalidOperationException("OAuth state was missing.");
    }

    private static void AssertProfileForbidden(IActionResult result)
    {
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(
            ProfileEndpointDisabledException.Code,
            forbidden.Value!.GetType().GetProperty("code")!.GetValue(forbidden.Value));
    }

    private static OnlineOrderIntegration IntegrationRow(Harness harness, string provider, string externalOrderId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = harness.Tenant.TenantId!.Value,
            Provider = provider,
            ExternalOrderId = externalOrderId,
            ExternalLocationId = "venue-1",
            Status = OnlineOrderIntegrationStatuses.Received,
            RawPayload = "{}",
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 0,
            IsActive = true,
        };

    private static string Notification(string venueId, string status, string orderId = "ord-w1") =>
        "{\"id\":\"evt-1\",\"type\":\"order.notification\",\"order\":{\"id\":\"" + orderId + "\",\"venue_id\":\"" + venueId + "\",\"status\":\"" + status + "\"}}";

    private static string Signed(WoltTestClient wolt, string orderId) =>
        Notification("venue-1", "CREATED", orderId);

    private static string OrderJson(string orderId, int basketCents, string currency, string posId, string phone) =>
        "{\"id\":\"" + orderId + "\",\"venue\":{\"id\":\"venue-1\"},\"consumer_name\":\"Max Mustermann\",\"consumer_phone_number\":\"" + phone + "\",\"basket_price\":{\"total\":{\"amount\":" + basketCents.ToString() + ",\"currency\":\"" + currency + "\"}},\"items\":[{\"pos_id\":\"" + posId + "\",\"name\":\"Pizza\",\"count\":1,\"item_price\":{\"unit_price\":{\"amount\":1000,\"currency\":\"" + currency + "\"}}}]}";

    private static string TokenJson(string accessToken, string refreshToken, int expiresIn) =>
        "{\"access_token\":\"" + accessToken + "\",\"refresh_token\":\"" + refreshToken + "\",\"expires_in\":" + expiresIn.ToString() + ",\"token_type\":\"bearer\"}";

    private static string FakeJwt(string venueId)
    {
        static string B64(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return B64("{\"alg\":\"none\"}") + "." + B64("{\"integration\":{\"venue_id\":\"" + venueId + "\"}}") + ".sig";
    }

    private static Dictionary<string, string> ParseForm(string form) =>
        form.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                StringComparer.Ordinal);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static IHttpClientFactory HttpFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient(WoltOAuthClient.HttpClientName)).Returns(client);
        return factory.Object;
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
                ProfileGuard(),
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

        public OnlineOrderIntegrationTokenProtector TokenProtector { get; } =
            new(new EphemeralDataProtectionProvider());

        public OnlineOrderIntegrationLocation? LocationForWolt { get; private set; }

        public const string WoltSecret = "wolt-client-secret";

        public void AddWoltLocation(bool enabled)
        {
            LocationForWolt = new OnlineOrderIntegrationLocation
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant.TenantId!.Value,
                Provider = "wolt",
                ExternalLocationId = "venue-1",
                ClaimingUserId = User.Id,
                Enabled = enabled,
                DeliveryFeeProductId = DeliveryProduct.Id,
                ServiceFeeProductId = ServiceProduct.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            Db.OnlineOrderIntegrationLocations.Add(LocationForWolt);
        }

        public void AddWoltMapping()
        {
            Db.OnlineOrderProductMappings.Add(new OnlineOrderProductMapping
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant.TenantId!.Value,
                Provider = "wolt",
                ExternalProductId = "plu-pizza",
                ProductId = Food.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        public void AddWoltToken()
        {
            Db.OnlineOrderIntegrationOAuthTokens.Add(new OnlineOrderIntegrationOAuthToken
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant.TenantId!.Value,
                Provider = "wolt",
                ExternalLocationId = "venue-1",
                AccessToken = TokenProtector.Protect("access-live"),
                RefreshToken = TokenProtector.Protect("refresh-live"),
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        }

        public OnlineOrderIntegrationProcessor Processor(IAuditLogService audit)
        {
            var notifications = new Mock<IOnlineOrderNotificationService>();
            notifications
                .Setup(n => n.SendOrderConfirmationAsync(It.IsAny<OnlineOrder>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return new OnlineOrderIntegrationProcessor(
                Factory,
                Tenant,
                new OrderIntegrationService(
                    Factory,
                    Tenant,
                    notifications.Object,
                    TimeProvider.System,
                    NullLogger<OrderIntegrationService>.Instance),
                audit,
                new NoopActivity(),
                TenantTestDoubles.HostEnvironmentReturning(EnvironmentName),
                TimeProvider.System,
                NullLogger<OnlineOrderIntegrationProcessor>.Instance);
        }

        public OnlineOrderIntegrationRetryHostedService RetryWorker(IOnlineOrderIntegrationProcessor processor)
        {
            var services = new ServiceCollection();
            services.AddSingleton(Factory);
            services.AddSingleton(processor);
            var provider = services.BuildServiceProvider();
            return new OnlineOrderIntegrationRetryHostedService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                TenantTestDoubles.HostEnvironmentReturning(EnvironmentName),
                TimeProvider.System,
                NullLogger<OnlineOrderIntegrationRetryHostedService>.Instance);
        }

        public WoltTestClient WoltNotifications(Func<string, HttpResponseMessage> onGet)
        {
            var handler = new StubHandler((request, _) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    var id = Uri.UnescapeDataString(request.RequestUri!.Segments[^1].Trim('/'));
                    return Task.FromResult(onGet(id));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });
            });
            var options = Options.Create(new OnlineOrderIntegrationOptions
            {
                Wolt = new WoltIntegrationOptions
                {
                    ClientId = "wolt-client",
                    ClientSecret = WoltSecret,
                    TokenUrl = "https://wolt.test/oauth2/token",
                    OrderBaseUrl = "https://wolt.test",
                },
            });
            var oauth = new WoltOAuthClient(HttpFactory(handler), options, TimeProvider.System, NullLogger<WoltOAuthClient>.Instance);
            var providers = ProviderService();
            var tokens = new WoltAccessTokenSource(Factory, TokenProtector, oauth, providers, TimeProvider.System, NullLogger<WoltAccessTokenSource>.Instance);
            var webhooks = Webhooks(Audit().Object, new NoopActivity());
            if (!Db.OnlineOrderIntegrationProviders.Any(p => p.Provider == "wolt"))
            {
                Db.OnlineOrderIntegrationProviders.Add(new OnlineOrderIntegrationProvider
                {
                    Id = Guid.NewGuid(),
                    Provider = "wolt",
                    Enabled = true,
                    ClientId = TokenProtector.Protect("wolt-client"),
                    ClientSecret = TokenProtector.Protect(WoltSecret),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                });
                Db.SaveChanges();
            }

            var service = new WoltOrderNotificationService(
                Factory,
                Tenant,
                tokens,
                oauth,
                webhooks,
                ProfileGuard(),
                providers,
                TimeProvider.System,
                NullLogger<WoltOrderNotificationService>.Instance);
            return new WoltTestClient(service, WoltSecret);
        }

        public OnlineOrderIntegrationProviderService ProviderService() =>
            new(Factory, Tenant, TokenProtector, TimeProvider.System);

        public IVerticalProfileGuard ProfileGuard()
        {
            var cache = new Mock<ICacheService>();
            var profiles = new VerticalProfileService(
                Db,
                Tenant,
                Audit().Object,
                NullLogger<VerticalProfileService>.Instance,
                new VerticalProfileRegistry(Db),
                cache.Object);
            return new VerticalProfileGuard(profiles, NullLogger<VerticalProfileGuard>.Instance);
        }

        public void SetVerticalProfile(string? profileId)
        {
            var row = Db.CompanySettings.IgnoreQueryFilters().FirstOrDefault(s => s.TenantId == Tenant.TenantId);
            if (row is null)
            {
                row = new CompanySettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = Tenant.TenantId!.Value,
                    CompanyName = "Demo",
                    CompanyAddress = "Wien",
                    CompanyTaxNumber = "ATU12345678",
                    Currency = "EUR",
                    Country = "AT",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                };
                Db.CompanySettings.Add(row);
            }

            row.VerticalProfileId = profileId;
            Db.SaveChanges();
        }

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

    private sealed class WoltTestClient
    {
        private readonly WoltOrderNotificationService _service;
        private readonly string _secret;

        public WoltTestClient(WoltOrderNotificationService service, string secret)
        {
            _service = service;
            _secret = secret;
        }

        public string Sign(string body) => OnlineOrderIntegrationSignature.SignHex(body, _secret);

        public Task<OnlineOrderIntegrationCommandResult> AcceptAsync(string body, string signature, CancellationToken ct) =>
            _service.AcceptAsync(body, signature, ct);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

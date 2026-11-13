using System.Net;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FoodoraAuthClientTests
{
    [Fact]
    public async Task Client_credentials_post_has_no_basic_auth_and_the_token_is_cached_until_expiry()
    {
        var posts = 0;
        var bodies = new List<string>();
        var sawAuthorization = false;
        var handler = new StubHandler(async (request, ct) =>
        {
            posts++;
            sawAuthorization |= request.Headers.Authorization is not null;
            bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            Assert.EndsWith("/v2/oauth/token", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Json(HttpStatusCode.OK, """{"access_token":"access-live","expires_in":7200,"token_type":"bearer"}""");
        });
        var start = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider { Now = start };
        var (source, db, tenantId) = await SourceAsync(handler, clock, "client-1", "secret-1");

        var first = await source.GetAccessAsync(tenantId);
        var second = await source.GetAccessAsync(tenantId);
        clock.Now = start.AddSeconds(7200);
        var third = await source.GetAccessAsync(tenantId);

        Assert.Equal("access-live", first!.AccessToken);
        Assert.Equal("chain-1", first.ChainId);
        Assert.Equal("access-live", second!.AccessToken);
        Assert.Equal("access-live", third!.AccessToken);
        Assert.Equal(2, posts);
        Assert.False(sawAuthorization);
        Assert.All(bodies, body =>
        {
            Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
            Assert.Contains("client_id=client-1", body, StringComparison.Ordinal);
            Assert.Contains("client_secret=secret-1", body, StringComparison.Ordinal);
            Assert.DoesNotContain("refresh_token", body, StringComparison.Ordinal);
        });
        db.ChangeTracker.Clear();
        var stored = await db.OnlineOrderIntegrationTenantCredentials.IgnoreQueryFilters().SingleAsync();
        Assert.DoesNotContain("secret-1", stored.ClientSecret, StringComparison.Ordinal);
        Assert.DoesNotContain("access-live", stored.AccessToken, StringComparison.Ordinal);
        Assert.DoesNotContain("client-1", stored.ClientId, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Token_errors_do_not_log_the_client_secret(HttpStatusCode status)
    {
        var logger = new ListLogger<FoodoraAuthClient>();
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        var client = Client(handler, logger, TimeProvider.System);
        var result = await client.RequestClientCredentialsAsync("client-1", "secret-1");

        Assert.False(result.Succeeded);
        Assert.Equal((int)status, result.StatusCode);
        Assert.DoesNotContain(logger.Lines, line => line.Contains("secret-1", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Lines, line => line.Contains("client-1", StringComparison.Ordinal));
    }

    [Fact]
    public void Basic_authorization_matches_the_portal_token()
    {
        const string token = "portal-user:portal-pass";
        var basic = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(token));

        Assert.True(FoodoraBasicAuth.Matches(basic, token));
        Assert.True(FoodoraBasicAuth.Matches(basic, basic));
        Assert.False(FoodoraBasicAuth.Matches(basic, "other-secret"));
        Assert.False(FoodoraBasicAuth.Matches(null, token));
        Assert.False(FoodoraBasicAuth.Matches("", token));
    }

    [Fact]
    public async Task Missing_authorization_is_rejected_before_the_body_is_read()
    {
        var service = new FoodoraOrderNotificationService(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            TimeProvider.System,
            NullLogger<FoodoraOrderNotificationService>.Instance);
        var body = new ThrowingStream();

        var result = await service.AcceptAsync("  ", body, CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("SIGNATURE_INVALID", result.Code);
        Assert.False(body.WasRead);
    }

    [Fact]
    public async Task Super_admin_foodora_row_stores_no_partner_secret_and_reports_tenant_ownership()
    {
        var (db, options, tenant, protector) = await DatabaseAsync();
        var providers = new OnlineOrderIntegrationProviderService(
            TenantTestDoubles.DbContextFactoryForTests(options, tenant),
            tenant,
            protector,
            TimeProvider.System);

        var saved = await providers.UpsertAsync(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "foodora",
            Enabled = true,
            ClientId = "should-not-stick",
            ClientSecret = "should-not-stick",
        });
        var admin = await providers.GetAdminAsync("foodora");

        Assert.True(saved.Succeeded);
        Assert.Equal(OnlineOrderIntegrationCredentialOwners.Tenant, admin.Body!.CredentialStatus);
        Assert.True(admin.Body.Enabled);
        var row = await db.OnlineOrderIntegrationProviders.SingleAsync();
        Assert.Equal(string.Empty, row.ClientId);
        Assert.Equal(string.Empty, row.ClientSecret);
        Assert.Equal(OnlineOrderIntegrationCredentialOwners.Tenant, row.CredentialOwner);
        Assert.DoesNotContain("should-not-stick", JsonSerializer.Serialize(admin.Body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_credentials_are_ciphertext_and_a_disabled_provider_rejects_the_save()
    {
        var (db, options, tenant, protector) = await DatabaseAsync();
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, tenant);
        var providers = new OnlineOrderIntegrationProviderService(factory, tenant, protector, TimeProvider.System);
        var credentials = new FoodoraTenantCredentialService(factory, tenant, protector, TimeProvider.System);
        await providers.UpsertAsync(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "foodora",
            Enabled = false,
        });

        var blocked = await credentials.UpsertAsync(new UpsertFoodoraTenantCredentialRequest
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            ChainId = "chain-1",
            WebhookToken = "portal-user:portal-pass",
        });
        Assert.Equal(409, blocked.StatusCode);
        Assert.Equal(OnlineOrderIntegrationProviderStatuses.DisabledCode, blocked.Code);

        await providers.UpsertAsync(new UpsertOnlineOrderIntegrationProviderRequest
        {
            Provider = "foodora",
            Enabled = true,
        });
        var saved = await credentials.UpsertAsync(new UpsertFoodoraTenantCredentialRequest
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            ChainId = "chain-1",
            WebhookToken = "portal-user:portal-pass",
        });
        var status = await credentials.GetAsync();

        Assert.True(saved.Succeeded);
        Assert.Equal(OnlineOrderIntegrationProviderStatuses.Configured, status.Body!.CredentialStatus);
        Assert.True(status.Body.ProviderEnabled);
        var stored = await db.OnlineOrderIntegrationTenantCredentials.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("chain-1", stored.ChainId);
        Assert.DoesNotContain("secret-1", stored.ClientSecret, StringComparison.Ordinal);
        Assert.DoesNotContain("portal-user:portal-pass", stored.WebhookToken, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-1", JsonSerializer.Serialize(status.Body), StringComparison.Ordinal);
        Assert.DoesNotContain("portal-pass", JsonSerializer.Serialize(status.Body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_gastronomy_foodora_credentials_return_403()
    {
        var guard = new Mock<IVerticalProfileGuard>();
        guard.Setup(item => item.EnforceEndpointAsync(VerticalProfileGuard.MarketplaceOrdersFeature, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProfileEndpointDisabledException(VerticalProfileGuard.MarketplaceOrdersFeature));
        var controller = new AdminFoodoraCredentialsController(Mock.Of<IFoodoraTenantCredentialService>(), guard.Object);

        var get = await controller.Get(CancellationToken.None);
        var put = await controller.Upsert(new UpsertFoodoraTenantCredentialRequest(), CancellationToken.None);

        Assert.Equal(403, Assert.IsType<ObjectResult>(get).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(put).StatusCode);
    }

    private static async Task<(FoodoraAccessTokenSource Source, AppDbContext Db, Guid TenantId)> SourceAsync(
        HttpMessageHandler handler,
        TimeProvider clock,
        string clientId,
        string clientSecret)
    {
        var (db, options, tenant, protector) = await DatabaseAsync();
        db.OnlineOrderIntegrationTenantCredentials.Add(new OnlineOrderIntegrationTenantCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId!.Value,
            Provider = "foodora",
            ClientId = protector.Protect(clientId),
            ClientSecret = protector.Protect(clientSecret),
            ChainId = "chain-1",
            WebhookToken = protector.Protect("hook"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var source = new FoodoraAccessTokenSource(
            TenantTestDoubles.DbContextFactoryForTests(options, tenant),
            protector,
            Client(handler, NullLogger<FoodoraAuthClient>.Instance, clock),
            clock,
            NullLogger<FoodoraAccessTokenSource>.Instance);
        return (source, db, tenant.TenantId!.Value);
    }

    private static FoodoraAuthClient Client(HttpMessageHandler handler, ILogger<FoodoraAuthClient> logger, TimeProvider clock)
    {
        var http = new Mock<IHttpClientFactory>();
        http.Setup(item => item.CreateClient(FoodoraAuthClient.HttpClientName)).Returns(new HttpClient(handler));
        return new FoodoraAuthClient(
            http.Object,
            Options.Create(new OnlineOrderIntegrationOptions
            {
                Foodora = new FoodoraIntegrationOptions
                {
                    TokenUrl = "https://foodora.test/v2/oauth/token",
                    ApiBaseUrl = "https://foodora.test",
                },
            }),
            clock,
            logger);
    }

    private static async Task<(AppDbContext Db, DbContextOptions<AppDbContext> Options, TenantTestDoubles.MutableTenantAccessor Tenant, OnlineOrderIntegrationTokenProtector Protector)> DatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = new TenantTestDoubles.MutableTenantAccessor(Guid.NewGuid());
        var db = new AppDbContext(options, tenant);
        await db.Database.EnsureCreatedAsync();
        return (db, options, tenant, new OnlineOrderIntegrationTokenProtector(new EphemeralDataProtectionProvider()));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
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
            public void Dispose() { }
        }
    }
}

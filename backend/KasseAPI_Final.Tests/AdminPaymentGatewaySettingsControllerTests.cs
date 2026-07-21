using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminPaymentGatewaySettingsControllerTests
{
    [Fact]
    public async Task Get_returns_status_without_secrets()
    {
        var tenantId = Guid.NewGuid();
        var (controller, _) = CreateController(
            tenantId,
            new PaymentGatewayOptions
            {
                Provider = "Stripe",
                Stripe = new PaymentGatewayStripeOptions
                {
                    ApiKey = "sk_test_secret_should_not_leak",
                    WebhookSecret = "whsec_secret_should_not_leak"
                }
            });

        var result = await controller.Get(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PaymentGatewaySettingsDto>(ok.Value);

        Assert.True(dto.IsStripeProvider);
        Assert.True(dto.ApiKeyConfigured);
        Assert.True(dto.WebhookSecretConfigured);
        Assert.Equal("/api/webhooks/stripe", dto.WebhookPath);
        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("sk_test", json);
        Assert.DoesNotContain("whsec", json);
    }

    [Fact]
    public async Task Put_updates_online_methods()
    {
        var tenantId = Guid.NewGuid();
        var (controller, db) = CreateController(tenantId, new PaymentGatewayOptions { Provider = "Mock" });

        var result = await controller.Put(
            new UpdatePaymentGatewaySettingsRequestDto
            {
                OnlinePaymentMethods = ["card", "paypal", "bank"]
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PaymentGatewaySettingsDto>(ok.Value);
        Assert.Contains("paypal", dto.OnlinePaymentMethods);

        db.ChangeTracker.Clear();
        var settings = await db.SystemSettings
            .IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenantId);
        Assert.Equal("card,paypal,bank", settings.OnlineCheckoutPaymentMethods);
    }

    private static (AdminPaymentGatewaySettingsController Controller, AppDbContext Db) CreateController(
        Guid tenantId,
        PaymentGatewayOptions gatewayOptions)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        db.SystemSettings.Add(new SystemSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyName = "Test",
            CompanyAddress = "Addr",
            CompanyTaxNumber = "ATU12345678",
            DefaultLanguage = "de",
            DefaultCurrency = "EUR",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            OnlineCheckoutPaymentMethods = "card,cash,online"
        });
        db.SaveChanges();

        var factory = new Factory(options);
        var tenant = new StubTenantAccessor(tenantId);
        var controller = new AdminPaymentGatewaySettingsController(
            new OptionsMonitorStub(gatewayOptions),
            factory,
            tenant,
            NullLogger<AdminPaymentGatewaySettingsController>.Instance);

        return (controller, db);
    }

    private sealed class StubTenantAccessor : ICurrentTenantAccessor
    {
        public StubTenantAccessor(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; set; }
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<PaymentGatewayOptions>
    {
        public OptionsMonitorStub(PaymentGatewayOptions current) => CurrentValue = current;
        public PaymentGatewayOptions CurrentValue { get; }
        public PaymentGatewayOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<PaymentGatewayOptions, string?> listener) => null;
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));
    }
}

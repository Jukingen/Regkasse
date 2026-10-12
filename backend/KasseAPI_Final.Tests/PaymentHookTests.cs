using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using CacheSettings = KasseAPI_Final.Configuration.CacheSettings;

namespace KasseAPI_Final.Tests;

public sealed class PaymentHookTests
{
    [Fact]
    public async Task CreatePayment_WhenExtensionEnabled_RunsHookAndStillSigns()
    {
        var probe = new PaymentProbeExtension();
        await using var ctx = CreateContext(out var registry, probe);
        SeedEnabled(ctx, probe.FeatureName);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                TseMock = tse,
                ExtensionRegistry = registry,
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.False(string.IsNullOrEmpty(result.Payment!.TseSignature));
        Assert.Equal(
            [TenantExtensionHooks.PaymentBeforeSign, TenantExtensionHooks.PaymentAfterPersist],
            probe.Hooks);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_WhenExtensionDisabled_DoesNotRunHook()
    {
        var probe = new PaymentProbeExtension();
        await using var ctx = CreateContext(out var registry, probe);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { ExtensionRegistry = registry });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.False(string.IsNullOrEmpty(result.Payment!.TseSignature));
        Assert.Empty(probe.Hooks);
    }

    [Fact]
    public async Task CreatePayment_WhenExtensionFails_ReturnsExtensionFailedAndDoesNotSign()
    {
        var probe = new PaymentProbeExtension { Fail = true };
        await using var ctx = CreateContext(out var registry, probe);
        SeedEnabled(ctx, probe.FeatureName);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                TseMock = tse,
                ExtensionRegistry = registry,
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ExtensionFailedException.ErrorCode, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePayment_WhenExtensionFails_ControllerReturns400()
    {
        var payment = new Mock<IPaymentService>();
        payment.Setup(service => service.CreatePaymentAsync(It.IsAny<CreatePaymentRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = false,
                Message = "campaign blocked",
                DiagnosticCode = ExtensionFailedException.ErrorCode,
                Errors = { "campaign blocked" },
            });

        var controller = new PaymentController(
            payment.Object,
            Mock.Of<IPaymentMethodCatalogService>(),
            Mock.Of<IPaymentHistoryService>(),
            Mock.Of<IQrImageService>(),
            null!,
            Mock.Of<Microsoft.AspNetCore.Authorization.IAuthorizationService>(),
            Mock.Of<IRksvEnvironmentService>(),
            NullLogger<PaymentController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, PaymentServiceCoverageHarness.CashierId)],
                        "Test")),
                },
            },
        };

        var response = await controller.CreatePayment(
            PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains(ExtensionFailedException.ErrorCode, json, StringComparison.Ordinal);
    }

    private static AppDbContext CreateContext(out ITenantExtensionRegistry registry, PaymentProbeExtension probe)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PayHook_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
        registry = new TenantExtensionRegistry(
            [probe],
            TenantTestDoubles.DbContextFactoryForTests(options, TenantTestDoubles.TenantAccessorReturning(null)),
            new MemoryCache(new MemoryCacheOptions()),
            new PassThroughCache(),
            Options.Create(new CacheSettings()),
            Mock.Of<IAuditLogService>(),
            NullLogger<TenantExtensionRegistry>.Instance);
        return ctx;
    }

    private static void SeedEnabled(AppDbContext ctx, string featureName)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            Key = TenantExtensionSettings.EnabledKey,
            Value = $"[\"{featureName}\"]",
            UpdatedAtUtc = DateTime.UtcNow,
        });
        ctx.SaveChanges();
    }

    [TenantExtensionHook(TenantExtensionHooks.PaymentBeforeSign)]
    [TenantExtensionHook(TenantExtensionHooks.PaymentAfterPersist)]
    private sealed class PaymentProbeExtension : ITenantFeatureExtension
    {
        public List<string> Hooks { get; } = [];

        public bool Fail { get; init; }

        public string FeatureName => "hair_salon_campaign";

        public bool AppliesTo(Guid tenantId) => tenantId != Guid.Empty;

        public Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct)
        {
            Hooks.Add(context.HookName);
            return Task.FromResult(Fail
                ? new ExtensionResult(false, "campaign blocked")
                : new ExtensionResult(true));
        }
    }

    private sealed class PassThroughCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAllAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}

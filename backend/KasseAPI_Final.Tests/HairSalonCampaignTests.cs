using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using CacheSettings = KasseAPI_Final.Configuration.CacheSettings;

namespace KasseAPI_Final.Tests;

public sealed class HairSalonCampaignTests
{
    [Fact]
    public async Task Execute_DiscountsMatchingProduct_LeavesOtherProductUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var matched = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        AddCampaign(ctx, tenantId, 10m, [matched], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        AddCampaign(ctx, tenantId, 25m, [matched], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        await ctx.SaveChangesAsync();

        var result = await new HairSalonCampaignExtension(ctx, Mock.Of<IAuditLogService>(), NullLogger<HairSalonCampaignExtension>.Instance)
            .ExecuteAsync(
                new ExtensionContext(
                    tenantId,
                    TenantExtensionHooks.PaymentBeforeSign,
                    ExtensionCart.Write(
                        [
                            new ExtensionCartLine(matched, 1, 2, 10m),
                            new ExtensionCartLine(other, 1, 2, 10m),
                        ],
                        [])),
                CancellationToken.None);

        var lines = ExtensionCart.ReadLines(result.ModifiedPayload);
        Assert.Equal(7.50m, lines[0].GrossTotal);
        Assert.Equal(10m, lines[1].GrossTotal);
    }

    [Fact]
    public async Task Execute_ExpiredCampaign_DoesNotChangeLines()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        AddCampaign(ctx, tenantId, 10m, [], DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddMinutes(-1));
        await ctx.SaveChangesAsync();

        var result = await new HairSalonCampaignExtension(ctx, Mock.Of<IAuditLogService>(), NullLogger<HairSalonCampaignExtension>.Instance)
            .ExecuteAsync(
                new ExtensionContext(
                    tenantId,
                    TenantExtensionHooks.PaymentBeforeSign,
                    ExtensionCart.Write([new ExtensionCartLine(productId, 1, 2, 10m)], [])),
                CancellationToken.None);

        Assert.Null(result.ModifiedPayload);
    }

    [Fact]
    public async Task CreatePayment_WhenCampaignMatches_SignsDiscountedTotal()
    {
        await using var ctx = CreatePaymentContext(out var registry, out var audit);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        SeedEnabled(ctx);
        AddCampaign(ctx, SystemTenantIds.Platform, 10m, [productId], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        await ctx.SaveChangesAsync();
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { TseMock = tse, ExtensionRegistry = registry });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(9.00m, result.Payment!.TotalAmount);
        Assert.False(string.IsNullOrEmpty(result.Payment.TseSignature));
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                9.00m,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
        audit.Verify(
            x => x.LogSystemOperationAsync(
                "HAIR_SALON_CAMPAIGN_APPLIED",
                "HairSalonCampaign",
                "extension",
                "system",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.HairSalonCampaignApplied,
                It.IsAny<Guid?>(),
                SystemTenantIds.Platform,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_WhenCampaignExpired_SignsCatalogTotal()
    {
        await using var ctx = CreatePaymentContext(out var registry, out _);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        SeedEnabled(ctx);
        AddCampaign(ctx, SystemTenantIds.Platform, 10m, [productId], DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddMinutes(-1));
        await ctx.SaveChangesAsync();
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { TseMock = tse, ExtensionRegistry = registry });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(10.00m, result.Payment!.TotalAmount);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                10.00m,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_WhenExtensionNotEnabled_SignsCatalogTotal()
    {
        await using var ctx = CreatePaymentContext(out var registry, out _);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        AddCampaign(ctx, SystemTenantIds.Platform, 10m, [], DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        await ctx.SaveChangesAsync();
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { TseMock = tse, ExtensionRegistry = registry });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(10.00m, result.Payment!.TotalAmount);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                10.00m,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_CrossTenantCampaign_Returns404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = InMemoryOptions();
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantA));
        TenantTestDoubles.EnsureTenant(ctx, tenantA);
        TenantTestDoubles.EnsureTenant(ctx, tenantB);
        var campaignId = Guid.NewGuid();
        ctx.HairSalonCampaigns.Add(new HairSalonCampaign
        {
            Id = campaignId,
            TenantId = tenantB,
            Name = "Other salon",
            DiscountPercent = 10m,
            ValidFromUtc = DateTime.UtcNow.AddDays(-1),
            ValidUntilUtc = DateTime.UtcNow.AddDays(1),
            IsActive = true,
        });
        SeedEnabled(ctx, tenantA);
        await ctx.SaveChangesAsync();

        var controller = new AdminHairSalonCampaignsController(
            ctx,
            TenantTestDoubles.TenantAccessorReturning(tenantA),
            CreateRegistry(options, ctx));

        var result = await controller.Delete(campaignId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        var stored = await ctx.HairSalonCampaigns.IgnoreQueryFilters().SingleAsync(row => row.Id == campaignId);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task List_WhenExtensionNotEnabled_Returns403()
    {
        var tenantId = Guid.NewGuid();
        var options = InMemoryOptions();
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        var controller = new AdminHairSalonCampaignsController(
            ctx,
            TenantTestDoubles.TenantAccessorReturning(tenantId),
            CreateRegistry(options, ctx));

        var result = await controller.List(CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Contains(AdminHairSalonCampaignsController.NotEnabledCode, System.Text.Json.JsonSerializer.Serialize(forbidden.Value), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_SetsInactive()
    {
        var tenantId = Guid.NewGuid();
        var options = InMemoryOptions();
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        var campaignId = Guid.NewGuid();
        ctx.HairSalonCampaigns.Add(new HairSalonCampaign
        {
            Id = campaignId,
            TenantId = tenantId,
            Name = "Weekday",
            DiscountPercent = 5m,
            ValidFromUtc = DateTime.UtcNow.AddDays(-1),
            ValidUntilUtc = DateTime.UtcNow.AddDays(1),
            IsActive = true,
        });
        SeedEnabled(ctx, tenantId);
        await ctx.SaveChangesAsync();

        var controller = new AdminHairSalonCampaignsController(
            ctx,
            TenantTestDoubles.TenantAccessorReturning(tenantId),
            CreateRegistry(options, ctx));

        var result = await controller.Delete(campaignId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var stored = await ctx.HairSalonCampaigns.SingleAsync(row => row.Id == campaignId);
        Assert.False(stored.IsActive);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var ctx = new AppDbContext(InMemoryOptions(), TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        return ctx;
    }

    private static AppDbContext CreatePaymentContext(out ITenantExtensionRegistry registry, out Mock<IAuditLogService> audit)
    {
        var options = InMemoryOptions();
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
        audit = new Mock<IAuditLogService>();
        registry = CreateRegistry(options, ctx, audit.Object);
        return ctx;
    }

    private static ITenantExtensionRegistry CreateRegistry(
        DbContextOptions<AppDbContext> options,
        AppDbContext extensionContext,
        IAuditLogService? audit = null)
    {
        var extension = new HairSalonCampaignExtension(
            extensionContext,
            audit ?? Mock.Of<IAuditLogService>(),
            NullLogger<HairSalonCampaignExtension>.Instance);
        return new TenantExtensionRegistry(
            [extension],
            TenantTestDoubles.DbContextFactoryForTests(options, TenantTestDoubles.TenantAccessorReturning(null)),
            new MemoryCache(new MemoryCacheOptions()),
            new EmptyCache(),
            Options.Create(new CacheSettings()),
            Mock.Of<IAuditLogService>(),
            NullLogger<TenantExtensionRegistry>.Instance);
    }

    private static void SeedEnabled(AppDbContext ctx, Guid? tenantId = null)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? SystemTenantIds.Platform,
            Key = TenantExtensionSettings.EnabledKey,
            Value = "[\"hair_salon_campaign\"]",
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    private static void AddCampaign(
        AppDbContext ctx,
        Guid tenantId,
        decimal percent,
        List<Guid> productIds,
        DateTime validFromUtc,
        DateTime validUntilUtc)
    {
        ctx.HairSalonCampaigns.Add(new HairSalonCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Salon",
            DiscountPercent = percent,
            AppliesToProductIds = productIds,
            ValidFromUtc = validFromUtc,
            ValidUntilUtc = validUntilUtc,
            IsActive = true,
        });
    }

    private static DbContextOptions<AppDbContext> InMemoryOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"HairSalon_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private sealed class EmptyCache : ICacheService
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

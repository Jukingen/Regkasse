using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Coverage for <c>EnsureLicenseAllowsPaymentAsync</c>.
/// Enforcement runs only in Production + Device TSE with <see cref="ILicenseService"/> present.
/// GraceWrite allows payments (no PaymentResult warning). GET / admin reads are not gated here.
/// </summary>
public sealed class PaymentServiceLicenseGateTests
{
    [Fact]
    public async Task CreatePayment_WhenTenantLicenseActive_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(30));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object));

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.NotNull(result.Payment);
    }

    [Fact]
    public async Task CreatePayment_WhenTenantLicenseInGraceWrite_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-3));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object));

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task CreatePayment_WhenTenantLicenseLockdown_AsCashier_ThrowsTenantScope()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-10));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object,
                role: "Cashier"));

        var ex = await Assert.ThrowsAsync<LicenseExpiredException>(() =>
            sut.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
                PaymentServiceCoverageHarness.CashierId));

        Assert.Equal("tenant", ex.Scope);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenTenantLicenseLockdown_AsManager_ThrowsTenantScope()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-10));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object,
                role: "Manager"));

        var ex = await Assert.ThrowsAsync<LicenseExpiredException>(() =>
            sut.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
                PaymentServiceCoverageHarness.CashierId));

        Assert.Equal("tenant", ex.Scope);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenTenantLicenseArchived_ThrowsTenantScope()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-40));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object));

        var ex = await Assert.ThrowsAsync<LicenseExpiredException>(() =>
            sut.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
                PaymentServiceCoverageHarness.CashierId));

        Assert.Equal("tenant", ex.Scope);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task GetPayment_WhenTenantLicenseLockdown_StillReturnsExistingPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(30));
        var license = PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment());
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(license.Object, role: "Manager"));

        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-10));

        var loaded = await sut.GetPaymentAsync(sale.Payment!.Id);
        Assert.NotNull(loaded);
        Assert.Equal(sale.Payment.Id, loaded!.Id);
    }

    [Fact]
    public async Task CreatePayment_WhenNoTenantLicenseAndDeploymentExpiredBeyondGrace_ThrowsDeploymentScope()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, null);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(
                    PaymentServiceCoverageHarness.ExpiredDeployment(20)).Object));

        var ex = await Assert.ThrowsAsync<LicenseExpiredException>(() =>
            sut.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
                PaymentServiceCoverageHarness.CashierId));

        Assert.Equal("deployment", ex.Scope);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenNoTenantLicenseAndDeploymentValid_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, null);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object));

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task CreatePayment_WhenDeploymentExpiredWithin15Days_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(30));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(
                    PaymentServiceCoverageHarness.ExpiredDeployment(10)).Object));

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task CreatePayment_WhenExpiredTrialDeployment_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(
                    PaymentServiceCoverageHarness.ExpiredDeployment(40, isTrial: true)).Object));

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task CreatePayment_WhenDevelopmentHostAndLockdown_SkipsEnforcement()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-10));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                License = PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object,
                LicenseOptions = new LicenseOptions { Enabled = true },
                Tse = new TseOptions { TseMode = "Device" },
                Host = TenantTestDoubles.HostEnvironmentReturning(Environments.Development)
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task CreatePayment_WhenDemoTseAndLockdown_SkipsEnforcement()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-10));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                License = PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object,
                LicenseOptions = new LicenseOptions { Enabled = true },
                Tse = new TseOptions { TseMode = "Demo" },
                Host = TenantTestDoubles.ProductionHostEnvironment
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
    }
}

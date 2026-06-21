using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantHardDeletePolicyTests
{
    private static TenantDeleteDependencyCountsDto EmptyCounts() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static ITenantHardDeletePolicy CreatePolicy(
        string environmentName,
        bool allowPermanentDeleteInProduction = false) =>
        new TenantHardDeletePolicy(
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environmentName),
            Options.Create(new TenantDeletionOptions
            {
                AllowPermanentDeleteInProduction = allowPermanentDeleteInProduction,
            }));

    [Fact]
    public void IsProduction_ReturnsFalse_InDevelopment()
    {
        var policy = CreatePolicy(Environments.Development);
        Assert.False(policy.IsProduction());
    }

    [Fact]
    public void IsProduction_ReturnsTrue_InProductionByDefault()
    {
        var policy = CreatePolicy(Environments.Production);
        Assert.True(policy.IsProduction());
    }

    [Fact]
    public void IsProduction_ReturnsFalse_InProductionWhenOptionEnabled()
    {
        var policy = CreatePolicy(Environments.Production, allowPermanentDeleteInProduction: true);
        Assert.False(policy.IsProduction());
    }

    [Fact]
    public void HasFiscalFootprint_TrueWhenPaymentsOrDailyClosingsPresent()
    {
        var policy = CreatePolicy(Environments.Development);

        Assert.False(policy.HasFiscalFootprint(EmptyCounts()));
        Assert.True(policy.HasFiscalFootprint(EmptyCounts() with { Payments = 1 }));
        Assert.True(policy.HasFiscalFootprint(EmptyCounts() with { DailyClosings = 1 }));
        Assert.False(policy.HasFiscalFootprint(EmptyCounts() with { Receipts = 3, VoucherLedgerEntries = 2 }));
    }

    [Fact]
    public void Validate_BlocksProductionWithDependencies()
    {
        var policy = CreatePolicy(Environments.Production);
        var counts = EmptyCounts() with { Products = 2 };

        var result = policy.Validate(counts, isProduction: true, forceDelete: false);

        Assert.False(result.CanDelete);
        Assert.Equal(TenantPermanentDeleteFailureCodes.ProductionPolicy, result.FailureCode);
    }

    [Fact]
    public void Validate_BlocksProductionEvenWithoutDependencies()
    {
        var policy = CreatePolicy(Environments.Production);

        var result = policy.Validate(EmptyCounts(), isProduction: true, forceDelete: false);

        Assert.False(result.CanDelete);
        Assert.Equal(TenantPermanentDeleteFailureCodes.ProductionPolicy, result.FailureCode);
    }

    [Fact]
    public void Validate_BlocksFiscalFootprint_InDevelopment()
    {
        var policy = CreatePolicy(Environments.Development);
        var counts = EmptyCounts() with { Payments = 1 };

        var result = policy.Validate(counts, isProduction: false, forceDelete: false);

        Assert.False(result.CanDelete);
        Assert.Equal(TenantPermanentDeleteFailureCodes.FiscalFootprintPresent, result.FailureCode);
    }

    [Fact]
    public void Validate_BlocksCashRegisters_InDevelopment()
    {
        var policy = CreatePolicy(Environments.Development);
        var counts = EmptyCounts() with { CashRegisters = 1 };

        var result = policy.Validate(counts, isProduction: false, forceDelete: false);

        Assert.False(result.CanDelete);
        Assert.Equal(TenantPermanentDeleteFailureCodes.CashRegistersPresent, result.FailureCode);
    }

    [Fact]
    public void Validate_AllowsEmptyTenant_InDevelopment()
    {
        var policy = CreatePolicy(Environments.Development);

        var result = policy.Validate(EmptyCounts(), isProduction: false, forceDelete: false);

        Assert.True(result.CanDelete);
        Assert.Null(result.FailureCode);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public void Validate_BlocksForceDelete_InProduction()
    {
        var policy = CreatePolicy(Environments.Production);

        var result = policy.Validate(EmptyCounts(), isProduction: true, forceDelete: true);

        Assert.False(result.CanDelete);
        Assert.Equal(TenantPermanentDeleteFailureCodes.ForceDeleteDevelopmentOnly, result.FailureCode);
    }

    [Fact]
    public void GetBlockers_IncludesProductionPolicyWhenProductionAndDependenciesExist()
    {
        var policy = CreatePolicy(Environments.Production);
        var counts = EmptyCounts() with { CashRegisters = 1, Products = 4 };

        var blockers = policy.GetBlockers(counts, isProduction: true);

        Assert.Contains(
            blockers,
            b => b.Code == TenantPermanentDeleteFailureCodes.ProductionPolicy);
        Assert.Contains(
            blockers,
            b => b.Code == TenantPermanentDeleteFailureCodes.CashRegistersPresent);
    }
}

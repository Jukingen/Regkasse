using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevLimitScenarioPlannerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static TenantLimitUsageDto Usage(int products = 8, int users = 4, decimal revenue = 80m) =>
        new()
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(TenantLimits.CreateDefault(TenantId)),
            CurrentProducts = products,
            CurrentUsers = users,
            CurrentDailyTransactions = 2,
            CurrentDailyRevenue = revenue,
            CurrentBackups = 1,
            CurrentBackupSizeMb = 12.4m,
            CurrentOfflineTransactions = 3,
            CurrentMaxAssignedRegistersPerUser = 1,
        };

    [Fact]
    public void Near_SetsProductsToEightyPercentCap()
    {
        var patch = DevLimitScenarioPlanner.Build(Usage(), DevLimitScenarioNames.Near, TenantLimitKeys.MaxProductsPerTenant);
        Assert.NotNull(patch);
        Assert.Equal(10, patch.MaxProductsPerTenant);
        Assert.Null(patch.MaxUsersPerTenant);
    }

    [Fact]
    public void At_SetsCapToCurrentSoNextCreateFails()
    {
        var patch = DevLimitScenarioPlanner.Build(Usage(products: 8), DevLimitScenarioNames.At, TenantLimitKeys.MaxProductsPerTenant);
        Assert.NotNull(patch);
        Assert.Equal(8, patch.MaxProductsPerTenant);
    }

    [Fact]
    public void Tiny_SetsAllIntegerCapsToOne()
    {
        var patch = DevLimitScenarioPlanner.Build(Usage(), DevLimitScenarioNames.Tiny, limitKey: null);
        Assert.NotNull(patch);
        Assert.Equal(1, patch.MaxProductsPerTenant);
        Assert.Equal(1, patch.MaxUsersPerTenant);
        Assert.Equal(1, patch.DailyMaxTransactions);
        Assert.Equal(1m, patch.MaxTransactionAmount);
        Assert.Equal(1m, patch.DailyMaxRevenue);
        Assert.Equal(1, patch.MaxOfflineTransactions);
    }

    [Fact]
    public void ResetWithoutKey_ReturnsNullForFullReset()
    {
        Assert.Null(DevLimitScenarioPlanner.Build(Usage(), DevLimitScenarioNames.Reset, limitKey: null));
    }

    [Fact]
    public void ResetWithKey_RestoresThatDefaultOnly()
    {
        var patch = DevLimitScenarioPlanner.Build(
            Usage(),
            DevLimitScenarioNames.Reset,
            TenantLimitKeys.MaxOfflineTransactions);
        Assert.NotNull(patch);
        Assert.Equal(TenantLimits.DefaultMaxOfflineTransactions, patch.MaxOfflineTransactions);
        Assert.Null(patch.MaxProductsPerTenant);
    }

    [Fact]
    public void UnknownScenario_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DevLimitScenarioPlanner.Build(Usage(), "explode", null));
    }
}

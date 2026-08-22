using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LimitDashboardMapperTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Theory]
    [InlineData(100, 79, LimitUsageStatuses.Healthy)]
    [InlineData(100, 80, LimitUsageStatuses.Warning)]
    [InlineData(100, 99, LimitUsageStatuses.Warning)]
    [InlineData(100, 100, LimitUsageStatuses.Critical)]
    [InlineData(100, 150, LimitUsageStatuses.Critical)]
    [InlineData(0, 1, LimitUsageStatuses.Healthy)]
    public void ClassifyHealth_UsesEightyPercentThreshold(int limit, int current, string expected)
    {
        Assert.Equal(expected, LimitDashboardMapper.ClassifyHealth(limit, current));
    }

    [Theory]
    [InlineData(5, 4, LimitUsageStatuses.Approaching)]
    [InlineData(5, 5, LimitUsageStatuses.Full)]
    [InlineData(5, 6, LimitUsageStatuses.Exceeded)]
    [InlineData(5, 3, null)]
    public void ClassifyUser_SeparatesFullFromExceeded(int limit, int current, string? expected)
    {
        Assert.Equal(expected, LimitDashboardMapper.ClassifyUser(limit, current));
    }

    [Theory]
    [InlineData(2, LimitUsageStatuses.Increasing)]
    [InlineData(0, LimitUsageStatuses.Stable)]
    [InlineData(-3, LimitUsageStatuses.Decreasing)]
    public void ClassifyTrend_UsesSignOfChangeCount(int change, string expected)
    {
        Assert.Equal(expected, LimitDashboardMapper.ClassifyTrend(change));
    }

    [Fact]
    public void FromUsage_MarksWarningProductsWithCatalogAndTrend()
    {
        var usage = new TenantLimitUsageDto
        {
            TenantId = TenantId,
            Limits = TenantLimitsDto.FromEntity(TenantLimits.CreateDefault(TenantId)),
            CurrentProducts = 8000,
            CurrentUsers = 1,
            CurrentDailyTransactions = 0,
            CurrentDailyRevenue = 0,
            CurrentBackups = 0,
            CurrentBackupSizeMb = 0,
            CurrentOfflineTransactions = 0,
            CurrentMaxAssignedRegistersPerUser = 0,
        };

        var rows = LimitDashboardMapper.FromUsage(
            usage,
            "Cafe",
            changeCounts: new Dictionary<string, int> { [TenantLimitKeys.MaxProductsPerTenant] = 12 },
            tenantSlug: "cafe");
        var products = Assert.Single(rows, r => r.Key == TenantLimitKeys.MaxProductsPerTenant);
        Assert.Equal(LimitUsageStatuses.Warning, products.Status);
        Assert.Equal(80d, products.Percentage);
        Assert.Equal("Cafe", products.TenantName);
        Assert.Equal("cafe", products.TenantSlug);
        Assert.Equal("cafe", products.TenantSlug);
        Assert.Equal("Max. products per tenant", products.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(products.Description));
        Assert.Equal(12, products.ChangeCount);
        Assert.Equal(LimitUsageStatuses.Increasing, products.Trend);
        Assert.Equal("products", products.ChangeUnit);
    }

    [Fact]
    public void RecommendedAction_ForFullAssignment()
    {
        var text = LimitDashboardMapper.RecommendedAction(
            TenantLimitKeys.MaxActiveRegistersPerUser,
            LimitUsageStatuses.Full);
        Assert.Contains("assignment cap", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToPublishRequest_UsesExceededTitleAndDedupKey()
    {
        var request = LimitDashboardMapper.ToPublishRequest(
            TenantId,
            ActivityEventType.LimitExceeded,
            TenantLimitKeys.MaxUsersPerTenant,
            50,
            50);

        Assert.Equal("Limit exceeded", request.Title);
        Assert.Equal("tenant_limit", request.EntityType);
        Assert.Equal(TenantLimitKeys.MaxUsersPerTenant, request.EntityId);
        Assert.Equal($"limit_exceeded_{TenantLimitKeys.MaxUsersPerTenant}", request.DedupKey);
    }
}

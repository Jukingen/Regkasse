using KasseAPI_Final.Services.DataDeletion;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataDeletionServiceLogicTests
{
    [Fact]
    public void ConfirmationWaitDays_IsSeven()
    {
        Assert.Equal(7, DataDeletionService.ConfirmationWaitDays);
        Assert.Equal(7, DataDeletionService.RksvRetentionYears);
    }

    [Fact]
    public void DeletionResult_Fail_SetsErrorCode()
    {
        var result = DeletionResult.Fail("Grace period not yet completed", DataDeletionErrorCodes.GracePeriodActive);
        Assert.False(result.Succeeded);
        Assert.Equal(DataDeletionErrorCodes.GracePeriodActive, result.ErrorCode);
        Assert.Contains("Grace", result.Error);
    }

    [Fact]
    public void DeletionResult_Success_SetsIds()
    {
        var requestId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var counts = new Dictionary<string, int> { ["products"] = 3 };
        var result = DeletionResult.Success(requestId, tenantId, counts);
        Assert.True(result.Succeeded);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(3, result.DeletedCounts!["products"]);
    }
}

using KasseAPI_Final.Services.Maintenance;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MaintenanceOperationFilterTests
{
    private readonly MaintenanceOperationFilter _filter = new();

    [Theory]
    [InlineData("GET", "/api/pos/products")]
    [InlineData("GET", "/api/pos/categories")]
    [InlineData("GET", "/api/admin/tenants")]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/admin/reports/summary")]
    [InlineData("GET", "/api/Receipts/list")]
    [InlineData("GET", "/api/admin/backup/runs")]
    [InlineData("HEAD", "/api/admin/tenants")]
    [InlineData("OPTIONS", "/api/pos/products")]
    public void IsOperationAllowed_AllowsReads(string method, string path)
    {
        Assert.True(_filter.IsOperationAllowed(method, path));
    }

    [Theory]
    [InlineData("POST", "/api/Auth/login")]
    [InlineData("POST", "/api/Auth/refresh")]
    [InlineData("GET", "/api/maintenance/status")]
    [InlineData("GET", "/api/maintenance/notification")]
    [InlineData("POST", "/api/pos/maintenance-notifications/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/acknowledge")]
    public void IsOperationAllowed_AllowsCriticalWritesAndStatus(string method, string path)
    {
        Assert.True(_filter.IsOperationAllowed(method, path));
    }

    [Theory]
    [InlineData("POST", "/api/pos/payment")]
    [InlineData("POST", "/api/pos/offline-orders")]
    [InlineData("POST", "/api/rksv/special-receipts")]
    [InlineData("POST", "/api/Tagesabschluss")]
    [InlineData("POST", "/api/admin/backup/trigger")]
    [InlineData("POST", "/api/admin/tenants")]
    [InlineData("PUT", "/api/admin/users/1")]
    [InlineData("DELETE", "/api/admin/tenants/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    public void IsOperationAllowed_BlocksWrites(string method, string path)
    {
        Assert.False(_filter.IsOperationAllowed(method, path));
    }

    [Theory]
    [InlineData("POST", "/api/pos/payment")]
    [InlineData("POST", "/api/admin/backup/trigger")]
    public void IsHighRiskBlockedWrite_DetectsFiscalAndBackup(string method, string path)
    {
        Assert.True(_filter.IsHighRiskBlockedWrite(method, path));
    }
}

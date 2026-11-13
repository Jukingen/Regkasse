using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Offline;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OfflineAlertServiceTests
{
    [Fact]
    public void MapAnomaly_MapsOrderBacklogToOfflineOrdersBacklogGrowing()
    {
        var anomaly = new OfflineAnomaly
        {
            Code = "too_many_pending",
            Severity = "critical",
            Message = "55 pending offline orders exceed tenant limit (50).",
            DetectedAt = DateTime.UtcNow,
        };

        var (eventType, dedupKey) = OfflineAlertService.MapAnomaly(anomaly);

        Assert.Equal(ActivityEventType.OfflineOrdersBacklogGrowing, eventType);
        Assert.Equal("offline_orders_backlog_tenant", dedupKey);
    }

    [Fact]
    public void MapAnomaly_MapsSyncFailureToOfflineSyncStalled()
    {
        var anomaly = new OfflineAnomaly
        {
            Code = "sync_failure",
            Severity = "critical",
            Message = "Offline sync success rate 50% is below minimum 80%.",
            DetectedAt = DateTime.UtcNow,
        };

        var (eventType, _) = OfflineAlertService.MapAnomaly(anomaly);

        Assert.Equal(ActivityEventType.OfflineSyncStalled, eventType);
    }
}

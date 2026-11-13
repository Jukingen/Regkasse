namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Business-level Prometheus gauges/counters (tenants, revenue, orders, users).
/// </summary>
public interface IBusinessMetricsService
{
    void UpdateTenantCount(int count);
    void UpdateRevenue(decimal amount);
    void UpdateActiveOrders(int count);
    void RecordOrderCreated();
    void UpdateRegisteredUsers(int count);

    int GetActiveTenants();
    int GetActiveOrders();
    int GetRegisteredUsers();
    decimal GetRevenueEur();
}

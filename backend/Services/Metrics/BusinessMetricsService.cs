using Prometheus;

namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Prometheus-backed business metrics for tenants, revenue, orders, and users.
/// </summary>
public class BusinessMetricsService : IBusinessMetricsService
{
    // Qualify Prometheus.Metrics — this type lives in namespace Services.Metrics.
    private static readonly Gauge ActiveTenants = global::Prometheus.Metrics
        .CreateGauge("tenants_active_total", "Active tenants");

    private static readonly Gauge TotalRevenue = global::Prometheus.Metrics
        .CreateGauge("revenue_total_eur", "Total revenue in EUR");

    private static readonly Gauge ActiveOrders = global::Prometheus.Metrics
        .CreateGauge("orders_active_total", "Active orders");

    private static readonly Counter OrdersCreated = global::Prometheus.Metrics
        .CreateCounter("orders_created_total", "Total orders created");

    private static readonly Gauge RegisteredUsers = global::Prometheus.Metrics
        .CreateGauge("users_registered_total", "Total registered users");

    private int _activeTenants;
    private int _activeOrders;
    private int _registeredUsers;
    private decimal _revenueEur;

    public void UpdateTenantCount(int count)
    {
        _activeTenants = Math.Max(0, count);
        ActiveTenants.Set(_activeTenants);
    }

    public void UpdateRevenue(decimal amount)
    {
        _revenueEur = Math.Max(0m, amount);
        TotalRevenue.Set((double)_revenueEur);
    }

    public void UpdateActiveOrders(int count)
    {
        _activeOrders = Math.Max(0, count);
        ActiveOrders.Set(_activeOrders);
    }

    public void RecordOrderCreated()
    {
        OrdersCreated.Inc();
    }

    public void UpdateRegisteredUsers(int count)
    {
        _registeredUsers = Math.Max(0, count);
        RegisteredUsers.Set(_registeredUsers);
    }

    public int GetActiveTenants() => _activeTenants;

    public int GetActiveOrders() => _activeOrders;

    public int GetRegisteredUsers() => _registeredUsers;

    public decimal GetRevenueEur() => _revenueEur;
}

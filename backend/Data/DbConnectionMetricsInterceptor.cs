using System.Data.Common;
using KasseAPI_Final.Services.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KasseAPI_Final.Data;

/// <summary>
/// Tracks <c>db_connections_active</c> via <see cref="IDbMetricsService.TrackConnection"/>.
/// </summary>
public sealed class DbConnectionMetricsInterceptor : DbConnectionInterceptor
{
    private readonly IDbMetricsService _metrics;

    public DbConnectionMetricsInterceptor(IDbMetricsService metrics)
    {
        _metrics = metrics;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        _metrics.TrackConnection(isOpen: true);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _metrics.TrackConnection(isOpen: true);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        _metrics.TrackConnection(isOpen: false);
        base.ConnectionClosed(connection, eventData);
    }

    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
    {
        _metrics.TrackConnection(isOpen: false);
        return base.ConnectionClosedAsync(connection, eventData);
    }
}

using System.Net;
using System.Net.Sockets;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// IP allowlist for Prometheus <c>/metrics</c>. Loopback is always allowed.
/// Empty <see cref="PrometheusMonitoringOptions.AllowedCidrs"/> allows RFC1918 + unique-local IPv6
/// (typical Docker scrape). An explicit list replaces that default (loopback still allowed).
/// </summary>
public static class PrometheusMetricsAccess
{
    private static readonly string[] DefaultInternalCidrs =
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "fc00::/7",
    ];

    public static bool IsAllowed(IPAddress? remoteAddress, PrometheusMonitoringOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (remoteAddress is null)
            return false;

        var ip = Normalize(remoteAddress);
        if (IPAddress.IsLoopback(ip))
            return true;

        foreach (var network in ResolveNetworks(options.AllowedCidrs))
        {
            if (network.Contains(ip))
                return true;
        }

        return false;
    }

    public static IReadOnlyList<IPNetwork> ResolveNetworks(IReadOnlyList<string>? configuredCidrs)
    {
        var raw = configuredCidrs is { Count: > 0 }
            ? configuredCidrs
            : DefaultInternalCidrs;

        var networks = new List<IPNetwork>(raw.Count);
        foreach (var cidr in raw)
        {
            if (string.IsNullOrWhiteSpace(cidr))
                continue;
            if (IPNetwork.TryParse(cidr.Trim(), out var network))
                networks.Add(network);
        }

        return networks;
    }

    private static IPAddress Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetworkV6
            && IPAddress.IsLoopback(address))
        {
            return IPAddress.Loopback;
        }

        return address;
    }
}

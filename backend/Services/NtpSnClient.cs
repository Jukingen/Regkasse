using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace KasseAPI_Final.Services;

/// <summary>
/// Minimal SNTP client (RFC 4330) for UDP port 123 — no external package.
/// </summary>
internal static class NtpSnClient
{
    private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Returns estimated offset (NTP − local clock) in seconds using transmit timestamp and round-trip midpoint.
    /// </summary>
    internal static async Task<(bool Ok, double OffsetSeconds, DateTime NtpUtc)> TryQueryOffsetAsync(
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        try
        {
            var request = new byte[48];
            request[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)

            var t1 = DateTime.UtcNow;
            await udp.SendAsync(request, request.Length, host, 123).ConfigureAwait(false);

            var receiveTask = udp.ReceiveAsync();
            var delayTask = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(receiveTask, delayTask).ConfigureAwait(false);
            if (completed != receiveTask)
                return (false, 0, default);

            var result = await receiveTask.ConfigureAwait(false);
            var t2 = DateTime.UtcNow;
            var buffer = result.Buffer;
            if (buffer.Length < 48)
                return (false, 0, default);

            var seconds = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(40, 4));
            var fraction = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(44, 4));
            var ntpUtc = NtpEpoch.AddSeconds(seconds + fraction / 4294967296.0);

            var midLocal = t1 + (t2 - t1) / 2;
            var offsetSeconds = (ntpUtc - midLocal).TotalSeconds;
            return (true, offsetSeconds, ntpUtc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return (false, 0, default);
        }
    }
}

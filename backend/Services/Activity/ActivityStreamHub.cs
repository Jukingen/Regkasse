using System.Runtime.CompilerServices;
using System.Threading.Channels;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Activity;

/// <summary>Tenant-scoped in-memory fan-out for SSE activity streams.</summary>
public sealed class ActivityStreamHub : IActivityStreamHub
{
    private readonly ActivityNotificationOptions _options;
    private readonly ILogger<ActivityStreamHub> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, TenantSubscriptionList> _tenants = new();

    public ActivityStreamHub(
        IOptions<ActivityNotificationOptions> options,
        ILogger<ActivityStreamHub> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void Publish(Guid tenantId, object activityPayload)
    {
        List<ChannelWriter<ActivityStreamMessage>> writers;
        lock (_gate)
        {
            if (!_tenants.TryGetValue(tenantId, out var list) || list.Writers.Count == 0)
                return;

            writers = list.Writers.ToList();
        }

        var message = new ActivityStreamMessage("activity", activityPayload);
        foreach (var writer in writers)
        {
            if (!writer.TryWrite(message))
            {
                _logger.LogDebug(
                    "Activity SSE subscriber channel full for tenant {TenantId}; dropping event",
                    tenantId);
            }
        }
    }

    public async IAsyncEnumerable<ActivityStreamMessage> SubscribeAsync(
        Guid tenantId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<ActivityStreamMessage>(
            new BoundedChannelOptions(64)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        Register(tenantId, channel.Writer);

        // PeriodicTimer allows only one in-flight WaitForNextTickAsync at a time.
        // Racing it with WaitToReadAsync via Task.WhenAny left the previous wait active
        // when activity arrived first, causing InvalidOperationException on the next loop.
        // Own the timer exclusively in a ping loop that writes into the same channel.
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pingSeconds = Math.Clamp(_options.SsePingIntervalSeconds, 5, 120);
        var pingTask = RunSsePingLoopAsync(channel.Writer, pingSeconds, pingCts.Token);

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var message))
                    yield return message;
            }
        }
        finally
        {
            pingCts.Cancel();
            try
            {
                await pingTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Expected when the SSE client disconnects.
            }

            Unregister(tenantId, channel.Writer);
            channel.Writer.TryComplete();
        }
    }

    private async Task RunSsePingLoopAsync(
        ChannelWriter<ActivityStreamMessage> writer,
        int pingSeconds,
        CancellationToken cancellationToken)
    {
        using var pingTimer = new PeriodicTimer(TimeSpan.FromSeconds(pingSeconds));
        try
        {
            while (await pingTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!writer.TryWrite(new ActivityStreamMessage("ping", new { })))
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            // Subscriber disconnected or subscription finally cancelled the ping loop.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Activity SSE ping loop ended unexpectedly");
        }
    }

    private void Register(Guid tenantId, ChannelWriter<ActivityStreamMessage> writer)
    {
        lock (_gate)
        {
            if (!_tenants.TryGetValue(tenantId, out var list))
            {
                list = new TenantSubscriptionList();
                _tenants[tenantId] = list;
            }

            list.Writers.Add(writer);
        }
    }

    private void Unregister(Guid tenantId, ChannelWriter<ActivityStreamMessage> writer)
    {
        lock (_gate)
        {
            if (!_tenants.TryGetValue(tenantId, out var list))
                return;

            list.Writers.Remove(writer);
            if (list.Writers.Count == 0)
                _tenants.Remove(tenantId);
        }
    }

    private sealed class TenantSubscriptionList
    {
        public List<ChannelWriter<ActivityStreamMessage>> Writers { get; } = new();
    }
}

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Logging;
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
                    "Activity SSE subscriber channel full for tenant: {TenantId}; dropping event",
                    LogIdFormatting.ShortGuid(tenantId));
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

        var pingSeconds = Math.Clamp(_options.SsePingIntervalSeconds, 5, 120);
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pingTask = WriteKeepAlivePingsAsync(channel.Writer, pingSeconds, pingCts.Token);

        try
        {
            await foreach (var message in ReadAllUntilCanceledAsync(channel.Reader, cancellationToken))
            {
                yield return message;
            }
        }
        finally
        {
            pingCts.Cancel();
            await AwaitPingShutdownAsync(pingTask).ConfigureAwait(false);

            Unregister(tenantId, channel.Writer);
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Sequential PeriodicTimer loop (one WaitForNextTickAsync at a time).
    /// Keeps SSE alive without racing the channel reader via Task.WhenAny.
    /// </summary>
    private static async Task WriteKeepAlivePingsAsync(
        ChannelWriter<ActivityStreamMessage> writer,
        int pingSeconds,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pingSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!writer.TryWrite(new ActivityStreamMessage("ping", new { })))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when client disconnects
        }
    }

    /// <summary>
    /// Channel read loop that swallows disconnect cancellation.
    /// MoveNext is outside try/catch-around-yield (CS1626 / CS1631).
    /// </summary>
    private static async IAsyncEnumerable<ActivityStreamMessage> ReadAllUntilCanceledAsync(
        ChannelReader<ActivityStreamMessage> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (await MoveNextIgnoreCancelAsync(enumerator).ConfigureAwait(false))
                yield return enumerator.Current;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<bool> MoveNextIgnoreCancelAsync(
        IAsyncEnumerator<ActivityStreamMessage> enumerator)
    {
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when client disconnects
            return false;
        }
    }

    private static async Task AwaitPingShutdownAsync(Task pingTask)
    {
        try
        {
            await pingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect / unsubscribe
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

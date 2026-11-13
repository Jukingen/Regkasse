namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Wakes the outbox poll loop. Does not submit, classify, or change backoff.
/// </summary>
public interface IFinanzOnlineOutboxWorkerSignal
{
    void Signal(Guid? outboxId = null);

    Task DelayAsync(TimeSpan delay, CancellationToken stoppingToken);
}

public sealed class FinanzOnlineOutboxWorkerSignal : IFinanzOnlineOutboxWorkerSignal
{
    private readonly object _gate = new();
    private CancellationTokenSource _wake = new();
    private Guid? _lastOutboxId;
    private int _signalCount;

    public Guid? LastOutboxId
    {
        get
        {
            lock (_gate)
                return _lastOutboxId;
        }
    }

    public int SignalCount
    {
        get
        {
            lock (_gate)
                return _signalCount;
        }
    }

    public void Signal(Guid? outboxId = null)
    {
        CancellationTokenSource previous;
        lock (_gate)
        {
            _signalCount++;
            _lastOutboxId = outboxId;
            previous = _wake;
            _wake = new CancellationTokenSource();
        }

        previous.Cancel();
    }

    public async Task DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        CancellationToken wake;
        lock (_gate)
            wake = _wake.Token;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake);
        try
        {
            await Task.Delay(delay, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
        }
    }
}

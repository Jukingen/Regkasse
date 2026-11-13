using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Activity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ActivityStreamHubTests
{
    private static ActivityStreamHub CreateHub(int pingSeconds = 5) =>
        new(
            Options.Create(new ActivityNotificationOptions { SsePingIntervalSeconds = pingSeconds }),
            Mock.Of<ILogger<ActivityStreamHub>>());

    /// <summary>
    /// Regression: racing PeriodicTimer.WaitForNextTickAsync with channel WaitToReadAsync via
    /// Task.WhenAny caused InvalidOperationException when activity arrived before the ping tick
    /// (overlapping WaitForNextTickAsync calls are not allowed).
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_activity_bursts_do_not_throw_InvalidOperationException()
    {
        var hub = CreateHub(pingSeconds: 5);
        var tenantId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));

        var received = 0;
        Exception? fault = null;

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var message in hub.SubscribeAsync(tenantId, cts.Token))
                {
                    if (message.EventName == "activity")
                        Interlocked.Increment(ref received);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the test CTS expires.
            }
            catch (Exception ex)
            {
                fault = ex;
            }
        });

        // Give the subscriber time to register before publishing.
        await Task.Delay(50);

        for (var i = 0; i < 40; i++)
        {
            hub.Publish(tenantId, new { i });
            await Task.Delay(5);
        }

        await consumeTask;

        Assert.Null(fault);
        Assert.True(received > 0, "Expected at least one activity message to be delivered.");
    }

    [Fact]
    public async Task SubscribeAsync_delivers_published_activity_to_subscriber()
    {
        var hub = CreateHub();
        var tenantId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var payload = new { title = "hello" };
        ActivityStreamMessage? got = null;

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var message in hub.SubscribeAsync(tenantId, cts.Token))
            {
                if (message.EventName == "activity")
                {
                    got = message;
                    cts.Cancel();
                    break;
                }
            }
        });

        await Task.Delay(50);
        hub.Publish(tenantId, payload);

        try
        {
            await consumeTask;
        }
        catch (OperationCanceledException)
        {
            // Consumer cancelled after first activity.
        }

        Assert.NotNull(got);
        Assert.Equal("activity", got!.EventName);
        Assert.Same(payload, got.Data);
    }

    [Fact]
    public async Task Publish_without_subscribers_does_not_throw()
    {
        var hub = CreateHub();
        var ex = Record.Exception(() => hub.Publish(Guid.NewGuid(), new { }));
        Assert.Null(ex);
        await Task.CompletedTask;
    }
}

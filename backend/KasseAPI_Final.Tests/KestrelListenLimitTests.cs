using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class KestrelListenLimitTests
{
    [Fact]
    public void ConfigureKestrelLimits_SetsConnectionCeilingToOneThousand()
    {
        var options = new KestrelServerOptions();

        ApplicationHost.ConfigureKestrelLimits(options);

        Assert.Equal(1000, options.Limits.MaxConcurrentConnections);
        Assert.Equal(1000, options.Limits.MaxConcurrentUpgradedConnections);
    }

    [Fact]
    public void ConfigureListenBacklog_RaisesSocketAcceptQueueAboveKestrelDefault()
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseKestrel();
                ApplicationHost.ConfigureListenBacklog(web);
                web.Configure(_ => { });
            })
            .Build();

        var backlog = host.Services.GetRequiredService<IOptions<SocketTransportOptions>>().Value.Backlog;

        Assert.Equal(ApplicationHost.EffectiveListenBacklog, backlog);
        Assert.Equal(ApplicationHost.KestrelListenBacklog, Math.Abs(backlog));
        if (OperatingSystem.IsWindows())
        {
            Assert.True(backlog < 0);
        }
    }
}

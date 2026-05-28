using KasseAPI_Final.Services.Activity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace KasseAPI_Final.Tests;

internal static class ActivityEventTestSupport
{
    public static ActivityEventRecorder CreateRecorder(IActivityEventService? activity = null)
    {
        activity ??= Mock.Of<IActivityEventService>();

        var scope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(IActivityEventService)))
            .Returns(activity);
        scope.SetupGet(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new ActivityEventRecorder(
            scopeFactory.Object,
            Mock.Of<ILogger<ActivityEventRecorder>>());
    }
}

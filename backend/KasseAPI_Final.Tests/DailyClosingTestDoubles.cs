using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Security.Claims;

namespace KasseAPI_Final.Tests;

internal static class DailyClosingTestDoubles
{
    public static DailyClosingService Create(
        AppDbContext ctx,
        ITseService? tse = null,
        ITseKeyProvider? tseKeyProvider = null,
        IRksvEnvironmentService? rksvEnv = null,
        ICurrentUserService? currentUser = null,
        IHttpContextAccessor? httpContextAccessor = null,
        ISettingsTenantResolver? tenantResolver = null,
        string actorUserId = "cashier-test")
    {
        var hostEnvironment = TenantTestDoubles.HostEnvironmentReturning(Environments.Development);
        var configuration = new ConfigurationBuilder().Build();

        tse ??= CreateDefaultTseMock().Object;
        tseKeyProvider ??= Mock.Of<ITseKeyProvider>(p => p.GetCurrentCertificateThumbprint() == "thumb-test");
        rksvEnv ??= new RksvEnvironmentService(configuration, hostEnvironment);
        currentUser ??= Mock.Of<ICurrentUserService>();
        httpContextAccessor ??= CreateHttpContextAccessor(actorUserId);
        tenantResolver ??= TenantTestDoubles.PrimaryTenantResolver;

        return new DailyClosingService(
            ctx,
            tse,
            tseKeyProvider,
            rksvEnv,
            currentUser,
            httpContextAccessor,
            tenantResolver);
    }

    public static Mock<ITseService> CreateDefaultTseMock()
    {
        var mock = new Mock<ITseService>();
        mock.Setup(x => x.CreateDailyClosingSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<int>()))
            .ReturnsAsync("eyJhbGciOiJFUzI1NiJ9.eyJ.test.daily.closing");
        return mock;
    }

    public static IHttpContextAccessor CreateHttpContextAccessor(string actorUserId)
    {
        var identity = new ClaimsIdentity([new Claim("userId", actorUserId)], "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return accessor.Object;
    }
}

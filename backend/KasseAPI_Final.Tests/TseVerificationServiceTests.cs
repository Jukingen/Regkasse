using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class TseVerificationServiceTests
{
    private static TseVerificationService CreateService(
        bool tseSimulated,
        bool isDevelopment = true,
        RksvSignatureVerifyResponse? realResponse = null)
    {
        var realVerify = new Mock<IRksvSignatureVerifyService>();
        realVerify
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(realResponse ?? new RksvSignatureVerifyResponse
            {
                Valid = false,
                Details = "ES256 cryptographic verification failed.",
            });

        var env = new Mock<IRksvEnvironmentService>();
        env.Setup(x => x.IsTseSimulated()).Returns(tseSimulated);

        var host = new Mock<IHostEnvironment>();
        host.Setup(x => x.EnvironmentName).Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new TseVerificationService(
            realVerify.Object,
            env.Object,
            host.Object,
            NullLogger<TseVerificationService>.Instance);
    }

    [Fact]
    public async Task Verify_WhenSimulatedWithCompactJws_ReturnsSimulatedAccepted()
    {
        var service = CreateService(tseSimulated: true);
        var result = await service.VerifySignatureAsync("aaa.bbb.ccc");

        Assert.True(result.IsValid);
        Assert.True(result.IsSimulated);
        Assert.Equal(TseVerificationService.SimulatedDevelopmentMessage, result.Message);
        Assert.Equal("SIMULATED", result.ToVerifyResultCode());
    }

    [Fact]
    public async Task Verify_WhenSimulatedWithSimPrefix_ReturnsSimulatedAccepted()
    {
        var service = CreateService(tseSimulated: true);
        var result = await service.VerifySignatureAsync("sim_test_signature");

        Assert.True(result.IsValid);
        Assert.True(result.IsSimulated);
        Assert.Equal("SIMULATED", result.ToVerifyResultCode());
    }

    [Fact]
    public async Task Verify_WhenSimulatedButEmpty_ReturnsFail()
    {
        var service = CreateService(tseSimulated: true);
        var result = await service.VerifySignatureAsync("   ");

        Assert.False(result.IsValid);
        Assert.False(result.IsSimulated);
        Assert.Equal("FAIL", result.ToVerifyResultCode());
    }

    [Fact]
    public async Task Verify_WhenNotSimulated_UsesRealCrypto()
    {
        var service = CreateService(
            tseSimulated: false,
            isDevelopment: false,
            realResponse: new RksvSignatureVerifyResponse
            {
                Valid = true,
                Details = "ES256 verification succeeded.",
            });

        var result = await service.VerifySignatureAsync("aaa.bbb.ccc");

        Assert.True(result.IsValid);
        Assert.False(result.IsSimulated);
        Assert.Equal("PASS", result.ToVerifyResultCode());
    }

    [Fact]
    public void LooksLikePresentableSignature_RejectsMalformed()
    {
        Assert.False(TseVerificationService.LooksLikePresentableSignature("only.two"));
        Assert.False(TseVerificationService.LooksLikePresentableSignature("a..c"));
        Assert.True(TseVerificationService.LooksLikePresentableSignature("a.b.c"));
        Assert.True(TseVerificationService.LooksLikePresentableSignature("sim_x"));
    }
}

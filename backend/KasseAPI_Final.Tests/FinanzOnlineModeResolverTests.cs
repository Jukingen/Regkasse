using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineModeResolverTests
{
    private static FinanzOnlineCutoverGuardOptions Cutover(bool allowProd, bool requireToken = true, string? token = "approved") =>
        new()
        {
            AllowProdMode = allowProd,
            RequireExplicitProdApproval = requireToken,
            ProdApprovalToken = token
        };

    [Theory]
    [InlineData("Simulation")]
    [InlineData("simulation")]
    [InlineData("Sim")]
    public void ResolveOutboxMode_Simulation_ReturnsTest(string mode)
    {
        var resolved = FinanzOnlineModeResolver.ResolveOutboxMode(mode, Cutover(allowProd: false), out var label);
        Assert.Equal(FinanzOnlineIntegrationMode.TEST, resolved);
        Assert.Equal(FinanzOnlineModeResolver.Simulation, label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Test")]
    [InlineData("TEST")]
    public void ResolveOutboxMode_TestOrDefault_ReturnsTest(string? mode)
    {
        var resolved = FinanzOnlineModeResolver.ResolveOutboxMode(mode, Cutover(allowProd: false), out var label);
        Assert.Equal(FinanzOnlineIntegrationMode.TEST, resolved);
        Assert.Equal(FinanzOnlineModeResolver.Test, label);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Prod")]
    [InlineData("prod")]
    public void ResolveOutboxMode_Production_WithCutover_ReturnsProd(string mode)
    {
        var resolved = FinanzOnlineModeResolver.ResolveOutboxMode(mode, Cutover(allowProd: true), out var label);
        Assert.Equal(FinanzOnlineIntegrationMode.PROD, resolved);
        Assert.Equal(FinanzOnlineModeResolver.Production, label);
    }

    [Fact]
    public void ResolveOutboxMode_Production_WithoutCutover_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FinanzOnlineModeResolver.ResolveOutboxMode(
                "Production",
                Cutover(allowProd: false),
                out _));
    }

    [Fact]
    public void ResolveOutboxMode_Production_MissingApprovalToken_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FinanzOnlineModeResolver.ResolveOutboxMode(
                "Production",
                Cutover(allowProd: true, requireToken: true, token: null),
                out _));
    }

    [Theory]
    [InlineData("Simulation", "Simulation")]
    [InlineData("Production", "Production")]
    [InlineData("Test", "Test")]
    [InlineData(null, "Test")]
    public void ToConfigEnvironmentLabel_MapsExpected(string? mode, string expected)
    {
        Assert.Equal(expected, FinanzOnlineModeResolver.ToConfigEnvironmentLabel(mode));
    }
}

using KasseAPI_Final.Tse;
using Xunit;

namespace KasseAPI_Final.Tests;

public class SignatureDiagnosticSimulationTests
{
    [Fact]
    public void ApplySimulationMode_WhenSimulatedWithSignature_RemapsFailToSimulated()
    {
        var steps = new[]
        {
            new SignatureDiagnosticStep(1, "CMC match", "FAIL", "No cert"),
            new SignatureDiagnosticStep(2, "JWS format", "PASS", "3 parts"),
            new SignatureDiagnosticStep(4, "Signature verify", "FAIL", "ES256 verification failed"),
        };

        var result = SignatureDiagnosticSimulation.ApplySimulationMode(steps, isTseSimulated: true, hasSignature: true);

        Assert.Equal(SignatureDiagnosticSimulation.SimulatedStatus, result[0].Status);
        Assert.StartsWith("TSE simulation:", result[0].Evidence);
        Assert.Equal("PASS", result[1].Status);
        Assert.Equal(SignatureDiagnosticSimulation.SimulatedStatus, result[2].Status);
    }

    [Fact]
    public void ApplySimulationMode_WhenNotSimulated_LeavesFailUnchanged()
    {
        var steps = new[]
        {
            new SignatureDiagnosticStep(4, "Signature verify", "FAIL", "ES256 verification failed"),
        };

        var result = SignatureDiagnosticSimulation.ApplySimulationMode(steps, isTseSimulated: false, hasSignature: true);

        Assert.Equal("FAIL", result[0].Status);
        Assert.Equal("ES256 verification failed", result[0].Evidence);
    }

    [Fact]
    public void ApplySimulationMode_WhenNoSignature_LeavesFailUnchanged()
    {
        var steps = new[]
        {
            new SignatureDiagnosticStep(2, "JWS format", "FAIL", "Empty TseSignature"),
        };

        var result = SignatureDiagnosticSimulation.ApplySimulationMode(steps, isTseSimulated: true, hasSignature: false);

        Assert.Equal("FAIL", result[0].Status);
    }
}

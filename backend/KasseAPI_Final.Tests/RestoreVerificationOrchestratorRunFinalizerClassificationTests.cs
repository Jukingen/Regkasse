using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationOrchestratorRunFinalizerClassificationTests
{
    [Fact]
    public void Classify_maps_JsonNode_parent_exception_to_stable_code()
    {
        var ex = new InvalidOperationException("The node already has a parent.");
        var (code, detail) = RestoreVerificationOrchestratorRunFinalizer.ClassifyUnhandledOrchestratorException(ex);
        Assert.Equal("RESTORE_DRILL_JSON_NODE_PARENT_CONFLICT", code);
        Assert.Contains("JsonNode", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_defaults_to_unhandled_for_other_exceptions()
    {
        var ex = new InvalidOperationException("Something else");
        var (code, _) = RestoreVerificationOrchestratorRunFinalizer.ClassifyUnhandledOrchestratorException(ex);
        Assert.Equal("UNHANDLED_EXCEPTION", code);
    }
}

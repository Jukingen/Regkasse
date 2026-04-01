using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Regression: <see cref="JsonObject"/> rejects attaching the same <see cref="JsonNode"/> under two keys
/// (InvalidOperationException: The node already has a parent). Production code uses two independent
/// <see cref="JsonSerializer.SerializeToNode(object?)"/> calls for the same payload, or <see cref="JsonNode.DeepClone"/>.
/// </summary>
public sealed class RestoreVerificationJsonNodeParentRegressionTests
{
    [Fact]
    public void JsonObject_rejects_same_JsonNode_under_two_keys()
    {
        var details = new JsonObject();
        var inspectionNode = JsonSerializer.SerializeToNode(new
        {
            passed = true,
            exitCode = 0,
            lineCount = 1,
            kind = "pg_restore_list_toc_inspection_not_checksum"
        });
        Assert.NotNull(inspectionNode);
        details["pgRestoreList"] = inspectionNode;
        var ex = Assert.Throws<InvalidOperationException>(() => details["dumpInspection"] = inspectionNode);
        Assert.Contains("parent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JsonObject_allows_duplicate_content_via_DeepClone()
    {
        var details = new JsonObject();
        var inspectionNode = JsonSerializer.SerializeToNode(new
        {
            passed = true,
            exitCode = 0,
            lineCount = 1,
            kind = "pg_restore_list_toc_inspection_not_checksum"
        });
        Assert.NotNull(inspectionNode);
        details["pgRestoreList"] = inspectionNode;
        details["dumpInspection"] = inspectionNode.DeepClone();
        Assert.Equal(2, details.Count);
        Assert.Equal(inspectionNode.ToJsonString(), details["dumpInspection"]!.ToJsonString());
    }

    [Fact]
    public void JsonObject_allows_same_payload_under_two_keys_via_two_SerializeToNode_calls()
    {
        var details = new JsonObject();
        var payload = new
        {
            passed = true,
            exitCode = 0,
            lineCount = 1,
            kind = "pg_restore_list_toc_inspection_not_checksum"
        };
        details["pgRestoreList"] = JsonSerializer.SerializeToNode(payload);
        details["dumpInspection"] = JsonSerializer.SerializeToNode(payload);
        Assert.Equal(2, details.Count);
        Assert.NotSame(details["pgRestoreList"], details["dumpInspection"]);
        Assert.Equal(details["pgRestoreList"]!.ToJsonString(), details["dumpInspection"]!.ToJsonString());
    }
}

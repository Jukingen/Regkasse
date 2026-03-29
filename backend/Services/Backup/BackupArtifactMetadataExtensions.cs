using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services.Backup;

internal static class BackupArtifactMetadataExtensions
{
    public static string MergePipelineFragment(string? existingMetadataJson, object pipelineFragment)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(existingMetadataJson))
            root = new JsonObject();
        else
        {
            try
            {
                var parsed = JsonNode.Parse(existingMetadataJson);
                root = parsed as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }

        var fragmentNode = JsonSerializer.SerializeToNode(pipelineFragment);
        root["pipeline"] = fragmentNode;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}

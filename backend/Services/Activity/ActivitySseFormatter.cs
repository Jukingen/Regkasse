using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Services.Activity;

internal static class ActivitySseFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task WriteAsync(
        HttpResponse response,
        ActivityStreamMessage message,
        CancellationToken cancellationToken = default)
    {
        var json = message.Data == null
            ? "{}"
            : JsonSerializer.Serialize(message.Data, JsonOptions);

        var builder = new StringBuilder();
        builder.Append("event: ").Append(message.EventName).Append('\n');
        builder.Append("data: ").Append(json).Append("\n\n");

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await response.Body.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

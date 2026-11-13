using System.Globalization;
using System.Text.Json.Nodes;
using KasseAPI_Final.Models;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// Adds enum member names parallel to each C# enum schema so generated clients
/// emit <c>TenantCountryChanged</c> instead of <c>NUMBER_97</c>.
/// <c>x-enum-varnames</c> is the OpenAPI generator convention.
/// Orval 6.31 reads <c>x-enumNames</c>, so both extensions carry the same array.
/// </summary>
public sealed class EnumVarnamesSchemaFilter : ISchemaFilter
{
    public const string ExtensionName = "x-enum-varnames";

    /// <summary>Extension key read by Orval 6.31 (<c>originalSchema["x-enumNames"]</c>).</summary>
    public const string OrvalExtensionName = "x-enumNames";

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema openApiSchema)
            return;

        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum)
            return;

        if (enumType == typeof(ActivityEventType))
            PublishActivityEventTypeAsMemberNames(openApiSchema);

        if (openApiSchema.Enum is not { Count: > 0 } enumValues)
            return;

        if (!TryBuildVarnames(enumType, enumValues, out var names))
            return;

        openApiSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        openApiSchema.Extensions[ExtensionName] = new JsonNodeExtension(ToJsonArray(names));
        openApiSchema.Extensions[OrvalExtensionName] = new JsonNodeExtension(ToJsonArray(names));
    }

    private static JsonArray ToJsonArray(IReadOnlyList<string> names)
    {
        var array = new JsonArray();
        foreach (var name in names)
            array.Add(JsonValue.Create(name));
        return array;
    }

    /// <summary>
    /// Activity feed JSON is the member name (<c>ActivityDto.Type</c> converter).
    /// A type-level converter would make Swashbuckle collapse dictionary keys that share
    /// an underlying value (<c>ChMwstQrBuilt</c> and <c>PermissionRequested</c> are both 110).
    /// </summary>
    private static void PublishActivityEventTypeAsMemberNames(OpenApiSchema schema)
    {
        var names = Enum.GetNames(typeof(ActivityEventType));
        schema.Enum = names.Select(name => (JsonNode)JsonValue.Create(name)!).ToList();
        schema.Type = JsonSchemaType.String;
        schema.Format = null;
    }

    /// <summary>
    /// Names follow <paramref name="enumValues"/> (the OpenAPI <c>enum</c> array),
    /// not source declaration order. <see cref="AuditEventType.Other"/> is 99 but
    /// is declared after later values.
    /// </summary>
    internal static bool TryBuildVarnames(Type enumType, IList<JsonNode> enumValues, out List<string> varnames)
    {
        varnames = [];
        var byNumber = new Dictionary<long, string>();
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var raw in Enum.GetValues(enumType))
        {
            var name = Enum.GetName(enumType, raw!);
            if (name is null)
                continue;

            byNumber.TryAdd(Convert.ToInt64(raw, CultureInfo.InvariantCulture), name);
            byName.TryAdd(name, name);
        }

        foreach (var node in enumValues)
        {
            if (!TryReadToken(node, out var number, out var text))
                return false;

            string? resolved = null;
            if (number is long numeric && byNumber.TryGetValue(numeric, out var numericName))
                resolved = numericName;
            else if (text is not null && byName.TryGetValue(text, out var stringName))
                resolved = stringName;
            else if (text is not null)
                resolved = text;

            if (resolved is null)
                return false;

            varnames.Add(resolved);
        }

        return varnames.Count == enumValues.Count;
    }

    private static bool TryReadToken(JsonNode? node, out long? number, out string? text)
    {
        number = null;
        text = null;
        if (node is null)
            return false;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var s))
            {
                text = s;
                return true;
            }

            if (value.TryGetValue<long>(out var asLong))
            {
                number = asLong;
                return true;
            }

            if (value.TryGetValue<int>(out var asInt))
            {
                number = asInt;
                return true;
            }

            if (value.TryGetValue<double>(out var asDouble)
                && asDouble is >= long.MinValue and <= long.MaxValue
                && asDouble == Math.Truncate(asDouble))
            {
                number = (long)asDouble;
                return true;
            }
        }

        var raw = node.ToJsonString();
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            number = parsed;
            return true;
        }

        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            text = raw[1..^1];
            return true;
        }

        return false;
    }
}

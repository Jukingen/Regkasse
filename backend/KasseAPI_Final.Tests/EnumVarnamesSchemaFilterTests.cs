using System.Text.Json.Nodes;
using KasseAPI_Final.Models;
using KasseAPI_Final.Swagger;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace KasseAPI_Final.Tests;

public class EnumVarnamesSchemaFilterTests
{
    [Fact]
    public void Apply_EmitsVarnamesParallelToEnum_NotDeclarationOrder()
    {
        // Other = 99 is declared after QrRechnungPayloadBuilt = 111.
        var schema = new OpenApiSchema
        {
            Enum = new List<JsonNode>
            {
                JsonValue.Create(97)!,
                JsonValue.Create(99)!,
                JsonValue.Create(111)!,
            },
        };

        new EnumVarnamesSchemaFilter().Apply(schema, ContextFor(typeof(AuditEventType)));

        var names = ReadNames(schema);
        Assert.Equal(new[] { "TenantCountryChanged", "Other", "QrRechnungPayloadBuilt" }, names);
        Assert.True(schema.Extensions!.ContainsKey(EnumVarnamesSchemaFilter.OrvalExtensionName));
    }

    [Fact]
    public void Apply_PublishesActivityEventTypeAsMemberNameStrings()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Format = "int32",
            Enum = new List<JsonNode>
            {
                JsonValue.Create(0)!,
                JsonValue.Create(110)!,
            },
        };

        new EnumVarnamesSchemaFilter().Apply(schema, ContextFor(typeof(ActivityEventType)));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Null(schema.Format);
        var published = schema.Enum!.Select(node => node!.GetValue<string>()).ToArray();
        Assert.Equal(Enum.GetNames(typeof(ActivityEventType)), published);
        Assert.Contains("PermissionRequested", published);
        Assert.Contains("ChMwstQrBuilt", published);
        Assert.Equal(published, ReadNames(schema));
    }

    [Fact]
    public void Apply_SkipsNonEnumSchemas()
    {
        var schema = new OpenApiSchema
        {
            Enum = new List<JsonNode> { JsonValue.Create("standard")! },
        };

        new EnumVarnamesSchemaFilter().Apply(schema, ContextFor(typeof(string)));

        Assert.True(schema.Extensions is null || !schema.Extensions.ContainsKey(EnumVarnamesSchemaFilter.ExtensionName));
    }

    private static SchemaFilterContext ContextFor(Type type) =>
        new(type, schemaGenerator: null!, schemaRepository: new SchemaRepository());

    private static string[] ReadNames(OpenApiSchema schema)
    {
        Assert.NotNull(schema.Extensions);
        var extension = Assert.IsType<JsonNodeExtension>(schema.Extensions[EnumVarnamesSchemaFilter.ExtensionName]);
        return extension.Node!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
    }
}

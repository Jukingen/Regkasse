using KasseAPI_Final.Models;
using KasseAPI_Final.Swagger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Asserts the Swashbuckle document (same pipeline as <c>backend/swagger.json</c>)
/// carries <c>x-enum-varnames</c> for the activity and audit enums.
/// </summary>
[Collection("OpenApiExportWebHost")]
public class EnumVarnamesOpenApiDocumentTests
{
    [Fact]
    public void GeneratedDocument_HasVarnamesForAuditAndActivityEnums()
    {
        var host = SwaggerHostFactory.CreateHost();
        try
        {
            var swagger = host.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
            AssertAudit(swagger);
            AssertActivity(swagger);
        AssertNotificationConfigKeepsAliasKeys(swagger);
        }
        finally
        {
            OpenApiExportMode.ToolingExportActive = false;
            host.Dispose();
        }
    }

    private static void AssertAudit(OpenApiDocument swagger)
    {
        var schema = RequireSchema(swagger, "AuditEventType");
        var names = ReadNames(schema);
        var values = schema.Enum!.Select(node => node!.GetValue<int>()).ToArray();
        Assert.Equal(values.Length, names.Length);
        Assert.Equal("TenantCountryChanged", names[Array.IndexOf(values, (int)AuditEventType.TenantCountryChanged)]);
        Assert.Equal("QrRechnungPayloadBuilt", names[Array.IndexOf(values, (int)AuditEventType.QrRechnungPayloadBuilt)]);
        Assert.Equal("TenantCreatedWithCountry", names[Array.IndexOf(values, (int)AuditEventType.TenantCreatedWithCountry)]);
        Assert.Equal("Other", names[Array.IndexOf(values, (int)AuditEventType.Other)]);
    }

    private static void AssertActivity(OpenApiDocument swagger)
    {
        var schema = RequireSchema(swagger, "ActivityEventType");
        Assert.Equal(JsonSchemaType.String, schema.Type);
        var names = ReadNames(schema);
        var published = schema.Enum!.Select(node => node!.GetValue<string>()).ToArray();
        Assert.Equal(published, names);
        Assert.Contains(nameof(ActivityEventType.UserCreated), names);
        Assert.Contains(nameof(ActivityEventType.QrRechnungPayloadBuilt), names);
        Assert.Contains(nameof(ActivityEventType.TenantCountryChanged), names);
        Assert.Contains(nameof(ActivityEventType.PermissionRequested), names);
        Assert.Contains(nameof(ActivityEventType.ChMwstQrBuilt), names);
    }

    private static void AssertNotificationConfigKeepsAliasKeys(OpenApiDocument swagger)
    {
        var config = RequireSchema(swagger, "NotificationConfig");
        Assert.NotNull(config.Properties);
        Assert.True(config.Properties.TryGetValue("enabledEvents", out var enabled));
        var enabledSchema = Assert.IsType<OpenApiSchema>(enabled);
        Assert.NotNull(enabledSchema.Properties);
        Assert.Contains(nameof(ActivityEventType.PermissionRequested), enabledSchema.Properties.Keys);
        Assert.Contains(nameof(ActivityEventType.ChMwstQrBuilt), enabledSchema.Properties.Keys);
    }

    private static OpenApiSchema RequireSchema(OpenApiDocument swagger, string name)
    {
        var schemas = swagger.Components?.Schemas;
        Assert.NotNull(schemas);
        Assert.True(schemas.TryGetValue(name, out var schema), $"Missing schema {name}");
        return Assert.IsType<OpenApiSchema>(schema);
    }

    private static string[] ReadNames(OpenApiSchema schema)
    {
        Assert.NotNull(schema.Extensions);
        Assert.True(schema.Extensions.TryGetValue(EnumVarnamesSchemaFilter.ExtensionName, out var extension));
        Assert.True(schema.Extensions.ContainsKey(EnumVarnamesSchemaFilter.OrvalExtensionName));
        var node = Assert.IsType<JsonNodeExtension>(extension);
        return node.Node!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
    }
}

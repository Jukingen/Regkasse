using System.Text.Json;
using Xunit;

namespace KasseAPI_Final.Tests.Contract;

/// <summary>
/// Guards committed OpenAPI for POS initiate, payment webhooks, and admin online-payment test console.
/// </summary>
public sealed class OnlinePaymentContractTests
{
    private static string ResolveSwaggerPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "swagger.json"));
        if (File.Exists(candidate))
            return candidate;

        var fallback = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "swagger.json"));
        if (File.Exists(fallback))
            return fallback;

        throw new InvalidOperationException(
            $"Could not locate swagger.json from BaseDirectory={baseDir}. Tried: {candidate}, {fallback}");
    }

    [Fact]
    public void SwaggerJson_Contains_OnlinePayment_Paths_And_Schemas()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ResolveSwaggerPath()));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("openapi", out var openapiEl));
        Assert.StartsWith("3.", openapiEl.GetString(), StringComparison.Ordinal);

        Assert.True(root.TryGetProperty("paths", out var paths));
        AssertPathMethod(paths, "/api/pos/payment/initiate", "post");
        AssertPathMethod(paths, "/api/pos/payment/initiate/{id}", "get");
        AssertPathMethod(paths, "/api/webhooks/payment/{provider}", "post");
        AssertPathMethod(paths, "/api/admin/online-payments", "get");
        AssertPathMethod(paths, "/api/admin/online-payments/{id}", "get");
        AssertPathMethod(paths, "/api/admin/online-payments/test", "post");

        var initiate = paths.GetProperty("/api/pos/payment/initiate").GetProperty("post");
        Assert.True(initiate.TryGetProperty("requestBody", out var body));
        var schemaRef = body.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        AssertHasSchemaRef(schemaRef, "InitiateOnlinePaymentRequest");

        var testPost = paths.GetProperty("/api/admin/online-payments/test").GetProperty("post");
        Assert.True(testPost.TryGetProperty("requestBody", out var testBody));
        var testSchema = testBody.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        AssertHasSchemaRef(testSchema, "AdminOnlinePaymentTestRequest");

        Assert.True(root.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        foreach (var name in new[]
                 {
                     "InitiateOnlinePaymentRequest",
                     "OnlinePaymentDto",
                     "AdminOnlinePaymentDto",
                     "AdminOnlinePaymentListResponse",
                     "AdminOnlinePaymentTestRequest",
                     "AdminOnlinePaymentTestResponse",
                     "PaymentWebhookReceivedResponse"
                 })
        {
            Assert.True(schemas.TryGetProperty(name, out _), $"Missing schema: {name}");
        }

        var initiateSchema = schemas.GetProperty("InitiateOnlinePaymentRequest");
        Assert.True(initiateSchema.TryGetProperty("properties", out var initiateProps));
        foreach (var prop in new[] { "cashRegisterId", "amount", "method", "idempotencyKey" })
            Assert.True(initiateProps.TryGetProperty(prop, out _), $"InitiateOnlinePaymentRequest missing {prop}");

        var dto = schemas.GetProperty("OnlinePaymentDto");
        Assert.True(dto.TryGetProperty("properties", out var dtoProps));
        foreach (var prop in new[] { "id", "status", "amount", "redirectUrl", "paymentIntentId" })
            Assert.True(dtoProps.TryGetProperty(prop, out _), $"OnlinePaymentDto missing {prop}");
    }

    [Fact]
    public void SwaggerJson_WebhookPath_IsPublicPost()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ResolveSwaggerPath()));
        Assert.True(doc.RootElement.TryGetProperty("paths", out var paths));
        var webhook = paths.GetProperty("/api/webhooks/payment/{provider}").GetProperty("post");
        Assert.True(webhook.TryGetProperty("parameters", out var parameters));
        var providerParam = parameters.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "provider");
        Assert.Equal("path", providerParam.GetProperty("in").GetString());
    }

    private static void AssertPathMethod(JsonElement paths, string route, string method)
    {
        Assert.True(paths.TryGetProperty(route, out var pathItem), $"Missing path: {route}");
        Assert.True(pathItem.TryGetProperty(method, out _), $"Missing {method.ToUpperInvariant()} on {route}");
    }

    private static void AssertHasSchemaRef(JsonElement schema, string expectedName)
    {
        if (schema.TryGetProperty("$ref", out var r))
        {
            Assert.Contains(expectedName, r.GetString(), StringComparison.Ordinal);
            return;
        }

        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var item in allOf.EnumerateArray())
            {
                if (item.TryGetProperty("$ref", out var nested)
                    && nested.GetString()?.Contains(expectedName, StringComparison.Ordinal) == true)
                    return;
            }
        }

        Assert.Fail($"Expected schema $ref containing {expectedName}");
    }
}

using System.Text.Json;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Guards committed OpenAPI (backend/swagger.json) for Admin/POS payment and cart routes used by clients.
/// Mirrors scripts/validate-critical-openapi-paths.mjs — keep both in sync when adding critical routes.
/// </summary>
public class OpenApiCriticalPathsContractTests
{
    private static string ResolveSwaggerPath()
    {
        var baseDir = AppContext.BaseDirectory;
        // KasseAPI_Final.Tests uses BaseOutputPath ..\bin\Tests\ — swagger lives next to KasseAPI_Final.csproj.
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "swagger.json"));
        if (File.Exists(candidate))
            return candidate;

        // Fallback: default SDK layout under test project folder.
        var fallback = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "swagger.json"));
        if (File.Exists(fallback))
            return fallback;

        throw new InvalidOperationException(
            $"Could not locate swagger.json from BaseDirectory={baseDir}. Tried: {candidate}, {fallback}");
    }

    [Fact]
    public void SwaggerJson_Contains_Critical_Pos_And_Admin_Payment_Paths_And_Schemas()
    {
        var path = ResolveSwaggerPath();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("openapi", out var openapiEl));
        var openapi = openapiEl.GetString();
        Assert.NotNull(openapi);
        Assert.StartsWith("3.", openapi, StringComparison.Ordinal);

        Assert.True(root.TryGetProperty("paths", out var paths));
        AssertPathMethod(paths, "/api/pos/payment", "post");
        AssertPathMethod(paths, "/api/pos/payment/methods", "get");
        AssertPathMethod(paths, "/api/pos/payment/{id}", "get");
        AssertPathMethod(paths, "/api/pos/cart/current", "get");
        AssertPathMethod(paths, "/api/admin/payments", "get");
        AssertPathMethod(paths, "/api/admin/payments/{id}", "get");

        Assert.True(root.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        foreach (var name in new[] { "CreatePaymentRequest", "AdminPaymentsListResponse", "PaymentMethod" })
        {
            Assert.True(schemas.TryGetProperty(name, out _), $"Missing schema: {name}");
        }
    }

    [Fact]
    public void SwaggerJson_Omits_LegacyCartPaymentProductAliases_And_DeprecatedFinanzOnlineSubmit()
    {
        var path = ResolveSwaggerPath();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(doc.RootElement.TryGetProperty("paths", out var paths));

        foreach (var legacy in new[]
                 {
                     "/api/Cart/current",
                     "/api/Payment",
                     "/api/Product",
                     "/api/FinanzOnline/submit-invoice"
                 })
        {
            Assert.False(paths.TryGetProperty(legacy, out _),
                $"Committed OpenAPI must not expose retired route: {legacy}");
        }
    }

    private static void AssertPathMethod(JsonElement paths, string route, string method)
    {
        Assert.True(paths.TryGetProperty(route, out var pathItem), $"Missing path: {route}");
        Assert.True(pathItem.TryGetProperty(method, out _), $"Missing {method.ToUpperInvariant()} on {route}");
    }
}

using KasseAPI_Final.Controllers;
using KasseAPI_Final.Services;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// Tightens Tagesabschluss schema required fields so generated clients
/// reflect stable payload contracts instead of weak optional-only objects.
/// </summary>
public sealed class TagesabschlussSchemaRequiredFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema openApiSchema)
            return;

        if (openApiSchema.Properties == null || openApiSchema.Properties.Count == 0)
            return;

        if (context.Type == typeof(TagesabschlussResult))
        {
            Require(openApiSchema, "success", "closingDate", "totalAmount", "totalTaxAmount", "transactionCount", "paymentsWithoutInvoiceCount");
            return;
        }

        if (context.Type == typeof(TagesabschlussCanCloseResponse))
        {
            Require(openApiSchema, "canClose", "paymentsWithoutInvoiceCount");
            return;
        }

        if (context.Type == typeof(TagesabschlussStatisticsResponse))
        {
            Require(openApiSchema, "totalClosings", "totalAmount", "totalTaxAmount", "totalTransactions", "averageDailyAmount");
        }
    }

    private static void Require(OpenApiSchema schema, params string[] propertyNames)
    {
        schema.Required ??= new HashSet<string>();
        foreach (var name in propertyNames)
        {
            if (schema.Properties.ContainsKey(name))
                schema.Required.Add(name);
        }
    }
}

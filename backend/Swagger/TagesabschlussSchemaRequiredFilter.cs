using KasseAPI_Final.Controllers;
using KasseAPI_Final.Services;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// Tightens Tagesabschluss schema required fields so generated clients
/// reflect stable payload contracts instead of weak optional-only objects.
/// </summary>
public sealed class TagesabschlussSchemaRequiredFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties == null || schema.Properties.Count == 0)
            return;

        if (context.Type == typeof(TagesabschlussResult))
        {
            Require(schema, "success", "closingDate", "totalAmount", "totalTaxAmount", "transactionCount", "paymentsWithoutInvoiceCount");
            return;
        }

        if (context.Type == typeof(TagesabschlussCanCloseResponse))
        {
            Require(schema, "canClose", "paymentsWithoutInvoiceCount");
            return;
        }

        if (context.Type == typeof(TagesabschlussStatisticsResponse))
        {
            Require(schema, "totalClosings", "totalAmount", "totalTaxAmount", "totalTransactions", "averageDailyAmount");
            return;
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

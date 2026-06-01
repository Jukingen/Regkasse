using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger
{
    /// <summary>
    /// PaymentItemRequest taxType alanı için Swagger açıklaması ve enum değerleri.
    /// ZeroRate: 0% VAT (Österreich 2026 Reform). Exempt deprecated.
    /// </summary>
    public class TaxTypeSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.Name != "PaymentItemRequest")
                return;

            if (schema is not OpenApiSchema openApiSchema)
                return;

            if (openApiSchema.Properties?.TryGetValue("taxType", out var taxTypeProp) == true
                && taxTypeProp is OpenApiSchema taxTypeSchema)
            {
                taxTypeSchema.Description = "Vergi tipi. standard(20%), reduced(10%), special(13%), zerorate(0% - Österreich 2026). "
                    + "Deprecated: exempt → zerorate, int (1-4) → string tercih edilir.";
                taxTypeSchema.Enum =
                [
                    JsonValue.Create("standard"),
                    JsonValue.Create("reduced"),
                    JsonValue.Create("special"),
                    JsonValue.Create("zerorate")
                ];
            }
        }
    }
}

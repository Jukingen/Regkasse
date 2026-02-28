using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger
{
    /// <summary>
    /// PaymentItemRequest taxType alanı için Swagger açıklaması ve enum değerleri.
    /// ZeroRate: 0% VAT (Österreich 2026 Reform). Exempt deprecated.
    /// </summary>
    public class TaxTypeSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.Name != "PaymentItemRequest")
                return;

            if (schema.Properties?.TryGetValue("taxType", out var taxTypeProp) == true)
            {
                taxTypeProp.Description = "Vergi tipi. standard(20%), reduced(10%), special(13%), zerorate(0% - Österreich 2026). "
                    + "Deprecated: exempt → zerorate, int (1-4) → string tercih edilir.";
                taxTypeProp.Enum = new List<IOpenApiAny>
                {
                    new OpenApiString("standard"),
                    new OpenApiString("reduced"),
                    new OpenApiString("special"),
                    new OpenApiString("zerorate")
                };
            }
        }
    }
}

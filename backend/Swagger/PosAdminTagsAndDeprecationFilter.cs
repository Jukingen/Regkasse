using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// Assigns POS / Admin tags for <c>/api/pos/*</c> and <c>/api/admin/*</c>. Legacy alias routes are omitted from OpenAPI via
/// <see cref="LegacySwaggerPathExclusions"/> (DocInclusionPredicate in <c>Program.cs</c>).
/// </summary>
public class PosAdminTagsAndDeprecationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;

        // Tag: POS veya Admin (Swagger gruplaması)
        if (path.StartsWith("api/pos/", StringComparison.OrdinalIgnoreCase) || path.Equals("api/pos", StringComparison.OrdinalIgnoreCase))
        {
            operation.Tags = new List<OpenApiTag> { new() { Name = "POS" } };
        }
        else if (path.StartsWith("api/admin/", StringComparison.OrdinalIgnoreCase) || path.Equals("api/admin", StringComparison.OrdinalIgnoreCase))
        {
            operation.Tags = new List<OpenApiTag> { new() { Name = "Admin" } };
        }
    }
}

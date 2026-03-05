using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KasseAPI_Final.Swagger;

/// <summary>
/// Legacy route'ları Swagger'da deprecated işaretler; /api/pos ve /api/admin path'lerine POS/Admin tag atar.
/// </summary>
public class PosAdminTagsAndDeprecationFilter : IOperationFilter
{
    // api/Categories kaldırıldı (sadece api/admin/categories kullanılıyor)
    private static readonly string[] LegacyPrefixes = { "api/Product", "api/Cart", "api/Payment" };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;

        // Legacy path'leri deprecated yap (yeni kullanım: /api/pos/... veya /api/admin/...)
        if (LegacyPrefixes.Any(p => path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase) || path.Equals(p, StringComparison.OrdinalIgnoreCase)))
        {
            operation.Deprecated = true;
            var deprecationNote = "Deprecated. Use /api/pos/... for POS or /api/admin/... for Admin.";
            operation.Description = string.IsNullOrEmpty(operation.Description)
                ? deprecationNote
                : operation.Description + "\n\n**" + deprecationNote + "**";
        }

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

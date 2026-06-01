using System.Text.Json.Nodes;
using Microsoft.OpenApi;
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

        if (string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            && path.Equals("api/license/activate", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Activate license (unified POS + FA)";
            operation.Description =
                "Single activation contract for POS and frontend-admin. **Authentication is optional** (POS may activate before login; "
                + "FA typically sends Bearer JWT for audit: initiating user id + `app_context` claim). "
                + "When unauthenticated, send header `X-App-Context: pos` or `X-App-Context: admin` so activation attempts record the source app. "
                + "Machine binding uses this server's canonical fingerprint (`ILicenseStorageService`); optional `machineFingerprint` in JSON or "
                + "`X-Machine-Fingerprint` must match the server when provided.";
            operation.Tags = new HashSet<OpenApiTagReference> { new("License") };
        }

        if (path.StartsWith("api/pos/", StringComparison.OrdinalIgnoreCase) || path.Equals("api/pos", StringComparison.OrdinalIgnoreCase))
        {
            operation.Tags = new HashSet<OpenApiTagReference> { new("POS") };
        }
        else if (path.StartsWith("api/admin/", StringComparison.OrdinalIgnoreCase) || path.Equals("api/admin", StringComparison.OrdinalIgnoreCase))
        {
            operation.Tags = new HashSet<OpenApiTagReference> { new("Admin") };
        }

        if (path.Equals("api/admin/development-mode/settings", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                operation.Summary = "Get development mode settings (singleton)";
                operation.Description =
                    "Reads the persisted development-mode singleton from PostgreSQL (cached ~30s on the server). "
                    + "Requires permission **system.critical** (default role matrix: SuperAdmin only). "
                    + "Response includes `updatedBy` (email) when `updated_by_user_id` matches an Identity user id.";
            }
            else if (string.Equals(context.ApiDescription.HttpMethod, "PUT", StringComparison.OrdinalIgnoreCase))
            {
                operation.Summary = "Update development mode settings (singleton)";
                operation.Description =
                    "Replaces development-mode toggles and feature list. Requires **system.critical**. "
                    + "Updates are rejected when the API host environment is not **Development** (see `DevelopmentModeService`). "
                    + "On success the server reloads its in-memory settings cache and appends a system audit event.";
            }
        }
    }
}

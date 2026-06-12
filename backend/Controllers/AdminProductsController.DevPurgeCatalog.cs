using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Dev;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public partial class AdminProductsController
{
    /// <summary>
    /// Development-only hard delete of all tenant products (and optionally categories).
    /// POST api/admin/products/dev/purge-catalog
    /// </summary>
    [HttpPost("dev/purge-catalog")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<IActionResult> DevPurgeTenantCatalog(
        [FromBody] DevTenantCatalogCleanupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    success = false,
                    message = "Dev catalog purge is only available when ASPNETCORE_ENVIRONMENT=Development. Restart the backend after pulling latest code.",
                });
        }

        if (request.ConfirmPhrase != DevTenantCatalogCleanup.ConfirmPhrase
            && request.ConfirmPhrase != DevTenantCatalogCleanup.FiscalOverridePhrase)
        {
            return ErrorResponse(
                $"Invalid confirm phrase. Use {DevTenantCatalogCleanup.ConfirmPhrase}.",
                400);
        }

        try
        {
            var tenantId = await ResolveDevPurgeTenantIdAsync(request, cancellationToken).ConfigureAwait(false);
            if (tenantId == Guid.Empty)
                return ErrorResponse("Tenant could not be resolved.", 400);

            var result = await DevTenantCatalogCleanup.ExecuteAsync(
                _context,
                tenantId,
                request.IncludeCategories,
                allowFiscalOverride: request.ConfirmPhrase == DevTenantCatalogCleanup.FiscalOverridePhrase,
                cancellationToken).ConfigureAwait(false);

            _logger.LogWarning(
                "Dev tenant catalog purge via admin API: tenant={TenantId} products={Products} categories={Categories}",
                result.TenantId,
                result.ProductsDeleted,
                result.CategoriesDeleted);

            return SuccessResponse(result, "Tenant catalog purged successfully");
        }
        catch (InvalidOperationException ex)
        {
            return ErrorResponse(ex.Message, 400);
        }
        catch (Exception ex)
        {
            return HandleException(ex, "AdminProducts.DevPurgeTenantCatalog");
        }
    }

    private async Task<Guid> ResolveDevPurgeTenantIdAsync(
        DevTenantCatalogCleanupRequest request,
        CancellationToken cancellationToken)
    {
        if (IsSuperAdminActor())
        {
            if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
                return request.TenantId.Value;

            if (!string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                var slug = request.TenantSlug.Trim();
                var tenant = await _context.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.Slug.ToLower() == slug.ToLower(),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (tenant != null)
                    return tenant.Id;

                throw new InvalidOperationException($"Tenant slug '{slug}' was not found.");
            }
        }

        return await _settingsTenantResolver.ResolveEffectiveTenantIdAsync().ConfigureAwait(false);
    }

    private bool IsSuperAdminActor() =>
        User.IsInRole(Roles.SuperAdmin)
        || User.Claims.Any(c =>
            (string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
             || string.Equals(c.Type, System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            && string.Equals(c.Value, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
}

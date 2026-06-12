using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public partial class AdminProductsController
{
    public const string DeactivateAllProductsConfirmPhrase = "DEACTIVATE-ALL-PRODUCTS";

    /// <summary>
    /// Soft-deactivate all active products for the current tenant (RKSV: receipt history preserved).
    /// POST api/admin/products/deactivate-all
    /// </summary>
    [HttpPost("deactivate-all")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<IActionResult> DeactivateAllProducts([FromBody] DeactivateAllProductsRequest request)
    {
        try
        {
            if (!string.Equals(
                    request.ConfirmPhrase?.Trim(),
                    DeactivateAllProductsConfirmPhrase,
                    StringComparison.Ordinal))
            {
                return ErrorResponse(
                    $"Invalid confirm phrase. Use {DeactivateAllProductsConfirmPhrase}.",
                    400);
            }

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var products = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .ToListAsync();

            var actor = User.Identity?.Name ?? "system";
            var now = DateTime.UtcNow;
            var deactivated = 0;
            var alreadyInactive = 0;

            foreach (var product in products)
            {
                if (!product.IsActive)
                {
                    alreadyInactive++;
                    continue;
                }

                product.IsActive = false;
                product.UpdatedAt = now;
                product.UpdatedBy = actor;
                deactivated++;
            }

            if (deactivated > 0)
                await _context.SaveChangesAsync();

            _logger.LogWarning(
                "Admin deactivate-all products: tenant={TenantId} deactivated={Deactivated} alreadyInactive={AlreadyInactive} actor={Actor}",
                tenantId,
                deactivated,
                alreadyInactive,
                actor);

            return SuccessResponse(
                new DeactivateAllProductsResult
                {
                    Deactivated = deactivated,
                    AlreadyInactive = alreadyInactive,
                    TotalProducts = products.Count,
                },
                deactivated > 0
                    ? "All active products deactivated successfully"
                    : "No active products to deactivate");
        }
        catch (Exception ex)
        {
            return HandleException(ex, "AdminProducts.DeactivateAllProducts");
        }
    }
}

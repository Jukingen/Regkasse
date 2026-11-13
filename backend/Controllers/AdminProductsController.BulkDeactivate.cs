using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public partial class AdminProductsController
{
    private const int BulkDeactivateMaxBatchSize = 500;

    /// <summary>
    /// Soft-deactivate multiple products (RKSV: historical receipts keep product references).
    /// POST api/admin/products/bulk-deactivate
    /// </summary>
    [HttpPost("bulk-deactivate")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<IActionResult> BulkDeactivateProducts([FromBody] BulkDeactivateProductsRequest request)
    {
        try
        {
            if (request.ProductIds == null || request.ProductIds.Count == 0)
                return ErrorResponse("At least one product id is required.", 400);

            if (request.ProductIds.Count > BulkDeactivateMaxBatchSize)
                return ErrorResponse($"Maximum {BulkDeactivateMaxBatchSize} products per request.", 400);

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var distinctIds = request.ProductIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (distinctIds.Count == 0)
                return ErrorResponse("At least one valid product id is required.", 400);

            var products = await _context.Products
                .Where(p => p.TenantId == tenantId && distinctIds.Contains(p.Id))
                .ToListAsync();

            var foundIds = products.Select(p => p.Id).ToHashSet();
            var notFound = distinctIds.Count(id => !foundIds.Contains(id));
            var alreadyInactive = 0;
            var deactivated = 0;
            var actor = User.Identity?.Name ?? "system";
            var now = DateTime.UtcNow;

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
            {
                await _context.SaveChangesAsync();
                await _productService.InvalidateProductsCacheAsync(tenantId);
            }

            _logger.LogInformation(
                "Admin bulk product deactivate: tenant={TenantId} deactivated={Deactivated} alreadyInactive={AlreadyInactive} notFound={NotFound}",
                tenantId,
                deactivated,
                alreadyInactive,
                notFound);

            return SuccessResponse(
                new BulkDeactivateProductsResult
                {
                    Deactivated = deactivated,
                    AlreadyInactive = alreadyInactive,
                    NotFound = notFound,
                },
                "Products deactivated successfully");
        }
        catch (Exception ex)
        {
            return HandleException(ex, "AdminProducts.BulkDeactivateProducts");
        }
    }
}

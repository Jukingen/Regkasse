using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Imei;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Tenant-wide IMEI catalog. Per-product create/list stays on
/// <c>/api/admin/products/{productId}/imeis</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/product-imeis")]
[Produces("application/json")]
[HasPermission(AppPermissions.ImeiView)]
public sealed class AdminProductImeiCatalogController : ControllerBase
{
    private readonly IProductImeiService _imeis;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminProductImeiCatalogController(
        IProductImeiService imeis,
        ICurrentTenantAccessor tenantAccessor)
    {
        _imeis = imeis;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminProductImeiListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AdminProductImeiListItemDto>>> List(
        [FromQuery] Guid? productId,
        [FromQuery] ProductImeiStatus? status,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var items = await _imeis.SearchAsync(productId, status, cancellationToken).ConfigureAwait(false);
        if (items == null)
            return NotFound();
        return Ok(items);
    }
}

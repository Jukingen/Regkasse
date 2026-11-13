using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Products;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/pos/products/{productId:guid}/variants")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class PosProductVariantsController : ControllerBase
{
    private readonly IProductVariantService _variants;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosProductVariantsController(
        IProductVariantService variants,
        ICurrentTenantAccessor tenantAccessor)
    {
        _variants = variants;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductVariantDto>>> List(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var items = await _variants.ListAsync(productId, cancellationToken).ConfigureAwait(false);
        if (items == null)
            return NotFound();
        return Ok(items);
    }
}

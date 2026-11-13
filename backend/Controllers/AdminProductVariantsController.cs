using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/products/{productId:guid}/variants")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminProductVariantsController : ControllerBase
{
    private readonly IProductVariantService _variants;

    public AdminProductVariantsController(IProductVariantService variants)
    {
        _variants = variants;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductVariantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductVariantDto>>> List(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var items = await _variants.ListAsync(productId, cancellationToken).ConfigureAwait(false);
        if (items == null)
            return NotFound();
        return Ok(items);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProductVariantErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProductVariantErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductVariantDto>> Create(
        Guid productId,
        [FromBody] CreateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _variants
            .CreateAsync(productId, request, ActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result, created: true);
    }

    [HttpPut("{variantId:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProductVariantErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProductVariantErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductVariantDto>> Update(
        Guid productId,
        Guid variantId,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _variants
            .UpdateAsync(productId, variantId, request, ActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result, created: false);
    }

    [HttpDelete("{variantId:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductVariantDto>> Deactivate(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var result = await _variants
            .DeactivateAsync(productId, variantId, ActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result, created: false);
    }

    [HttpPost("{variantId:guid}/stock")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProductVariantErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductVariantDto>> AdjustStock(
        Guid productId,
        Guid variantId,
        [FromBody] AdjustProductVariantStockRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _variants
            .AdjustStockAsync(productId, variantId, request.Delta, ActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return Map(result, created: false);
    }

    private ActionResult<ProductVariantDto> Map(ProductVariantMutation result, bool created) =>
        result switch
        {
            ProductVariantMutation.Ok ok => created
                ? StatusCode(StatusCodes.Status201Created, ok.Variant)
                : Ok(ok.Variant),
            ProductVariantMutation.NotFound => NotFound(),
            ProductVariantMutation.Error error => StatusCode(
                error.StatusCode,
                new ProductVariantErrorDto { Code = error.Code, Message = error.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };

    private string ActorUserId() =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
}

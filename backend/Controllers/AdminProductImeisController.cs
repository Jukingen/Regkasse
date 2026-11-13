using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Imei;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/products/{productId:guid}/imeis")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminProductImeisController : ControllerBase
{
    private readonly IProductImeiService _imeis;

    public AdminProductImeisController(IProductImeiService imeis)
    {
        _imeis = imeis;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductImeiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductImeiDto>>> List(
        Guid productId,
        [FromQuery] ProductImeiStatus? status,
        CancellationToken cancellationToken)
    {
        var items = await _imeis.ListAsync(productId, status, cancellationToken).ConfigureAwait(false);
        if (items == null)
            return NotFound();
        return Ok(items);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(ProductImeiDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProductImeiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProductImeiErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductImeiDto>> Add(
        Guid productId,
        [FromBody] AddProductImeiRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _imeis.AddAsync(productId, request, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            ProductImeiAddResult.Ok ok => StatusCode(StatusCodes.Status201Created, ok.Item),
            ProductImeiAddResult.NotFound => NotFound(),
            ProductImeiAddResult.Error error => StatusCode(
                error.StatusCode,
                new ProductImeiErrorDto { Code = error.Code, Message = error.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public partial class AdminProductsController
{
    private const long DemoTemplateMaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>Download editable demo catalog template (CSV, Excel-compatible). GET api/admin/products/demo/template</summary>
    [HttpGet("demo/template")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
    [Produces("text/csv")]
    public async Task<IActionResult> DownloadDemoTemplate(
        [FromServices] IDemoProductImportService importService,
        CancellationToken cancellationToken = default)
    {
        var bytes = await importService.GetTemplateCsvAsync(cancellationToken).ConfigureAwait(false);
        return File(bytes, "text/csv", "demo-produkt-vorlage.csv");
    }

    /// <summary>Validate uploaded template and return preview. POST api/admin/products/demo/template/preview</summary>
    [HttpPost("demo/template/preview")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DemoTemplateMaxUploadBytes)]
    [ProducesResponseType(typeof(DemoTemplateValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DemoTemplateValidationResultDto>> PreviewDemoTemplate(
        IFormFile? file,
        [FromQuery] int maxRows = 20,
        [FromServices] IDemoProductImportService importService = null!,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > DemoTemplateMaxUploadBytes)
            return BadRequest(new { message = "File exceeds maximum size (10 MB)." });

        await using var stream = file.OpenReadStream();
        var preview = await importService
            .ValidateTemplateFileAsync(stream, file.FileName, Math.Clamp(maxRows, 1, 50), cancellationToken)
            .ConfigureAwait(false);
        return Ok(preview);
    }

    /// <summary>Import products from uploaded template file. POST api/admin/products/demo/template/import</summary>
    [HttpPost("demo/template/import")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Manager)]
    [HasPermission(AppPermissions.ProductManage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DemoTemplateMaxUploadBytes)]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResult>> ImportDemoTemplate(
        IFormFile? file,
        [FromForm] bool overwriteExisting = false,
        [FromForm] string? priceAdjustmentMode = null,
        [FromForm] decimal? priceAdjustmentPercent = null,
        [FromForm] decimal? priceRoundIncrement = null,
        [FromForm] string? imageMode = null,
        [FromServices] IDemoProductImportService importService = null!,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId == null)
            return BadRequest(new { error = "No tenant context" });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > DemoTemplateMaxUploadBytes)
            return BadRequest(new { message = "File exceeds maximum size (10 MB)." });

        var request = new DemoImportRequest
        {
            OverwriteExisting = overwriteExisting,
            PriceAdjustmentMode = priceAdjustmentMode,
            PriceAdjustmentPercent = priceAdjustmentPercent,
            PriceRoundIncrement = priceRoundIncrement,
            ImageMode = imageMode,
        };

        await using var stream = file.OpenReadStream();
        var result = await importService
            .ImportFromTemplateFileAsync(tenantId.Value, stream, file.FileName, request, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage ?? "Template import failed.", result });

        return Ok(result);
    }
}

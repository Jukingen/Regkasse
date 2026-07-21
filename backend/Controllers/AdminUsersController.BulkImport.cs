using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public partial class AdminUsersController
{
    private const long BulkImportMaxUploadBytes = 20 * 1024 * 1024;

    /// <summary>Download CSV template for bulk user import.</summary>
    [HasPermission(AppPermissions.UserManage)]
    [HttpGet("bulk-import/template")]
    [Produces("text/csv")]
    public IActionResult DownloadBulkImportTemplate()
    {
        var csv = "email,username,firstName,lastName,role,tenantSlug\n" +
                  "max.mustermann@example.com,,Max,Mustermann,Cashier,cafe\n";
        return File(Encoding.UTF8.GetBytes("\uFEFF" + csv), "text/csv", "bulk-user-import-template.csv");
    }

    /// <summary>Preview first rows of an import file without creating users.</summary>
    [HasPermission(AppPermissions.UserManage)]
    [HttpPost("bulk-import/preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BulkImportMaxUploadBytes)]
    [ProducesResponseType(typeof(BulkImportPreviewResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkImportPreviewResponseDto>> PreviewBulkImport(
        IFormFile? file,
        [FromQuery] int maxRows = BulkImportRequest.DefaultPreviewRowCount,
        [FromServices] IBulkUserImportService? importService = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > BulkImportMaxUploadBytes)
            return BadRequest(new { message = "File exceeds maximum size (20 MB)." });

        await using var stream = file.OpenReadStream();
        var preview = importService!.Preview(stream, file.FileName, Math.Clamp(maxRows, 1, 50));
        return Ok(preview);
    }

    /// <summary>Start a background bulk import job (supports 1000+ rows; poll job status for progress).</summary>
    [HasPermission(AppPermissions.UserManage)]
    [HttpPost("bulk-import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BulkImportMaxUploadBytes)]
    [ProducesResponseType(typeof(BulkImportStartResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkImportStartResponseDto>> StartBulkImport(
        IFormFile? file,
        [FromServices] IBulkUserImportJobManager jobManager,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (file.Length > BulkImportMaxUploadBytes)
            return BadRequest(new { message = "File exceeds maximum size (20 MB)." });

        var actorId = ActorId;
        if (string.IsNullOrEmpty(actorId))
            return Unauthorized(new { message = "User not authenticated." });

        await using var stream = file.OpenReadStream();
        var (rows, parseError) = BulkUserImportFileParser.Parse(stream, file.FileName);
        if (parseError != null)
            return BadRequest(new { message = parseError });

        if (rows.Count == 0)
            return BadRequest(new { message = "No data rows found." });

        var actor = new BulkImportActorContext(
            actorId,
            ActorRole,
            IsActorSuperAdmin(),
            _tenantAccessor.TenantId);

        var started = await jobManager
            .StartJobAsync(rows, actor, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Bulk import job {JobId} started by {ActorId} with {RowCount} rows",
            started.JobId,
            actorId,
            started.TotalRows);

        return Accepted(started);
    }

    /// <summary>Poll bulk import job progress and final result.</summary>
    [HasPermission(AppPermissions.UserManage)]
    [HttpGet("bulk-import/jobs/{jobId}")]
    [ProducesResponseType(typeof(BulkImportJobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<BulkImportJobStatusDto> GetBulkImportJobStatus(
        string jobId,
        [FromServices] IBulkUserImportJobManager jobManager)
    {
        var status = jobManager.GetStatus(jobId);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>Cancel a running bulk import job.</summary>
    [HasPermission(AppPermissions.UserManage)]
    [HttpDelete("bulk-import/jobs/{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult CancelBulkImportJob(
        string jobId,
        [FromServices] IBulkUserImportJobManager jobManager)
    {
        if (!jobManager.TryCancel(jobId))
        {
            var status = jobManager.GetStatus(jobId);
            if (status == null)
                return NotFound();
            return Conflict(new { message = "Job is not running or already finished." });
        }

        return NoContent();
    }

    /// <summary>Download detailed import results CSV.</summary>
    [HasPermission(AppPermissions.UserManage)]
    [HttpGet("bulk-import/results/{resultId}")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadBulkImportResults(
        string resultId,
        [FromServices] IBulkUserImportResultStore resultStore,
        CancellationToken cancellationToken = default)
    {
        var stream = await resultStore.OpenResultAsync(resultId, cancellationToken).ConfigureAwait(false);
        if (stream == null)
            return NotFound();

        var fileName = Path.GetFileName(resultId);
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            fileName += ".csv";

        return File(stream, "text/csv", fileName);
    }
}

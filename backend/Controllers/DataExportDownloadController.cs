using KasseAPI_Final.Services.DataExport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Token-gated public download for GDPR data-export packages (7-day expiry).
/// Production URL shape: <c>https://api.regkasse.at/data/download/{token}</c>.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("data/download")]
[Produces("application/zip", "application/json")]
public sealed class DataExportDownloadController : ControllerBase
{
    private readonly IDataExportService _export;

    public DataExportDownloadController(IDataExportService export)
    {
        _export = export;
    }

    [HttpGet("{token}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Download(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var result = await _export.GetExportByDownloadTokenAsync(token, ct).ConfigureAwait(false);
        if (result?.Data == null)
            return NotFound(new { message = "Download link is invalid or has expired." });

        return File(
            result.Data,
            "application/zip",
            result.FileName ?? "data-export.zip");
    }
}

using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Anonymous time-limited download for large export emails.
/// Production URL: <c>https://api.regkasse.at/data/export-email/{token}</c>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("data/export-email")]
public sealed class ExportEmailDownloadController : ControllerBase
{
    private readonly IExportEmailDeliveryService _delivery;

    public ExportEmailDownloadController(IExportEmailDeliveryService delivery)
    {
        _delivery = delivery;
    }

    [HttpGet("{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string token, CancellationToken cancellationToken)
    {
        var opened = await _delivery
            .TryOpenDownloadByTokenAsync(token, cancellationToken)
            .ConfigureAwait(false);
        if (opened is null)
            return NotFound();

        return File(opened.Value.Stream, opened.Value.ContentType, opened.Value.FileName);
    }
}

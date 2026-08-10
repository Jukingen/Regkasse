using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Communication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin outbound communication (announcements).</summary>
[ApiController]
[Route("api/admin/communication")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class CommunicationController : ControllerBase
{
    private readonly IBulkEmailService _bulkEmail;
    private readonly ILogger<CommunicationController> _logger;

    public CommunicationController(IBulkEmailService bulkEmail, ILogger<CommunicationController> logger)
    {
        _bulkEmail = bulkEmail;
        _logger = logger;
    }

    /// <summary>Preview how many Manager recipients match the filters (no send).</summary>
    [HttpPost("bulk-email/preview")]
    [ProducesResponseType(typeof(BulkEmailPreviewResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkEmailPreviewResult>> PreviewBulkEmail(
        [FromBody] BulkEmailPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bulkEmail.PreviewAsync(request ?? new BulkEmailPreviewRequest(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Send HTML announcement emails to Manager admins of selected (or filtered) tenants.</summary>
    [HttpPost("bulk-email")]
    [ProducesResponseType(typeof(BulkEmailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<BulkEmailResult>> SendBulkEmail(
        [FromBody] BulkEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Subject and body are required." });

        try
        {
            var result = await _bulkEmail.SendBulkAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Bulk email rate limited");
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message, code = "BULK_EMAIL_RATE_LIMIT" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

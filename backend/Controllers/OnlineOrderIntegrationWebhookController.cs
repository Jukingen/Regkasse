using System.Text;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Marketplace order intake. Signature scheme is
/// <see cref="OnlineOrderIntegrationSignatureStatus.PendingConfirmationNote"/>.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/webhooks/online-order-integrations")]
public sealed class OnlineOrderIntegrationWebhookController : ControllerBase
{
    private readonly IOnlineOrderIntegrationWebhookService _webhooks;

    public OnlineOrderIntegrationWebhookController(IOnlineOrderIntegrationWebhookService webhooks)
    {
        _webhooks = webhooks;
    }

    [HttpPost("{provider}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(string provider, CancellationToken ct)
    {
        string raw;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            raw = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers[OnlineOrderIntegrationSignatureStatus.HeaderName].ToString();
        var result = await _webhooks.AcceptAsync(provider, raw, signature, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}

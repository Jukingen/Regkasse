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
    private readonly IWoltOrderNotificationService _wolt;

    public OnlineOrderIntegrationWebhookController(
        IOnlineOrderIntegrationWebhookService webhooks,
        IWoltOrderNotificationService wolt)
    {
        _webhooks = webhooks;
        _wolt = wolt;
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

        if (OnlineOrderIntegrationSignatureStatus.IsWolt(provider))
        {
            var woltSignature = Request.Headers[OnlineOrderIntegrationSignatureStatus.WoltHeaderName].ToString();
            var woltResult = await _wolt.AcceptAsync(raw, woltSignature, ct);
            if (woltResult.Succeeded)
                return woltResult.Id is Guid woltId ? Ok(new { id = woltId }) : Ok();

            return StatusCode(woltResult.StatusCode, new { code = woltResult.Code, message = woltResult.Error });
        }

        var signature = Request.Headers[OnlineOrderIntegrationSignatureStatus.HeaderName].ToString();
        var result = await _webhooks.AcceptAsync(provider, raw, signature, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}

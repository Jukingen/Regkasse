using System.Text;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Public payment-gateway webhooks. CSRF-exempt via <c>/api/webhooks/*</c>.
/// Signature is verified in <see cref="IPaymentGatewayService.VerifyWebhookAsync"/>.
/// Does not create fiscal receipts.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/webhooks/payment")]
[Route("api/webhooks/payments")]
[Produces("application/json")]
public sealed class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentGatewayService _payments;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(
        IPaymentGatewayService payments,
        ILogger<PaymentWebhookController> logger)
    {
        _payments = payments;
        _logger = logger;
    }

    [HttpPost("{provider}")]
    [ProducesResponseType(typeof(PaymentWebhookReceivedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Handle(string provider, CancellationToken cancellationToken)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var result = await _payments
            .VerifyWebhookAsync(provider, payload, Request.Headers, cancellationToken)
            .ConfigureAwait(false);

        if (!result.SignatureValid)
        {
            _logger.LogWarning(
                "Rejected payment webhook for provider {Provider}: {Code}",
                provider,
                result.ErrorCode);
            return BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        }

        return Ok(new PaymentWebhookReceivedResponse
        {
            Received = true,
            Status = result.Payment?.Status,
            OnlinePaymentId = result.Payment?.Id
        });
    }
}

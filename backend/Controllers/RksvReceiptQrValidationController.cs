using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV receipt QR format validation (internal, no BMF, no crypto verify).</summary>
[Authorize]
[ApiController]
[Route("api/rksv")]
public sealed class RksvReceiptQrValidationController : ControllerBase
{
    private readonly IRksvReceiptQrPayloadFormatValidator _validator;

    public RksvReceiptQrValidationController(IRksvReceiptQrPayloadFormatValidator validator)
    {
        _validator = validator;
    }

    /// <summary>Validates receipt QR payload structure emitted by this API (format only).</summary>
    [HttpPost("validate-receipt")]
    [HasPermission(AppPermissions.PaymentView)]
    [ProducesResponseType(typeof(RksvValidateReceiptQrResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<RksvValidateReceiptQrResponse> ValidateReceiptQr([FromBody] RksvValidateReceiptQrRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.QrPayload))
        {
            return BadRequest(new RksvValidateReceiptQrResponse
            {
                IsValidFormat = false,
                Parsed = null,
                Errors = new List<string> { "Request body with non-empty qrPayload is required." }
            });
        }

        var result = _validator.Validate(request.QrPayload);
        return Ok(result);
    }
}

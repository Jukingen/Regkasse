using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Security;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Backoffice Operations Center: Nachdruck mit receipt.reprint, Druck-Routing-Stub, keine Änderung an TSE/FiBu-Kernlogik.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/backoffice")]
[Produces("application/json")]
public class AdminBackofficeController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<AdminBackofficeController> _logger;

    public AdminBackofficeController(
        IPaymentService paymentService,
        ILogger<AdminBackofficeController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// GET: Platzhalter-Liste für Gerät/Drucker-Routing (später: echte Hardware-Registry).
    /// </summary>
    [HttpGet("print-routing/options")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [ProducesResponseType(typeof(PrintRoutingOptionsResponse), StatusCodes.Status200OK)]
    public ActionResult<PrintRoutingOptionsResponse> GetPrintRoutingOptions()
    {
        var response = new PrintRoutingOptionsResponse
        {
            Devices = new List<PrintRoutingDeviceOption>
            {
                new()
                {
                    Id = "default-register",
                    Label = "Standard (aktive Kasse / Browser-Druck)",
                    Kind = "default",
                    IsSimulated = true,
                },
            },
        };
        return Ok(response);
    }

    /// <summary>
    /// POST: Nachdruck bestätigen — strukturierte Audit-Zeile (<c>ReceiptReprintConfirmed</c> / <c>ReceiptReprintRejected</c>).
    /// Vorschau ohne Audit: <c>GET /api/Receipts/by-payment/{paymentId}</c> (SaleView).
    /// </summary>
    [HttpPost("receipts/{paymentId:guid}/reprint-request")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [ProducesResponseType(typeof(ReceiptReprintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReceiptReprintResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ReceiptReprintResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiptReprintResponse>> RequestReceiptReprint(
        Guid paymentId,
        [FromBody] ReceiptReprintRequest? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        _logger.LogInformation(
            "Backoffice reprint confirm: paymentId={PaymentId}, reasonCode={ReasonCode}, deviceId={DeviceId}",
            paymentId,
            body?.ReprintReasonCode,
            body?.DeviceId);

        var result = await _paymentService.ConfirmReceiptReprintAsync(paymentId, body, userId, cancellationToken).ConfigureAwait(false);

        var response = new ReceiptReprintResponse
        {
            Receipt = result.Receipt,
            Routing = result.Routing,
            AuditLogId = result.AuditLogId?.ToString(),
    };

        if (result.NotFound)
        {
            response.Outcome = "Failed";
            response.ErrorCode = result.ErrorCode;
            response.ErrorMessage = result.ErrorMessage;
            return NotFound(response);
        }

        if (!result.Success)
        {
            response.Outcome = "Failed";
            response.ErrorCode = result.ErrorCode;
            response.ErrorMessage = result.ErrorMessage;
            return BadRequest(response);
        }

        response.Outcome = "Success";
        response.ReportableEventType = "ReceiptReprintConfirmed";
        return Ok(response);
    }
}

using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS customer lookup (QR scan, number). Read-only; benefits applied at payment.</summary>
[Authorize]
[ApiController]
[Route("api/pos/customers")]
[HasPermission(AppPermissions.CustomerView)]
public sealed class PosCustomerController : BaseController
{
    private readonly IPosCustomerQrLookupService _qrLookup;

    public PosCustomerController(
        IPosCustomerQrLookupService qrLookup,
        ILogger<PosCustomerController> logger)
        : base(logger)
    {
        _qrLookup = qrLookup;
    }

    /// <summary>Resolve customer from scanned QR payload (customer:, RK:C:, RK:CU:, regkasse://, number, email).</summary>
    [HttpGet("by-qr")]
    [ProducesResponseType(typeof(PosCustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosCustomerDto>> GetCustomerByQr(
        [FromQuery] string qrData,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrData))
            return BadRequest(new { message = "qrData is required" });

        var parsed = CustomerQrPayloadParser.Parse(qrData);
        if (!parsed.Ok && !LooksLikeRawLookupToken(qrData))
            return BadRequest(new { message = parsed.Error });

        var customer = await _qrLookup.ResolveByQrDataAsync(qrData, cancellationToken);
        if (customer == null)
            return NotFound();

        return Ok(customer);
    }

    /// <summary>Legacy POST alias for QR lookup (same resolution as GET by-qr).</summary>
    [HttpPost("qr-lookup")]
    [ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QrLookup(
        [FromBody] CustomerQrLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var dto = await _qrLookup.ResolveByQrDataAsync(request.QrPayload, cancellationToken);
        if (dto == null)
            return NotFound(new { message = "Customer not found" });

        return SuccessResponse(MapLegacyCustomer(dto), "Customer retrieved successfully");
    }

    private static bool LooksLikeRawLookupToken(string qrData)
    {
        var trimmed = qrData.Trim();
        return Guid.TryParse(trimmed, out _)
               || trimmed.Contains('@', StringComparison.Ordinal)
               || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9_-]{1,20}$");
    }

    private static Customer MapLegacyCustomer(PosCustomerDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        CustomerNumber = dto.CustomerNumber,
        Email = dto.Email,
        Phone = dto.Phone,
        LoyaltyPoints = dto.LoyaltyPoints,
        IsActive = true,
    };
}

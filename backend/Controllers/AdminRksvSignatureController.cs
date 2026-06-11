using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[ApiController]
[Route("api/admin/rksv/signature")]
[Authorize]
[HasPermission(AppPermissions.AuditView)]
public sealed class AdminRksvSignatureController : ControllerBase
{
    private readonly IRksvSignatureVerifyService _verifyService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminRksvSignatureController(
        IRksvSignatureVerifyService verifyService,
        ICurrentTenantAccessor tenantAccessor)
    {
        _verifyService = verifyService;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// Verifies a single compact RKSV JWS signature against a certificate (explicit thumbprint or active TSE cert).
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(RksvSignatureVerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifySignature(
        [FromBody] RksvSignatureVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Signature))
        {
            return BadRequest(new { message = "signature is required." });
        }

        var result = await _verifyService
            .VerifyAsync(request.Signature, request.CertificateThumbprint, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }
}

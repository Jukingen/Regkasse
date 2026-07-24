using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE simulated blockchain anchoring (signature hash ledger — not RKSV proof).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/blockchain")]
[Produces("application/json")]
public sealed class AdminTseBlockchainController : ControllerBase
{
    private readonly ITseBlockchainService _blockchain;

    public AdminTseBlockchainController(ITseBlockchainService blockchain)
    {
        _blockchain = blockchain;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBlockchainStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseBlockchainStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await _blockchain.GetBlockchainStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("sync")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBlockchainStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseBlockchainStatusDto>> Sync(CancellationToken cancellationToken)
    {
        return Ok(await _blockchain.SyncBlockchainAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("transactions")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseBlockchainTransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseBlockchainTransactionDto>>> Transactions(
        [FromQuery] Guid tenantId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _blockchain
                .GetTransactionsAsync(tenantId, take, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("store")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBlockchainRecordDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseBlockchainRecordDto>> Store(
        [FromBody] TseBlockchainSignatureDataDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new TseBlockchainSignatureDataDto();
        try
        {
            return Ok(await _blockchain.StoreSignatureAsync(body, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{signatureId:guid}/verify")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBlockchainVerificationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseBlockchainVerificationResultDto>> Verify(
        Guid signatureId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _blockchain
                .VerifySignatureAsync(signatureId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

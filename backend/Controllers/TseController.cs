using System;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Controllers;

/// <summary>TSE diagnostics for POS and operators.</summary>
[Authorize]
[Route("api/tse")]
public sealed class TseController : BaseController
{
    private readonly ITseHealthMonitor _health;
    private readonly AppDbContext _db;

    public TseController(ITseHealthMonitor health, AppDbContext db, ILogger<TseController> logger)
        : base(logger)
    {
        _health = health;
        _db = db;
    }

    /// <summary>Returns cached TSE health from background probing.</summary>
    [HttpGet("health")]
    [HasPermission(AppPermissions.CashRegisterView)]
    public async Task<ActionResult<TseHealthResponseDto>> GetHealth(
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var s = _health.Snapshot;

        int? queueCount = null;
        if (cashRegisterId is { } rid && rid != Guid.Empty)
        {
            queueCount = await _db.OfflineTransactions.AsNoTracking()
                .CountAsync(
                    x => x.CashRegisterId == rid && x.Status == OfflineTransactionStatus.NonFiscalPending,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Ok(new TseHealthResponseDto
        {
            Status = s.Status.ToString(),
            LastCheckUtc = s.LastCheckUtc,
            LastSuccessfulPingUtc = s.LastSuccessfulPingUtc,
            ConsecutiveFailures = s.ConsecutiveFailures,
            EstimatedRecoveryTimeUtc = s.EstimatedRecoveryTimeUtc,
            LastErrorMessageSafe = s.LastErrorMessageSafe,
            NonFiscalPendingQueueCount = queueCount
        });
    }
}

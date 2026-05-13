using System;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>TSE diagnostics for POS and operators.</summary>
[Authorize]
[Route("api/tse")]
public sealed class TseController : BaseController
{
    private readonly ITseHealthMonitor _health;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<DevelopmentOptions> _developmentOptions;

    public TseController(
        ITseHealthMonitor health,
        AppDbContext db,
        IWebHostEnvironment environment,
        IOptionsMonitor<DevelopmentOptions> developmentOptions,
        ILogger<TseController> logger)
        : base(logger)
    {
        _health = health;
        _db = db;
        _environment = environment;
        _developmentOptions = developmentOptions;
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

        if (!OpenApiExportMode.IsEnabled
            && _environment.IsDevelopment()
            && _developmentOptions.CurrentValue.SimulateTseUnavailable)
        {
            return Ok(new TseHealthResponseDto
            {
                Status = TseOperationalHealth.Offline.ToString(),
                LastCheckUtc = DateTime.UtcNow,
                LastSuccessfulPingUtc = null,
                ConsecutiveFailures = Math.Max(s.ConsecutiveFailures, 99),
                EstimatedRecoveryTimeUtc = DateTime.UtcNow.AddMinutes(1),
                LastErrorMessageSafe = "Entwicklungssimulation: TSE als nicht verfügbar gemeldet.",
                NonFiscalPendingQueueCount = queueCount
            });
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

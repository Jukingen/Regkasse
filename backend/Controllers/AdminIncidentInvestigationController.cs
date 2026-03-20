using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: single payload for correlation-centred incident investigation (replay batch + audit + FO state per batch payment).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/incidents")]
[Produces("application/json")]
public class AdminIncidentInvestigationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminIncidentInvestigationController> _logger;

    public AdminIncidentInvestigationController(
        AppDbContext context,
        IAuditLogService auditLogService,
        ILogger<AdminIncidentInvestigationController> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// GET: Aggregated incident view for a replay batch correlation id (Guid, with or without dashes).
    /// </summary>
    [HttpGet("{correlationId}")]
    [HasPermission(AppPermissions.FinanzOnlineManage)]
    public async Task<ActionResult<IncidentInvestigationResponse>> GetIncident(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return BadRequest(new { message = "correlationId is required.", code = "INCIDENT_INVALID_ID" });

        if (!Guid.TryParse(correlationId.Replace("-", ""), out var batchId))
            return BadRequest(new { message = "correlationId must be a valid Guid.", code = "INCIDENT_INVALID_ID" });

        var replayBatch = await ReplayBatchDetailAssembler
            .BuildAsync(_context, batchId, cancellationToken)
            .ConfigureAwait(false);

        var auditKey = replayBatch.AuditCorrelationId;
        var auditEntities = (await _auditLogService
                .GetAuditLogsByCorrelationIdAsync(auditKey)
                .ConfigureAwait(false))
            .OrderBy(a => a.Timestamp)
            .ToList();

        var auditLogs = AuditLogEntryMapper.ToDtoList(auditEntities);

        var paymentIds = replayBatch.Payments.Select(p => p.PaymentId).ToList();
        IReadOnlyList<FinanzOnlineReconciliationItemDto> foItems = Array.Empty<FinanzOnlineReconciliationItemDto>();
        if (paymentIds.Count > 0)
        {
            foItems = await _context.PaymentDetails
                .AsNoTracking()
                .Where(p => paymentIds.Contains(p.Id))
                .Select(p => new FinanzOnlineReconciliationItemDto
                {
                    PaymentId = p.Id,
                    ReceiptNumber = p.ReceiptNumber,
                    CreatedAt = p.CreatedAt,
                    TotalAmount = p.TotalAmount,
                    CashRegisterId = p.CashRegisterId,
                    FinanzOnlineStatus = p.FinanzOnlineStatus,
                    FinanzOnlineError = p.FinanzOnlineError,
                    FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                    FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                    FinanzOnlineRetryCount = p.FinanzOnlineRetryCount
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var hints = BuildHints(auditEntities, foItems);

        _logger.LogInformation(
            "Incident aggregate: CorrelationId={CorrelationId}, Payments={PaymentCount}, AuditEntries={AuditCount}, FoRows={FoCount}",
            batchId, paymentIds.Count, auditEntities.Count, foItems.Count);

        return Ok(new IncidentInvestigationResponse
        {
            ReplayBatch = replayBatch,
            AuditLogs = auditLogs,
            FinanzOnlineReconciliation = foItems,
            Hints = hints
        });
    }

    private static IncidentInvestigationHintsDto BuildHints(
        IReadOnlyList<AuditLog> auditEntities,
        IReadOnlyList<FinanzOnlineReconciliationItemDto> foItems)
    {
        var actions = new HashSet<string>(auditEntities.Select(a => a.Action), StringComparer.Ordinal);
        return new IncidentInvestigationHintsDto
        {
            HasLockTimeoutAudit = actions.Contains("OfflineReplayLockTimeout"),
            HasPayloadImmutableMismatchAudit = actions.Contains("PAYLOAD_IMMUTABLE_MISMATCH"),
            FinanzOnlineSubmittedCount = foItems.Count(i =>
                string.Equals(i.FinanzOnlineStatus, "Submitted", StringComparison.OrdinalIgnoreCase)),
            FinanzOnlineOpenOrProblemCount = foItems.Count(i =>
                i.FinanzOnlineStatus == null ||
                !string.Equals(i.FinanzOnlineStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
        };
    }
}

public sealed class IncidentInvestigationResponse
{
    public ReplayBatchDetailResponse ReplayBatch { get; set; } = null!;
    public IReadOnlyList<AuditLogEntryDto> AuditLogs { get; set; } = Array.Empty<AuditLogEntryDto>();
    public IReadOnlyList<FinanzOnlineReconciliationItemDto> FinanzOnlineReconciliation { get; set; } =
        Array.Empty<FinanzOnlineReconciliationItemDto>();
    public IncidentInvestigationHintsDto Hints { get; set; } = null!;
}

public sealed class IncidentInvestigationHintsDto
{
    public bool HasLockTimeoutAudit { get; set; }
    public bool HasPayloadImmutableMismatchAudit { get; set; }
    public int FinanzOnlineSubmittedCount { get; set; }
    public int FinanzOnlineOpenOrProblemCount { get; set; }
}

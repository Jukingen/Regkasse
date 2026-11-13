using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin Peppol participant ids. No credential is accepted or stored.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/peppol/participants")]
[Produces("application/json")]
public sealed class AdminPeppolParticipantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IAuditLogService _audit;
    private readonly IActivityEventPublisher? _activity;

    public AdminPeppolParticipantsController(
        AppDbContext db,
        ICurrentTenantAccessor tenant,
        IAuditLogService audit,
        IActivityEventPublisher? activity = null)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _activity = activity;
    }

    [HttpGet("{tenantId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<PeppolParticipantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PeppolParticipantDto>>> List(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!await CanSeeTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var rows = await _db.PeppolParticipants.AsNoTracking()
            .Where(row => row.TenantId == tenantId)
            .OrderBy(row => row.ParticipantId)
            .Select(row => new PeppolParticipantDto(
                row.Id,
                row.TenantId,
                row.ParticipantId,
                row.ApEnvironment,
                row.CreatedAtUtc,
                row.LegalEntityId,
                row.EIdentifierScheme,
                row.EIdentifierValue))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    [HttpPost]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PeppolParticipantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeppolParticipantDto>> Register(
        [FromBody] RegisterPeppolParticipantRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || request.TenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.ParticipantId)
            || !PeppolApEnvironments.IsAllowed(request.ApEnvironment)
            || Exceeds(request.LegalEntityId, 255)
            || Exceeds(request.EIdentifierScheme, 64)
            || Exceeds(request.EIdentifierValue, 255))
        {
            return BadRequest();
        }

        if (!await CanSeeTenantAsync(request.TenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var now = DateTime.UtcNow;
        var row = new PeppolParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ParticipantId = request.ParticipantId.Trim(),
            ApEnvironment = request.ApEnvironment.Trim(),
            LegalEntityId = BlankToNull(request.LegalEntityId),
            EIdentifierScheme = BlankToNull(request.EIdentifierScheme),
            EIdentifierValue = BlankToNull(request.EIdentifierValue),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.PeppolParticipants.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        await _audit.LogSystemOperationAsync(
            action: "PEPPOL_PARTICIPANT_REGISTERED",
            entityType: "PeppolParticipant",
            userId: userId,
            userRole: "SuperAdmin",
            description: "Peppol participant registered",
            actionType: AuditEventType.PeppolParticipantRegistered,
            entityId: row.Id,
            tenantId: row.TenantId,
            newValues: new
            {
                participantId = row.ParticipantId,
                apEnvironment = row.ApEnvironment,
                legalEntityId = row.LegalEntityId,
                eIdentifierScheme = row.EIdentifierScheme,
                eIdentifierValue = row.EIdentifierValue,
            },
            correlationIdOverride: Guid.NewGuid().ToString("D")).ConfigureAwait(false);

        if (_activity is not null)
        {
            await _activity.TryPublishAsync(
                row.TenantId,
                ActivityEventType.PeppolParticipantRegistered,
                metadata: new { participantId = row.ParticipantId, apEnvironment = row.ApEnvironment },
                actorUserId: userId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var dto = ToDto(row);
        return Created($"/api/admin/peppol/participants/{row.TenantId}", dto);
    }

    private static PeppolParticipantDto ToDto(PeppolParticipant row) =>
        new(
            row.Id,
            row.TenantId,
            row.ParticipantId,
            row.ApEnvironment,
            row.CreatedAtUtc,
            row.LegalEntityId,
            row.EIdentifierScheme,
            row.EIdentifierValue);

    private static string? BlankToNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private static bool Exceeds(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength;

    private async Task<bool> CanSeeTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not Guid ambient || ambient == Guid.Empty || ambient != tenantId)
            return false;

        return await _db.Tenants.AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record RegisterPeppolParticipantRequest(
    Guid TenantId,
    string ParticipantId,
    string ApEnvironment,
    string? LegalEntityId = null,
    string? EIdentifierScheme = null,
    string? EIdentifierValue = null);

public sealed record PeppolParticipantDto(
    Guid Id,
    Guid TenantId,
    string ParticipantId,
    string ApEnvironment,
    DateTime CreatedAtUtc,
    string? LegalEntityId = null,
    string? EIdentifierScheme = null,
    string? EIdentifierValue = null);

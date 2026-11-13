using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// TSE operational incident CRUD, status workflow, and diagnostic reports.
/// </summary>
public sealed class TseIncidentService : ITseIncidentService
{
    private const int MaxPeriodDays = 366;

    private readonly AppDbContext _db;
    private readonly IActivityEventPublisher _activity;
    private readonly ILogger<TseIncidentService> _logger;

    public TseIncidentService(
        AppDbContext db,
        IActivityEventPublisher activity,
        ILogger<TseIncidentService> logger)
    {
        _db = db;
        _activity = activity;
        _logger = logger;
    }

    public async Task<TseIncidentDto> CreateIncidentAsync(
        CreateTseIncidentRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.", nameof(request));

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length is < 3 or > 200)
            throw new ArgumentException("Title must be 3–200 characters.", nameof(request));

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length is < 3 or > 4000)
            throw new ArgumentException("Description must be 3–4000 characters.", nameof(request));

        var severity = NormalizeSeverity(request.Severity);
        var detectedAt = NormalizeUtc(request.DetectedAt ?? DateTime.UtcNow);

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant {request.TenantId} was not found.");

        string? deviceLabel = null;
        if (request.DeviceId is { } deviceId && deviceId != Guid.Empty)
        {
            var device = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
                .ConfigureAwait(false);
            if (device is null)
                throw new KeyNotFoundException($"TSE device {deviceId} was not found.");
            if (device.TenantId is { } deviceTenant && deviceTenant != request.TenantId)
                throw new ArgumentException("Device does not belong to the specified tenant.", nameof(request));
            deviceLabel = string.IsNullOrWhiteSpace(device.DeviceId) ? device.SerialNumber : device.DeviceId;
        }

        var incident = new TseIncident
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            DeviceId = request.DeviceId is { } d && d != Guid.Empty ? d : null,
            Title = title,
            Description = description,
            Severity = severity,
            Status = TseIncidentStatuses.Open,
            DetectedAt = detectedAt,
            CreatedBy = Truncate(actorUserId, 450),
            CreatedAt = DateTime.UtcNow,
        };

        incident.Logs.Add(new TseIncidentLog
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            EventType = TseIncidentLogEventTypes.Created,
            Message = $"Incident created with severity {severity}.",
            ActorUserId = Truncate(actorUserId, 450),
            CreatedAt = DateTime.UtcNow,
        });

        _db.TseIncidents.Add(incident);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();

        await _activity.TryPublishAsync(
                request.TenantId,
                ActivityEventType.TseIncidentCreated,
                new
                {
                    IncidentId = incident.Id.ToString("D"),
                    DeviceId = incident.DeviceId?.ToString("D"),
                    DeviceLabel = deviceLabel,
                    incident.Title,
                    incident.Severity,
                    incident.Status,
                },
                actorUserId: actorUserId ?? "system",
                dedupKey: $"tse-incident-created:{incident.Id:N}",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Created TSE incident IncidentId={IncidentId} TenantId={TenantId} Severity={Severity}",
            incident.Id,
            incident.TenantId,
            incident.Severity);

        return await MapIncidentAsync(incident.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TseIncidentDto> UpdateIncidentStatusAsync(
        Guid incidentId,
        string status,
        string? resolution = null,
        string? note = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (incidentId == Guid.Empty)
            throw new ArgumentException("incidentId is required.", nameof(incidentId));

        var nextStatus = NormalizeStatus(status);
        var incident = await _db.TseIncidents
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken)
            .ConfigureAwait(false);
        if (incident is null)
            throw new KeyNotFoundException($"Incident {incidentId} was not found.");

        var previous = incident.Status;
        if (string.Equals(previous, nextStatus, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(note)
            && string.IsNullOrWhiteSpace(resolution))
        {
            return await MapIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);
        }

        EnsureValidTransition(previous, nextStatus);

        incident.Status = nextStatus;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.UpdatedBy = Truncate(actorUserId, 450);

        if (nextStatus is TseIncidentStatuses.Resolved or TseIncidentStatuses.Closed)
        {
            incident.ResolvedAt ??= DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(resolution))
                incident.Resolution = Truncate(resolution.Trim(), 4000);
            else if (string.IsNullOrWhiteSpace(incident.Resolution))
                throw new ArgumentException("Resolution is required when resolving or closing an incident.");
        }
        else
        {
            incident.ResolvedAt = null;
        }

        var message = $"Status changed from {previous} to {nextStatus}.";
        if (!string.IsNullOrWhiteSpace(note))
            message += $" Note: {Truncate(note.Trim(), 500)}";

        _db.TseIncidentLogs.Add(new TseIncidentLog
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            EventType = TseIncidentLogEventTypes.StatusChanged,
            Message = message,
            ActorUserId = Truncate(actorUserId, 450),
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();

        if (nextStatus is TseIncidentStatuses.Resolved or TseIncidentStatuses.Closed)
        {
            await _activity.TryPublishAsync(
                    incident.TenantId,
                    ActivityEventType.TseIncidentResolved,
                    new
                    {
                        IncidentId = incident.Id.ToString("D"),
                        DeviceId = incident.DeviceId?.ToString("D"),
                        incident.Title,
                        incident.Severity,
                        Status = nextStatus,
                        incident.Resolution,
                    },
                    actorUserId: actorUserId ?? "system",
                    dedupKey: $"tse-incident-resolved:{incident.Id:N}:{nextStatus}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return await MapIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TseIncidentDto> AddIncidentActionAsync(
        Guid incidentId,
        AddTseIncidentActionRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (incidentId == Guid.Empty)
            throw new ArgumentException("incidentId is required.", nameof(incidentId));

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length is < 2 or > 2000)
            throw new ArgumentException("Description must be 2–2000 characters.", nameof(request));

        var actionType = string.IsNullOrWhiteSpace(request.ActionType)
            ? TseIncidentActionTypes.Other
            : Truncate(request.ActionType.Trim(), 64)!;

        var incident = await _db.TseIncidents
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken)
            .ConfigureAwait(false);
        if (incident is null)
            throw new KeyNotFoundException($"Incident {incidentId} was not found.");

        _db.TseIncidentActions.Add(new TseIncidentAction
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            ActionType = actionType,
            Description = description,
            PerformedBy = Truncate(actorUserId, 450),
            PerformedAt = DateTime.UtcNow,
            IsCompleted = request.IsCompleted,
        });

        _db.TseIncidentLogs.Add(new TseIncidentLog
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            EventType = TseIncidentLogEventTypes.ActionAdded,
            Message = $"Action added ({actionType}): {Truncate(description, 200)}",
            ActorUserId = Truncate(actorUserId, 450),
            CreatedAt = DateTime.UtcNow,
        });

        incident.UpdatedAt = DateTime.UtcNow;
        incident.UpdatedBy = Truncate(actorUserId, 450);
        if (string.Equals(incident.Status, TseIncidentStatuses.Open, StringComparison.OrdinalIgnoreCase))
            incident.Status = TseIncidentStatuses.Investigating;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        return await MapIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TseIncidentReportDto> GenerateIncidentReportAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await MapIncidentAsync(incidentId, cancellationToken).ConfigureAwait(false);

        var timeToResolve = incident.ResolvedAt is { } resolved
            ? resolved - incident.DetectedAt
            : (TimeSpan?)null;

        var completed = incident.Actions.Count(a => a.IsCompleted);
        var summary =
            $"TSE incident '{incident.Title}' ({incident.Severity}/{incident.Status}) "
            + $"detected {incident.DetectedAt:u}"
            + (incident.ResolvedAt is { } r ? $", resolved {r:u}" : ", still open")
            + $". {incident.Logs.Count} log entries, {completed}/{incident.Actions.Count} actions completed.";

        var tracked = await _db.TseIncidents
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken)
            .ConfigureAwait(false);
        if (tracked is not null)
        {
            _db.TseIncidentLogs.Add(new TseIncidentLog
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                EventType = TseIncidentLogEventTypes.ReportGenerated,
                Message = "Incident report generated.",
                ActorUserId = "system",
                CreatedAt = DateTime.UtcNow,
            });
            tracked.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
        }

        return new TseIncidentReportDto
        {
            IncidentId = incident.Id,
            TenantId = incident.TenantId,
            TenantName = incident.TenantName,
            DeviceId = incident.DeviceId,
            DeviceLabel = incident.DeviceLabel,
            Title = incident.Title,
            Severity = incident.Severity,
            Status = incident.Status,
            DetectedAt = incident.DetectedAt,
            ResolvedAt = incident.ResolvedAt,
            TimeToResolve = timeToResolve,
            Summary = summary,
            Resolution = incident.Resolution,
            LogCount = incident.Logs.Count,
            ActionCount = incident.Actions.Count,
            CompletedActionCount = completed,
            GeneratedAt = DateTime.UtcNow,
            Timeline = incident.Logs,
            Actions = incident.Actions,
        };
    }

    public async Task<IReadOnlyList<TseIncidentDto>> GetIncidentsAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        fromUtc = NormalizeUtc(fromUtc);
        toUtc = NormalizeUtc(toUtc);
        if (toUtc <= fromUtc)
            throw new ArgumentException("toUtc must be strictly greater than fromUtc.", nameof(toUtc));
        if ((toUtc - fromUtc).TotalDays > MaxPeriodDays)
            throw new ArgumentException($"Period must be at most {MaxPeriodDays} days.", nameof(toUtc));

        var query = _db.TseIncidents.AsNoTracking()
            .Where(i => i.DetectedAt >= fromUtc && i.DetectedAt < toUtc);
        if (tenantId is { } tid && tid != Guid.Empty)
            query = query.Where(i => i.TenantId == tid);

        var ids = await query
            .OrderByDescending(i => i.DetectedAt)
            .Select(i => i.Id)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = new List<TseIncidentDto>(ids.Count);
        foreach (var id in ids)
            list.Add(await MapIncidentAsync(id, cancellationToken).ConfigureAwait(false));
        return list;
    }

    public Task<TseIncidentDto> GetIncidentAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default) =>
        MapIncidentAsync(incidentId, cancellationToken);

    public async Task<TseIncidentDashboardDto> GetDashboardAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-30);
        var incidents = await GetIncidentsAsync(tenantId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        return new TseIncidentDashboardDto
        {
            OpenCount = incidents.Count(i => i.Status == TseIncidentStatuses.Open),
            InvestigatingCount = incidents.Count(i => i.Status == TseIncidentStatuses.Investigating),
            ResolvedCount = incidents.Count(i => i.Status == TseIncidentStatuses.Resolved),
            ClosedCount = incidents.Count(i => i.Status == TseIncidentStatuses.Closed),
            CriticalOpenCount = incidents.Count(i =>
                i.Severity == TseIncidentSeverities.Critical
                && i.Status is TseIncidentStatuses.Open or TseIncidentStatuses.Investigating),
            Incidents = incidents,
        };
    }

    private async Task<TseIncidentDto> MapIncidentAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _db.TseIncidents.AsNoTracking()
            .Include(i => i.Logs)
            .Include(i => i.Actions)
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken)
            .ConfigureAwait(false);
        if (incident is null)
            throw new KeyNotFoundException($"Incident {incidentId} was not found.");

        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == incident.TenantId, cancellationToken)
            .ConfigureAwait(false);

        string? deviceLabel = null;
        if (incident.DeviceId is { } deviceId)
        {
            var device = await _db.TseDevices.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
                .ConfigureAwait(false);
            if (device is not null)
                deviceLabel = string.IsNullOrWhiteSpace(device.DeviceId) ? device.SerialNumber : device.DeviceId;
        }

        return new TseIncidentDto
        {
            Id = incident.Id,
            TenantId = incident.TenantId,
            TenantName = tenant?.Name,
            TenantSlug = tenant?.Slug,
            DeviceId = incident.DeviceId,
            DeviceLabel = deviceLabel,
            Title = incident.Title,
            Description = incident.Description,
            Severity = incident.Severity,
            Status = incident.Status,
            DetectedAt = incident.DetectedAt,
            ResolvedAt = incident.ResolvedAt,
            Resolution = incident.Resolution,
            CreatedBy = incident.CreatedBy,
            CreatedAt = incident.CreatedAt,
            UpdatedAt = incident.UpdatedAt,
            Logs = incident.Logs
                .OrderBy(l => l.CreatedAt)
                .Select(l => new TseIncidentLogDto
                {
                    Id = l.Id,
                    EventType = l.EventType,
                    Message = l.Message,
                    ActorUserId = l.ActorUserId,
                    CreatedAt = l.CreatedAt,
                })
                .ToList(),
            Actions = incident.Actions
                .OrderBy(a => a.PerformedAt)
                .Select(a => new TseIncidentActionDto
                {
                    Id = a.Id,
                    ActionType = a.ActionType,
                    Description = a.Description,
                    PerformedBy = a.PerformedBy,
                    PerformedAt = a.PerformedAt,
                    IsCompleted = a.IsCompleted,
                })
                .ToList(),
        };
    }

    private static string NormalizeSeverity(string? value)
    {
        var raw = (value ?? TseIncidentSeverities.Medium).Trim();
        if (!TseIncidentSeverities.IsValid(raw))
            throw new ArgumentException("Severity must be Critical, High, Medium, or Low.");
        return TseIncidentSeverities.All.First(s => string.Equals(s, raw, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeStatus(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (!TseIncidentStatuses.IsValid(raw))
            throw new ArgumentException("Status must be Open, Investigating, Resolved, or Closed.");
        return TseIncidentStatuses.All.First(s => string.Equals(s, raw, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureValidTransition(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return;

        // Allow reopen from Closed/Resolved back to Investigating/Open; otherwise linear-ish.
        var allowed = (from, to) switch
        {
            (TseIncidentStatuses.Open, TseIncidentStatuses.Investigating) => true,
            (TseIncidentStatuses.Open, TseIncidentStatuses.Resolved) => true,
            (TseIncidentStatuses.Open, TseIncidentStatuses.Closed) => true,
            (TseIncidentStatuses.Investigating, TseIncidentStatuses.Resolved) => true,
            (TseIncidentStatuses.Investigating, TseIncidentStatuses.Closed) => true,
            (TseIncidentStatuses.Investigating, TseIncidentStatuses.Open) => true,
            (TseIncidentStatuses.Resolved, TseIncidentStatuses.Closed) => true,
            (TseIncidentStatuses.Resolved, TseIncidentStatuses.Investigating) => true,
            (TseIncidentStatuses.Closed, TseIncidentStatuses.Investigating) => true,
            (TseIncidentStatuses.Closed, TseIncidentStatuses.Open) => true,
            _ => false,
        };

        if (!allowed)
            throw new ArgumentException($"Invalid status transition from {from} to {to}.");
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}

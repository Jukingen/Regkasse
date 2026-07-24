using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Super Admin TSE operational incident tracking (not fiscal/RKSV evidence).
/// </summary>
public interface ITseIncidentService
{
    Task<TseIncidentDto> CreateIncidentAsync(
        CreateTseIncidentRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseIncidentDto> UpdateIncidentStatusAsync(
        Guid incidentId,
        string status,
        string? resolution = null,
        string? note = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseIncidentDto> AddIncidentActionAsync(
        Guid incidentId,
        AddTseIncidentActionRequestDto request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseIncidentReportDto> GenerateIncidentReportAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseIncidentDto>> GetIncidentsAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<TseIncidentDto> GetIncidentAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<TseIncidentDashboardDto> GetDashboardAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}

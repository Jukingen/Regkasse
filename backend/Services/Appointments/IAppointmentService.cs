namespace KasseAPI_Final.Services.Appointments;

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentDto>> ListAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? staffId,
        CancellationToken cancellationToken);

    Task<AppointmentWriteResult> CreateAsync(
        CreatePosAppointmentRequest request,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);

    Task<AppointmentWriteResult> UpdateAsync(
        Guid id,
        UpdatePosAppointmentRequest request,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);

    Task<AppointmentWriteResult> CancelAsync(
        Guid id,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);
}

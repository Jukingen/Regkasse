using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Appointments;

public static class AppointmentConflictCodes
{
    public const string Conflict = "APPOINTMENT_CONFLICT";
}

public sealed class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ServiceProductId { get; set; }
    public string? StaffId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppointmentStatus Status { get; set; }

    public string? Notes { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
}

public sealed class AppointmentConflictDto
{
    public string Code { get; set; } = AppointmentConflictCodes.Conflict;
    public string Message { get; set; } = "Appointment slot conflict.";
    public AppointmentDto Appointment { get; set; } = null!;
}

public sealed class CreatePosAppointmentRequest
{
    public Guid? CustomerId { get; set; }

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    public Guid? ServiceProductId { get; set; }

    [MaxLength(450)]
    public string? StaffId { get; set; }

    [Required]
    public DateTime StartUtc { get; set; }

    public DateTime? EndUtc { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Optimistic slot token. <c>0</c>/omitted means "slot is empty".
    /// A mismatch against an existing active row returns HTTP 409.
    /// </summary>
    public int? ExpectedVersion { get; set; }

    public AppointmentStatus? Status { get; set; }
}

public sealed class UpdatePosAppointmentRequest
{
    public Guid? CustomerId { get; set; }

    [MaxLength(450)]
    public string? StaffId { get; set; }

    public Guid? ServiceProductId { get; set; }

    public DateTime? StartUtc { get; set; }

    public DateTime? EndUtc { get; set; }

    public string? Notes { get; set; }

    public AppointmentStatus? Status { get; set; }

    [Required]
    public int ExpectedVersion { get; set; }
}

public readonly record struct AppointmentWriteResult(
    AppointmentDto? Appointment,
    AppointmentDto? Conflict,
    string? ErrorCode,
    int StatusCode);

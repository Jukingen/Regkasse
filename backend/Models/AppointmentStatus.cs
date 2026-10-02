using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppointmentStatus
{
    Booked = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4,
}

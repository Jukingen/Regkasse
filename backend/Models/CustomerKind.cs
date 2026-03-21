using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>
/// Explicit customer classification for requests (avoids inferring meaning from nulls alone).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomerKind
{
    Registered,
    WalkIn,
    EmployeeBenefitSubject
}

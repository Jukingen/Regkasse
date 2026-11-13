namespace KasseAPI_Final.DTOs;

public sealed class JahresbelegTenantStatusDto
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public int Year { get; init; }
    public DateOnly Deadline { get; init; }

    /// <summary><c>created</c>, <c>missing</c>, or <c>notDue</c>.</summary>
    public string Status { get; init; } = "notDue";

    public string? Warning { get; init; }
}
